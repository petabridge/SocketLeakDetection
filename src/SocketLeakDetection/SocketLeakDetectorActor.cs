using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Akka.Event;
using static SocketLeakDetection.Messages;

namespace SocketLeakDetection
{
    /// <summary>
    /// Simple data structure for self-contained EMWA mathematics.
    /// </summary>
    public struct EMWA
    {
        public EMWA(double alpha, double currentAvg)
        {
            Alpha = alpha;
            CurrentAvg = currentAvg;
        }

        public double Alpha { get; }

        public double CurrentAvg { get; }

        public EMWA Next(int nextValue)
        {
            return new EMWA(Alpha, Alpha*nextValue+(1-Alpha)*CurrentAvg);
        }

        public static EMWA Init(int sampleSize, int firstReading)
        {
            var alpha = 2.0 / (sampleSize + 1);
            return new EMWA(alpha, firstReading);
        }

        public static double operator % (EMWA e1, EMWA e2)
        {
            return (e1.CurrentAvg - e2.CurrentAvg) / (e1.CurrentAvg);
        }

        public static EMWA operator +(EMWA e1, int next)
        {
            return e1.Next(next);
        }
    }

    /// <summary>
    /// The leak detection business logic.
    /// </summary>
    public sealed class LeakDetector
    {
        public const int DefaultShortSampleSize = 10;
        public const int DefaultLongSampleSize = 30;
        private bool _minThresholdBreached = false;

        public LeakDetector(SocketLeakDetectorSettings settings)
            : this(settings.MinConnections, settings.MaxDifference, 
                settings.MaxConnections, settings.ShortSampleSize, settings.LongSampleSize) { }

        public LeakDetector(int minConnectionCount, double maxDifference, int maxConnectionCount, 
            int shortSampleSize = DefaultShortSampleSize, int longSampleSize = DefaultLongSampleSize)
        {
            MinConnectionCount = minConnectionCount;
            if(MinConnectionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(minConnectionCount), "MinConnectionCount must be at least 1");
           
            MaxDifference = maxDifference;
            if(MaxDifference <= 0.0d)
                throw new ArgumentOutOfRangeException(nameof(maxDifference), "MaxDifference must be greater than 0.0");

            MaxConnectionCount = maxConnectionCount;
            if (MaxConnectionCount <= MinConnectionCount)
                throw new ArgumentOutOfRangeException(nameof(maxConnectionCount), "MaxConnectionCount must be greater than MinConnectionCount");

            // default both EMWAs to the minimum connection count.
            Short = EMWA.Init(shortSampleSize, minConnectionCount);
            Long = EMWA.Init(longSampleSize, minConnectionCount);
        }

        /// <summary>
        /// Moving average - long
        /// </summary>
        public EMWA Long { get; private set; }

        /// <summary>
        /// Moving average - short
        /// </summary>
        public EMWA Short { get; private set; }

        public double RelativeDifference => Short % Long;

        public double MaxDifference { get; }

        /// <summary>
        /// Below this threshold, don't start tracking the rate of connection growth.
        /// </summary>
        public int MinConnectionCount { get; }

        /// <summary>
        /// If the connection count exceeds this threshold, signal failure anyway regardless of the averages.
        /// Meant to act as a stop-loss mechanism in the event of a _very_ slow upward creep in connections over time.
        /// </summary>
        public int MaxConnectionCount { get; }

        /// <summary>
        /// The current number of connections.
        /// </summary>
        public int CurrentConnectionCount { get; private set; }

        /// <summary>
        /// Feed the next connection count into the <see cref="LeakDetector"/>.
        /// </summary>
        /// <param name="newConnectionCount">The updated connection count.</param>
        /// <returns>The current <see cref="LeakDetector"/> instance but with updated state.</returns>
        public LeakDetector Next(int newConnectionCount)
        {
            CurrentConnectionCount = newConnectionCount;
            if (CurrentConnectionCount >= MinConnectionCount) // time to start using samples
            {
                // Used to signal that we've crossed the threshold
                if (!_minThresholdBreached)
                    _minThresholdBreached = true;

                Long += CurrentConnectionCount;
                Short += CurrentConnectionCount;
            }
            else if (_minThresholdBreached) // fell back below minimum for first time
            {
                _minThresholdBreached = false;

                // reset averages back to starting position
                Long = new EMWA(Long.Alpha, CurrentConnectionCount);
                Short = new EMWA(Short.Alpha, CurrentConnectionCount);
            }

            return this;
        }

        /// <summary>
        /// Returns <c>true</c> if the <see cref="CurrentConnectionCount"/> exceeds <see cref="MaxConnectionCount"/>
        /// or if <see cref="RelativeDifference"/> exceeds <see cref="MaxDifference"/>.
        /// </summary>
        public bool ShouldFail => RelativeDifference >= MaxDifference || CurrentConnectionCount >= MaxConnectionCount;
    }

    public class SocketLeakDetectorActor: UntypedActor
    {
        private sealed class TcpCount
        {
            public static readonly TcpCount Instance = new TcpCount();
            private TcpCount() { }
        }

        private readonly SocketLeakDetectorSettings _settings;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private LeakDetector _leakDetector;
        private IActorRef _supervisor;
        private ITcpCounter _tCounter; //TCP counter set to be used.

        /// <summary>
        /// Fired when the system is in failure - cancelled when the numbers fall back in line.
        /// </summary>
        private ICancelable _breachSignal;

        /// <summary>
        /// Used to check the ports via the <see cref="_tCounter"/>.
        /// </summary>
        private ICancelable _portCheck;

        /// <summary>
        /// Constructor will setup the values that we will need to determine if we need to message our supervisor actor in case we experience an increase in TCP connections
        /// </summary>
        /// <param name="settings">The settings for this actor's leak detection algorithm.</param>
        /// <param name="counter">TCP counter class we want to use to determine the number of opened TCP connections</param>
        /// <param name="supervisor">Actor Reference for the Supervisor Actor in charge of terminating Actor System</param>
        public SocketLeakDetectorActor(SocketLeakDetectorSettings settings, ITcpCounter counter, IActorRef supervisor)
        {
            _supervisor = supervisor;
            _settings = settings;
            _tCounter = counter;
        }

        protected override void OnReceive(object message)
        {
            if (message is TcpCount)
            {
                var count = _tCounter.GetTcpCount(); //Get TCP count
                _leakDetector.Next(count);

                if (_leakDetector.ShouldFail)
                {
                    _log.Warning("Current port count detected to be {0} - triggering ActorSystem termination in {1} seconds unless port count stabilizes.", count, _settings.BreachDuration);
                    _breachSignal = Context.System.Scheduler.ScheduleTellOnceCancelable(_settings.BreachDuration, Self,
                        TimerExpired.Instance, ActorRefs.NoSender);
                }
                else if(_breachSignal != null)
                {
                    _breachSignal.Cancel();
                    _log.Warning("Port count back down to {0} - within healthy levels. Cancelling shutdown.", count);
                }
            }
            else if(message is TimerExpired)
            {
                _supervisor.Tell(new Stat { CurretStatus = 2 }); // Signals that an increase has been observed for 60 seconds and we should terminate. 
            }
        }


        protected override void PreStart()
        {
            //A commonly used value for alpa is alpha = 2/(N+1). This is because the weights of an SMA and EMA have the same "center of mass"  when alpa(rma)=2/(N(rma)+1)
            //https://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
            _portCheck = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(0), _settings.PortCheckInterval,
                Self, TcpCount.Instance, ActorRefs.NoSender); //Schedule TCP counts to happen every 500 ms.
            _leakDetector = new LeakDetector(_settings);
        }

        protected override void PostStop()
        {
            _portCheck?.Cancel();
            _breachSignal?.Cancel();
        }
    }
}

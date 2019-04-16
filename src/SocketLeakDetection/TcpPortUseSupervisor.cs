// -----------------------------------------------------------------------
// <copyright file="TcpPortUseSupervisor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;

namespace SocketLeakDetection
{
    /// <summary>
    ///     Actor responsible for instantiating the monitoring system.
    /// </summary>
    public class TcpPortUseSupervisor : ReceiveActor
    {
        private readonly Props _childProps;
        private readonly ILoggingAdapter _log = Context.GetLogger();

        private readonly SocketLeakDetectorSettings _settings;
        private IActorRef _tcpScanner;

        /// <summary>
        ///     Supervisor actor used to determine if we need to log warning about increase in TCP connections or terminate the
        ///     Actor System.
        ///     Created using the default <see cref="SocketLeakDetectorSettings" />.
        /// </summary>
        public TcpPortUseSupervisor() : this(new SocketLeakDetectorSettings())
        {
        }

        /// <summary>
        ///     Supervisor actor used to determine if we need to log warning about increase in TCP connections or terminate the
        ///     Actor System.
        /// </summary>
        public TcpPortUseSupervisor(SocketLeakDetectorSettings settings)
        {
            _settings = settings;
            _childProps = Props.Create(() => new SocketLeakDetectorActor(_settings, Self));

            Receive<TcpCount>(t =>
            {
                var childName = Uri.EscapeUriString(t.HostInterface.ToString());
                var child = Context.Child(childName).GetOrElse(() =>
                    Context.ActorOf(_childProps, childName));

                child.Forward(t);
            });

            Receive<Shutdown>(_ =>
            {
                _log.Warning("Received shutdown notification from LeakDetector. Terminating ActorSystem");

                // trigger shutdown using custom reason
                CoordinatedShutdown.Get(Context.System).Run(PortLeakReason.Instance);
            });
        }

        private long MaxConnections { get; set; }
        private double PercentDifference { get; set; }
        private double MaxDifference { get; set; }
        private int LargeSample { get; set; }
        private int SmallSample { get; set; }

        protected override void PreStart()
        {
            _tcpScanner = Context.ActorOf(Props.Create(() => new TcpPortMonitoringActor(Self, _settings)),
                "portMonitor");
            Context.Watch(_tcpScanner);
        }

        /// <summary>
        ///     Sets configuration variables for Percent Difference actor.
        /// </summary>
        /// <param name="config"></param>
        private void GetConfig(Config config)
        {
            var actorConfig = config.GetConfig("SLD");
            if (actorConfig != null)
            {
                var mx = Convert.ToInt64(actorConfig.GetString("Max-Connections",
                    "16777214")); // Set value to theoretical max TCP connections for Windows Server 2003 https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2003/cc758980(v=ws.10)
                if (mx > 0) // If no value is given.
                {
                    MaxConnections = mx;
                }
                else
                {
                    _log.Warning("Value for Max Number of Connection not given, setting to 16777214");
                    MaxConnections = 16777214;
                }

                var pd = Convert.ToDouble(actorConfig.GetString("Percent-Difference", "0.20"));
                if (pd < 1 && pd > 0)
                {
                    PercentDifference = pd;
                }
                else
                {
                    _log.Warning("Percent Difference value not between 0 and 1, setting to 0.20");
                    PercentDifference = 0.20;
                }

                var md = Convert.ToDouble(actorConfig.GetString("Max-Difference", "0.25"));
                if (md < 1 && md > 0)
                {
                    MaxDifference = md;
                }
                else
                {
                    _log.Warning("Max Difference value not between 0 and 1, setting to 0.25");
                    MaxDifference = 0.25;
                }

                var ls = Convert.ToInt32(actorConfig.GetString("Large-Sample", "120"));
                if (ls > 2)
                {
                    LargeSample = ls;
                }
                else
                {
                    _log.Warning(
                        "Large Sample must be greater than 2, setting to 120"); //120 sets a sample size of 1 minute when readings are done every 500 milliseconds
                    LargeSample = 120;
                }

                var ss = Convert.ToInt32(actorConfig.GetString("Small-Sample",
                    "20")); //20 sets a sample size of 10 seconds when readings are done every 500 milliseconds
                if (ss < ls)
                {
                    SmallSample = ss;
                }
                else
                {
                    _log.Warning("Small Sample must be greater than 1 and smaller than Large Sample, setting to 1");
                    SmallSample = 1; //alpha cannot be bigger than 1: Alpa = 2/(samplesize +1)
                }
            }
        }

        /// <summary>
        ///     INTERNAL API - signals <see cref="ActorSystem" /> termination.
        /// </summary>
        public sealed class Shutdown
        {
            public static readonly Shutdown Instance = new Shutdown();

            private Shutdown()
            {
            }
        }

        private sealed class PortLeakReason : CoordinatedShutdown.Reason
        {
            public static readonly PortLeakReason Instance = new PortLeakReason();

            private PortLeakReason()
            {
            }

            public override string ToString()
            {
                return "Port exhaustion threshold breached";
            }
        }
    }
}
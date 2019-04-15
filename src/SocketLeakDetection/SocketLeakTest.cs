using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static SocketLeakDetection.Messages;

namespace SocketLeakDetection
{
    public class SocketLeakTest: UntypedActor
    {
        private readonly double alphaL; 
        private readonly double alphaS;
        private readonly double _percDif; // Percent difference between Large Sample and Small Sample.
        private readonly double _maxDif; // Maximum set percent difference between Large Sample and Small Sample. 
        private IActorRef _supervisor;
        private ITcpCounter _tCounter; //TCP counter set to be used.
        private double pValueL; // Past Average for Large Sample
        private double cValueL; // Current Average for Large Sample
        private double pValueS; // Past Average for Small Sample
        private double cValueS; // Current Average for Small Sample
        private bool timerFlag = false; // Flag to signal increase in TCP connections
        private long _maxCon = 16777214; // Theoretical max value for TCP connection https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2003/cc758980(v=ws.10)


        /// <summary>
        /// Constructor will setup the values that we will need to determine if we need to message our supervisor actor in case we experience an increase in TCP connections
        /// </summary>
        /// <param name="maxCon">Maximum number of connections allowed</param>
        /// <param name="perDif">Percent difference we set to send warning. Value can be between 0-1</param>
        /// <param name="maxDif">Max percent difference allowed before we message the supervisor actor for temination</param>
        /// <param name="largeSample">The sample size we want to use for the large sample EMWA</param>
        /// <param name="smallSample">The sample size we want to use for the small sample EMWA</param>
        /// <param name="counter">TCP counter class we want to use to determine the number of opened TCP connections</param>
        /// <param name="Supervisor">Actor Reference for the Supervisor Actor in charge of terminating Actor System</param>
        public SocketLeakTest(long maxCon, double perDif,double maxDif, int largeSample, int smallSample, ITcpCounter counter, IActorRef Supervisor)
        {
            _maxCon = maxCon;
            _supervisor = Supervisor;
            _percDif = perDif;
            _maxDif = maxDif;
            _tCounter = counter;
            //A commonly used value for alpa is alpha = 2/(N+1). This is because the weights of an SMA and EMA have the same "center of mass"  when alpa(rma)=2/(N(rma)+1)
            //https://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
            alphaL = 2.0 / (largeSample + 1); // Large sample size will be less responseive to changes to average. 
            alphaS = 2.0 / (smallSample + 1); // Small sample size will be more response to changes to average. 
            pValueL = counter.GetTcpCount(); // Set initial value for Large Sample Weighted Average.  
            pValueS = counter.GetTcpCount(); // Set initial value for Small Sample Weighted Average.
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0), TimeSpan.FromMilliseconds(500), Self, counter, ActorRefs.NoSender); //Schedule TCP counts to happen every 500 ms.
            
        }

        /// <summary>
        /// Called by the scheduler every 500 milliseconds to compute the difference between a large sample EMWA vs a small sample EMWA. 
        /// A small smaple EMWA will be more responsive to change. 
        /// </summary>
        /// <param name="message">Message to signal for a comparison to be done, or to signal a constant increase has been observed for 60 seconds</param>
        protected override void OnReceive(object message)
        {
            double dif;
            var count = _tCounter.GetTcpCount(); //Get TCP count

            if (message is TcpCount)
            {
                cValueL = EMWA(alphaL, pValueL, count);
                pValueL = cValueL;
                cValueS = EMWA(alphaS, pValueS, count);
                pValueS = cValueS;

                if (cValueL > 0  && cValueL<cValueS)
                {
                    dif = (cValueS / cValueL) - 1.0;  // Get percent difference between the latest large sample EMWA vs small Sample EMWA.
                    if (dif > _maxDif || count>_maxCon)  // Send termination message if max number of connections reached or max difference is exceeded. 
                        _supervisor.Tell(new Stat { CurretStatus = 2 });

                    else if (dif > 0 && dif > _percDif) // If difference is below Max difference but above warning level inform supervisor. 
                    {
                        _supervisor.Tell(new Stat { CurretStatus = 1 });
                        if (!timerFlag)
                        {
                            // Begin timer, if percent difference above warning level start timer to signal if this increase continues
                            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(60), Self, new TimerExpired(), ActorRefs.NoSender);
                            timerFlag = true; //Turn timer Flag on. 
                        }
                    }
                    else if (timerFlag && dif < _percDif)
                    { // TBD- need to add cancel token for timer. 
                        timerFlag = false;
                    }

                }
            }
            if(message is TimerExpired)
            {
                _supervisor.Tell(new Stat { CurretStatus = 2 }); // Signals that an increase has been observed for 60 seconds and we should terminate. 
            }
        }
        /// <summary>
        /// Calculates the exponential moving average for a data point. The equation only cares about the previously computed Exponentially Weighted Moving Average. 
        /// </summary>
        /// <param name="alpha">Degree of weighting decrease, a constant smoothing factor between 0 and 1</param>
        /// <param name="pvalue">Previously computed Exponential Weighted Moving Average</param>
        /// <param name="xn">Current Measurement</param>
        /// <returns></returns>

        public double EMWA(double alpha, double pvalue, int xn )
        {
        
            return  (alpha * xn) + (1 - alpha) * pvalue;
        }
        
        
    }
}

using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using System;
using System.IO;
using static SocketLeakDetection.Messages;

namespace SocketLeakDetection
{
    public class Supervisor : ReceiveActor
    {

        protected ILoggingAdapter Log = Context.GetLogger();

        /// <summary>
        /// Supervisor actor used to determine if we need to log warning about increase in TCP connections or terminate the Actor System. 
        /// </summary>
        /// <param name="System">Actor System we are monitoring</param>
        /// <param name="config">Configuration used to setup the actor and the PercentDifference parameters</param>
        public Supervisor(ActorSystem System, Config config, ITcpCounter counter)
        {
            GetConfig(config);
            var settings = new SocketLeakDetectorSettings();

            System.ActorOf(Props.Create(() => new TcpPortMonitoringActor(Self,settings,System)));
            Receive<Stat>(s =>
            {
                if (s.CurretStatus == 2) // Status of 2 signal we need to Teminate Actor system as we have reached either the max number of TCP connections
                {                        // Or the increase has been above the warning level for more than 60 seconds. 
                    Log.Error("ActorSystem Terminated due to increase in TCP connections");
                    System.Terminate();
                }
                else if (s.CurretStatus == 1) // Increase of TCP connections is above the set PercentDifference. 
                {
                    Log.Warning("TCP Connection increase Warning");
                }

            });
        }

        /// <summary>
        /// Sets configuration variables for Percent Difference actor. 
        /// </summary>
        /// <param name="config"></param>
        private void GetConfig(Config config)
        {
           
            var actorConfig = config.GetConfig("SLD");
            if (actorConfig != null)
            {
                var mx = Convert.ToInt64(actorConfig.GetString("Max-Connections", "16777214")); // Set value to theoretical max TCP connections for Windows Server 2003 https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2003/cc758980(v=ws.10)
                if (mx>0)                                                                       // If no value is given.
                    MaxConnections = mx;
                else
                {
                    Log.Warning("Value for Max Number of Connection not given, setting to 16777214");
                    MaxConnections = 16777214;
                }

                var pd = Convert.ToDouble(actorConfig.GetString("Percent-Difference", "0.20"));
                if (pd < 1 && pd > 0)
                    PercentDifference = pd;
                else
                {
                    Log.Warning("Percent Difference value not between 0 and 1, setting to 0.20");
                    PercentDifference = 0.20;
                }

                var md = Convert.ToDouble(actorConfig.GetString("Max-Difference", "0.25"));
                if (md < 1 && md > 0)
                    MaxDifference = md;
                else
                {
                    Log.Warning("Max Difference value not between 0 and 1, setting to 0.25");
                    MaxDifference = 0.25;
                }

                var ls = Convert.ToInt32(actorConfig.GetString("Large-Sample", "120"));
                if (ls > 2)
                    LargeSample = ls;
                else
                {
                    Log.Warning("Large Sample must be greater than 2, setting to 120"); //120 sets a sample size of 1 minute when readings are done every 500 milliseconds
                    LargeSample = 120;
                }

                var ss = Convert.ToInt32(actorConfig.GetString("Small-Sample", "20")); //20 sets a sample size of 10 seconds when readings are done every 500 milliseconds
                if (ss < ls)
                    SmallSample = ss;
                else
                {
                    Log.Warning("Small Sample must be greater than 1 and smaller than Large Sample, setting to 1");
                    SmallSample = 1; //alpha cannot be bigger than 1: Alpa = 2/(samplesize +1)
                }

            }
        }
        private long MaxConnections { get; set; }
        private double PercentDifference { get; set; }
        private double MaxDifference { get; set; }
        private int LargeSample { get; set; }
        private int SmallSample { get; set; }

    }
}

// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;

namespace SocketLeakDetection.Simple.Demo
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            /*
             * Simple demo illustrating the monitoring of local TCP endpoints
             * that are currently running on the system somewhere. Shuts itself down
             * if a port leak is detected.
             */
            var actorSystem = ActorSystem.Create("PortDetector", "akka.loglevel = DEBUG");
            var supervisor = actorSystem.ActorOf(Props.Create(() => new TcpPortUseSupervisor()), "tcpPorts");
            actorSystem.WhenTerminated.Wait();
        }
    }
}
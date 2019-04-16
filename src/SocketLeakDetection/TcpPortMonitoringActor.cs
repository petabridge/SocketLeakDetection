// -----------------------------------------------------------------------
// <copyright file="TcpPortMonitoringActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Akka.Actor;

namespace SocketLeakDetection
{
    /// <summary>
    ///     Used to count the number of ports for a specific IPAddress
    /// </summary>
    public sealed class TcpCount
    {
        public TcpCount(IPAddress hostInterface, int currentPortCount)
        {
            HostInterface = hostInterface;
            CurrentPortCount = currentPortCount;
        }

        public IPAddress HostInterface { get; }

        public int CurrentPortCount { get; }
    }

    /// <summary>
    ///     Used to aggregate information about local ports in-use on the current system,
    ///     aggregated by each distinct network interface.
    /// </summary>
    public class TcpPortMonitoringActor : UntypedActor
    {
        private readonly SocketLeakDetectorSettings _settings;

        private readonly IActorRef _supervisor;
        private ICancelable _tcpPortCheck;

        public TcpPortMonitoringActor(IActorRef supervisor, SocketLeakDetectorSettings settings)
        {
            _supervisor = supervisor;
            _settings = settings;
        }

        protected override void PreStart()
        {
            _tcpPortCheck = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(_settings.PortCheckInterval,
                _settings.PortCheckInterval, Self, CheckPorts.Instance, ActorRefs.NoSender);
        }

        protected override void PostStop()
        {
            _tcpPortCheck.Cancel();
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case CheckPorts _:
                    CollectPortData();
                    break;
                default:
                    Unhandled(message);
                    break;
            }
        }

        private void CollectPortData()
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            //IPEndPoint[] iPEndPoints = ipProperties.GetActiveTcpListeners();
            //foreach (var EndPoint in iPEndPoints)
            //{
            //    Networks.Add(EndPoint.Address);
            //}

            var portsPerAddress = ipProperties.GetActiveTcpConnections().GroupBy(x => x.LocalEndPoint.Address)
                .Select(x => new TcpCount(x.Key, x.Count()));

            foreach (var t in portsPerAddress)
                _supervisor.Tell(t);
        }

        /// <summary>
        ///     INTERNAL API - triggers the checking of internal ports
        /// </summary>
        public sealed class CheckPorts
        {
            public static readonly CheckPorts Instance = new CheckPorts();

            private CheckPorts()
            {
            }
        }
    }
}
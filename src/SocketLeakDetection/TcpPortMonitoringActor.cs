using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace SocketLeakDetection
{
    public class TcpPortMonitoringActor : UntypedActor
    {
        private readonly IActorRef _supervisor;
        private readonly SocketLeakDetectorSettings _settings;
        private readonly ActorSystem _system;

        HashSet<IPAddress> Networks = new HashSet<IPAddress>();
        List<IActorRef> DetectorActors = new List<IActorRef>();


        public TcpPortMonitoringActor(IActorRef supervisor, SocketLeakDetectorSettings settings, ActorSystem System)
        {
            _supervisor = supervisor;
            _settings = settings;
            _system = System; 
        }
        protected override void PreStart()
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] iPEndPoints = ipProperties.GetActiveTcpListeners();
            foreach(var EndPoint in iPEndPoints)
            {
                Networks.Add(EndPoint.Address);
            }
            foreach(var address in Networks)
            {
                DetectorActors.Add(_system.ActorOf(Props.Create(() => new SocketLeakDetectorActor(_settings, new TcpCounter(address), _supervisor))));
            }
        }
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException();
        }
    }
}

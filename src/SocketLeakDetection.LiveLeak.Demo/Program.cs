using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Petabridge.Cmd.Host;

namespace SocketLeakDetection.LiveLeak.Demo
{
    class Program
    {
        public class TcpPortActor : ReceiveActor
        {
            private readonly ILoggingAdapter _log = Context.GetLogger();
            private readonly EndPoint _hostEp;
            private ICancelable _connectTask;

            private sealed class DoConnect
            {
                public static readonly DoConnect Instance = new DoConnect();
                private DoConnect() { }
            }

            public TcpPortActor(EndPoint hostEp)
            {
                _hostEp = hostEp;

                Receive<DoConnect>(_ =>
                {
                    Context.System.Tcp().Tell(new Tcp.Connect(_hostEp, timeout: TimeSpan.FromSeconds(5)));
                });

                Receive<Tcp.Connected>(c =>
                {
                    _log.Info("Now connected to [{0}->{1}]", c.LocalAddress, c.RemoteAddress);
                    Sender.Tell(new Tcp.Register(Self));
                });
            }

            protected override void PreStart()
            {
                _connectTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(250), Self, DoConnect.Instance, ActorRefs.NoSender);
            }

            protected override void PostStop()
            {
                _connectTask.Cancel();
            }
        }

        public class TcpHostActor : ReceiveActor, IWithUnboundedStash
        {
            public sealed class GetInboundEndpoint
            {
                public static GetInboundEndpoint Instance = new GetInboundEndpoint();

                private GetInboundEndpoint()
                {
                }
            }

            private readonly ILoggingAdapter _log = Context.GetLogger();
            private IActorRef _host;
            private EndPoint _boundAddress;

            public TcpHostActor()
            {
                Binding();    
            }

            private void Binding()
            {
                Receive<Tcp.Bound>(bound =>
                {
                    _host = Sender;
                    _boundAddress = bound.LocalAddress;
                    _log.Info("host bound to [{0}]", bound.LocalAddress);

                    Stash.UnstashAll();
                    Become(Bound);
                });

                Receive<Tcp.CommandFailed>(f => f.Cmd is Tcp.Bind, f =>
                {
                    var b = (Tcp.Bind)f.Cmd;
                    _log.Error("FATAL. host failed to bind to [{0}]. Failure: [{1}]. Shutting down...",
                        b.LocalAddress, f.Cmd.FailureMessage);
                    Context.System.Terminate();
                });

                ReceiveAny(_ => Stash.Stash());
            }

            private void Bound()
            {
                Receive<Tcp.Connected>(conn =>
                {
                    _log.Info("Port  from client [{0}]", conn.RemoteAddress);
                    var port = Sender;
                    port.Tell(new Tcp.Register(Self, true), Self);
                });

                Receive<GetInboundEndpoint>(_ => Sender.Tell(_boundAddress));
            }

            public IStash Stash { get; set; }

            protected override void PreStart()
            {
                Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Loopback, 0)));
            }

            protected override void PostStop()
            {
                try
                {
                    if (_host != null)
                    {
                        _log.Info("Shutting down host...");
                        var shutdownTimeout = TimeSpan.FromSeconds(3);

                        var unbindTask = _host.Ask<Tcp.Unbound>(Tcp.Unbind.Instance, shutdownTimeout);
                        if (!unbindTask.Wait(shutdownTimeout))
                            _log.Warning("Failed to shut down host after {0}", shutdownTimeout);
                    }
                }
                catch
                {

                }
            }
        }

        static async Task Main(string[] args)
        {
            /*
             * Simple demo illustrating the monitoring of local TCP endpoints
             * that are currently running on the system somewhere. Shuts itself down
             * if a port leak is detected.
             */
            var actorSystem = ActorSystem.Create("PortDetector", "akka.loglevel = DEBUG");
            var supervisor = actorSystem.ActorOf(Props.Create(() => new TcpPortUseSupervisor()), "tcpPorts");

            var cmd = PetabridgeCmd.Get(actorSystem);
            cmd.Start();


            var tcp = actorSystem.ActorOf(Props.Create(() => new TcpHostActor()), "tcp");
            var endpoint = await tcp.Ask<EndPoint>(TcpHostActor.GetInboundEndpoint.Instance, TimeSpan.FromSeconds(3));
            var spawner = actorSystem.ActorOf(Props.Create(() => new TcpPortActor(endpoint)), "leaker");

            await actorSystem.WhenTerminated;
        }
    }
}

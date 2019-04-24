using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Event;
using Akka.Remote;
using Akka.Routing;
using Akka.Util.Internal;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;

namespace SocketLeakDetection.ClusterQuarantine.Demo
{
    public class SilenceActor : ReceiveActor
    {
        public SilenceActor()
        {
            ReceiveAny(o =>
            {

            });
        }
    }

    public class AssociationProfiler : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public AssociationProfiler()
        {
            Receive<AssociatedEvent>(q =>
            {
                // add entry to table - log it
                
            });

            Receive<DisassociatedEvent>(q =>
            {
                // remove matching entry from table - log it
            });
        }


        protected override void PreStart()
        {
            Context.System.EventStream.Subscribe(Self, typeof(AssociatedEvent));
            Context.System.EventStream.Subscribe(Self, typeof(DisassociatedEvent));
        }
    }

    public class QuarantineDetector : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public QuarantineDetector()
        {
            Receive<ThisActorSystemQuarantinedEvent>(q =>
            {
                _log.Warning("I [{0}] have been quarantined by [{1}]", q.LocalAddress, q.RemoteAddress);
            });

            Receive<QuarantinedEvent>(q =>
            {
                _log.Warning("I [{0}] quarantined [{1}]", Cluster.Get(Context.System).SelfAddress, q.Address);
            });
        }

        protected override void PreStart()
        {
            Context.System.EventStream.Subscribe(Self, typeof(ThisActorSystemQuarantinedEvent));
            Context.System.EventStream.Subscribe(Self, typeof(QuarantinedEvent));
        }
    }

    class Program
    {
        private static Config ClusterConfig(int portNumber)
        {
            return @"
                akka.actor.provider = cluster
                akka.remote.dot-netty.tcp.port = """+ portNumber + @"""
                akka.remote.dot-netty.tcp.hostname = 127.0.0.1
                akka.actor.deployment{
                    /random {
			            router = random-group
			            routees.paths = [""/user/silence""]
			            cluster{
				            enabled = on
				            allow-local-routees = off
			            }
		            }
                }
                akka.cluster.seed-nodes = [""akka.tcp://quarantine-test@127.0.0.1:9444""]
            ";
        }

        public static ActorSystem StartNode(int portNumber)
        {
            var node = ActorSystem.Create("quarantine-test", ClusterConfig(portNumber));
            var silence = node.ActorOf(Props.Create(() => new SilenceActor()), "silence");
            var router = node.ActorOf(Props.Empty.WithRouter(FromConfig.Instance), "random");
            node.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), router, "hit", ActorRefs.NoSender);
            var q = node.ActorOf(Props.Create(() => new QuarantineDetector()), "q");
            return node;
        }

        static async Task Main(string[] args)
        {
            // launch seed node
            var seed = StartNode(9444);
            var pbm = PetabridgeCmd.Get(seed);
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.Start();
            var settings = new SocketLeakDetectorSettings(maxPorts:20000);
            var leakDetector = seed.ActorOf(Props.Create(() => new TcpPortUseSupervisor(settings, new[]{ IPAddress.Loopback })), "portMonitor");

            // start node that will be quarantined
            var quarantineNode = StartNode(9555);
            var node2Addr = Cluster.Get(quarantineNode).SelfAddress;
            var uid = AddressUidExtension.Uid(quarantineNode);

            var peanutGallery = Enumerable.Repeat(1, 3).Select(x => StartNode(0)).ToList();

            Func<int, bool> checkMembers = i => Cluster.Get(seed).State.Members.Count == i;

            seed.Log.Info("Waiting for members to join...");
            while (!checkMembers(5))
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            seed.Log.Info("Cluster up.");

            //Console.WriteLine("Press enter to begin quarantine.");
            //Console.ReadLine();
            RarpFor(seed).Quarantine(node2Addr, uid);

            await Task.Delay(TimeSpan.FromSeconds(2.5));
            seed.ActorSelection(new RootActorPath(node2Addr) / "user" / "silence").Tell("fuber");


            //Console.WriteLine("Press enter to terminate quarantined node");
            //Console.ReadLine();
            //await quarantineNode.Terminate();

            seed.WhenTerminated.Wait();
        }

        static RemoteActorRefProvider RarpFor(ActorSystem system)
        {
            return system.AsInstanceOf<ExtendedActorSystem>().Provider.AsInstanceOf<RemoteActorRefProvider>();
        }
    }
}

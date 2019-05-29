using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Akka.Util;
using Xunit;
using Xunit.Abstractions;

namespace SocketLeakDetection.Tests
{
    public class SocketLeakDetectorActorSpecs : TestKit
    {
        private SocketLeakDetectorSettings _settings;

        private static TcpCount GenerateCount(int portCount)
        {
            return new TcpCount(IPAddress.Any, portCount);
        }

        public SocketLeakDetectorActorSpecs(ITestOutputHelper output) : base(output:output, config:"akka.loglevel=DEBUG")
        {
            // keep a really short breach duration in order to make testing more expedient
            _settings = new SocketLeakDetectorSettings(breachDuration:TimeSpan.FromMilliseconds(100), maxPorts:200);
        }

        [Fact(DisplayName = "SocketLeakDetectorActor should send breach signal upon LeakDetector signal")]
        public void SocketLeakDetectorActor_should_trigger_breach()
        {
            var leakDetector = Sys.ActorOf(Props.Create(() => new SocketLeakDetectorActor(_settings, TestActor)));

            EventFilter.Warning(start: "Current port count detected to be").ExpectOne(() =>
            {
                leakDetector.Tell(GenerateCount(210)); //generate count above the acceptable maximum
            });

            ExpectMsg<TcpPortUseSupervisor.Shutdown>();
        }

        [Fact(DisplayName = "SocketLeakDetectorActor should cancel sending breach signal upon LeakDetector returning to normal")]
        public void SocketLeakDetectorActor_should_trigger_and_cancel_breach()
        {
            var leakDetector = Sys.ActorOf(Props.Create(() => new SocketLeakDetectorActor(_settings, TestActor)));

            EventFilter.Warning(start: "Current port count detected to be")
                .And.Warning(start: "Port count back down to ").Expect(2, () =>
            {
                leakDetector.Tell(GenerateCount(210)); //generate count above the acceptable maximum
                leakDetector.Tell(GenerateCount(110)); //generate count below the acceptable maximum
            });

            ExpectNoMsg(500);
        }

        [Fact(DisplayName = "SocketLeakDetectorActor should obey original shutdown timer even if breach is continuous")]
        public void SocketLeakDetectorActor_ContinuousBreach()
        {
            var leakDetector = Sys.ActorOf(Props.Create(() => new SocketLeakDetectorActor(_settings, TestActor)));

            foreach(var i in Enumerable.Range(100, 1000))
                leakDetector.Tell(GenerateCount(i));

            ExpectMsg<TcpPortUseSupervisor.Shutdown>();
        }

        [Fact(DisplayName = "SocketLeakDetectorActor should trigger shutdown solely via EMWA")]
        public void SocketLeakDetectorActor_RollerCoaster()
        {
            var portCounts = new[]
            {
                100, 120, 130, 140, 150, 160, 170, 180, 190, 200, 200, 200, 210, 220, 230, 230, 230, 230, 230
            };
            var newSettings = new SocketLeakDetectorSettings(breachDuration:TimeSpan.FromMilliseconds(100), largeSampleSize:40);
            var leakDetector = Sys.ActorOf(Props.Create(() => new SocketLeakDetectorActor(newSettings, TestActor)));

            foreach (var i in portCounts)
                leakDetector.Tell(GenerateCount(i));

            ExpectMsg<TcpPortUseSupervisor.Shutdown>();
        }
    }
}

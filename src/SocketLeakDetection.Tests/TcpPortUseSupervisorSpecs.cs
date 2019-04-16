// -----------------------------------------------------------------------
// <copyright file="TcpPortUseSupervisorSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace SocketLeakDetection.Tests
{
    public class TcpPortUseSupervisorSpecs : TestKit
    {
        public TcpPortUseSupervisorSpecs(ITestOutputHelper output) : base(output: output)
        {
            // keep a really short breach duration in order to make testing more expedient
            _settings = new SocketLeakDetectorSettings(breachDuration: TimeSpan.FromMilliseconds(100),
                maxPorts: 2, minPorts: 1) {PortCheckInterval = TimeSpan.FromMilliseconds(100)};
        }

        private readonly SocketLeakDetectorSettings _settings;

        [Fact(DisplayName = "TcpPortUseSupervisor should exclude unspecified IPs when given a filter list")]
        public void TcpPortUseSupervisor_should_exclude_IPs_when_not_specified()
        {
            var myIp = IPAddress.IPv6Any;
            var supervisor = Sys.ActorOf(Props.Create(() => new TcpPortUseSupervisor(_settings, new[] {myIp})),
                "supervisor");

            EventFilter.Warning().Expect(0, () =>
            {
                // trigger breach of IP we don't care about
                supervisor.Tell(new TcpCount(IPAddress.Any, 200));
            });
            ExpectNoMsg();
        }

        [Fact(DisplayName = "TcpPortUseSupervisor should include only specified IPs when given a filter list")]
        public void TcpPortUseSupervisor_should_filter_IPs_when_specified()
        {
            var myIp = IPAddress.IPv6Any;
            var supervisor = Sys.ActorOf(Props.Create(() => new TcpPortUseSupervisor(_settings, new[] {myIp})),
                "supervisor");
            ExpectNoMsg();

            Watch(supervisor);

            // trigger breach of the only IP we care about
            supervisor.Tell(new TcpCount(myIp, 200));

            // actor system should be shutting down
            ExpectTerminated(supervisor);
            AwaitCondition(() => Sys.WhenTerminated.IsCompleted);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Xunit;
using Xunit.Abstractions;
using static SocketLeakDetection.Messages;

namespace SocketLeakDetection.Tests
{
    public class PercentDifferenceWarningSpecs : TestKit 

    {

        [Fact(Skip = "Waiting for API stabilization")]
        public void MessageShouldBeSentWhenRiseIsHigh()
        {
            var counter = new FakeCounter(600);
            var Watcher = Sys.ActorOf(Props.Create(() => new SocketLeakDetectorActor(1500,0.1, 0.2, 120, 20, counter, TestActor)));
            for (var i = 0; i < 1000000; i++)
            {
                if (i % 100 == 0)
                    counter.GetTcpCount();
            }
            ExpectMsg<Stat>().CurretStatus.Equals(1);

        }
    }

}

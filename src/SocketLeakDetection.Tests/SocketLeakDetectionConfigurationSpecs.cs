
using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit.Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace SocketLeakDetection.Tests
{
    public class SocketLeakDetectionConfigurationSpecs: TestKit
    {
        [Fact]
        public void Should_Load_Configuration_From_Hocon_File()
        {
            Config testConfig = DefaultConfig.WithFallback(ConfigurationFactory.ParseString(File.ReadAllText("akkaTest.hocon")));
            var System = ActorSystem.Create("test", testConfig);
            var x = System.ActorOf(Props.Create(() => new Supervisor(Sys,testConfig, new FakeCounter())));
            var config = System.Settings.Config.GetConfig("SLD");
            Assert.NotNull(config);
            Assert.Equal("0.3", config.GetString("Percent-Difference"));
            Assert.Equal("0.6", config.GetString("Max-Difference"));
            Assert.Equal("140", config.GetString("Large-Sample"));
            Assert.Equal("30", config.GetString("Small-Sample"));
            
        }
    }
}

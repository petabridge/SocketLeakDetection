using Akka.Actor;
using Akka.Configuration;
using SocketLeakDetection;
using SocketLeakDetection.Tests;
using System;
using System.IO;

namespace FakeCounterSample
{
    class Program
    {
        static void Main(string[] args)
        {
            var Sys = ActorSystem.Create("Test");
            var Config = ConfigurationFactory.ParseString(File.ReadAllText("akka.hocon"));
            var watcher = Sys.ActorOf(Props.Create(() => new Watcher()));
            var sup = Sys.ActorOf(Props.Create(() => new Supervisor(Sys, Config, new FakeCounter(600))));
            Sys.WhenTerminated.Wait();

        }
    }
}

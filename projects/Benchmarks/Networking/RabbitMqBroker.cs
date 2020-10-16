using System;
using System.Threading;
using Ductus.FluentDocker.Builders;

namespace Benchmarks.Networking
{
    public class RabbitMqBroker
    {
        public static IDisposable Start()
        {
            var broker = new Builder().UseContainer()
               .UseImage("rabbitmq")
               .ExposePort(5672, 5672)
               .WaitForPort("5672/tcp", 40000 /*40s*/)
               .ReuseIfExists()
               .WithName("rabbitmq")
               .Build()
               .Start();

            Thread.Sleep(5000); // the broker needs some more time after the port is open to be operational

            return broker;
        }
    }
}

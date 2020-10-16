using System;
using System.Threading;
using Ductus.FluentDocker.Builders;

namespace Benchmarks.Networking
{
    public class RabbitMQBroker
    {
        public static IDisposable Start()
        {
            var broker = new Builder().UseContainer()
               .UseImage("rabbitmq")
               .ExposePort(5672, 5672)
               .WaitForPort("5672/tcp", 40000 /*40s*/)
               .ReuseIfExists()
               .WithName("rabbitmq")
               .WithEnvironment("NODENAME=rabbit1")
               .ExecuteOnRunning("rabbitmqctl --node rabbit1 await_startup ")
               .Build()
               .Start();

            return broker;
        }
    }
}

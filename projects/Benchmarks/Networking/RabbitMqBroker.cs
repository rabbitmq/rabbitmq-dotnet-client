using System;
using System.Diagnostics;
using System.Threading;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Benchmarks.Networking
{
    public class RabbitMQBroker : IDisposable
    {
        private readonly IContainerService _service;

        public static IDisposable Start()
        {
            var broker = new Builder().UseContainer()
               .UseImage("rabbitmq")
               .ExposePort(5672, 5672)
               .WaitForPort("5672/tcp", 40000 /*40s*/)
               .ReuseIfExists()
               .WithName("rabbitmq")
               .WithEnvironment("NODENAME=rabbit1")
               .Build()
               .Start();

            for (int i = 0; i < 10; i++)
            {
                var response = broker.Execute("rabbitmqctl--node rabbit1 await_startup ");
                if (response.Success)
                {
                    break;
                }

                Thread.Sleep(1000);
            }

            return new RabbitMQBroker(broker);
        }

        public RabbitMQBroker(IContainerService service)
        {
            _service = service;
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}

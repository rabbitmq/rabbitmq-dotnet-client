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
               .Wait("rabbitmq",  ReadyProbe )
               .ReuseIfExists()
               .WithName("rabbitmq")
               .WithEnvironment("NODENAME=rabbit1")
               .Build()
               .Start();

            return new RabbitMQBroker(broker);
        }

        private static int ReadyProbe(IContainerService containerService, int arg2)
        {
            var response = containerService.Execute("rabbitmqctl --node rabbit1 await_startup ");
            return response.Success ? 0 : 500;
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

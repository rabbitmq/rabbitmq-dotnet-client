using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Impl
{
    class RandomEndpointSelector : IEndpointSelector
    {
        AmqpTcpEndpoint IEndpointSelector.NextFrom(IList<AmqpTcpEndpoint> options)
        {
            return options.RandomItem();
        }
    }
}

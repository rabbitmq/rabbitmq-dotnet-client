using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabbitMQ.Client
{
    public class QueueDeclareResult
    {
        public string Queue { get; private set; }
        public uint MessageCount { get; private set; }
        public uint ConsumerCount { get; private set; }

        public QueueDeclareResult(string queue, uint messageCount, uint consumerCount)
        {
            this.Queue = queue;
            this.MessageCount = messageCount;
            this.ConsumerCount = consumerCount;
        }
    }
}

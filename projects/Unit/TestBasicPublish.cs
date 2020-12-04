using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBasicPublish
    {
        [Test]
        public async Task TestChannelRoundtripArray()
        {
            var cf = new ConnectionFactory();
            using(IConnection c = cf.CreateConnection())
            await using(IChannel channel = await c.CreateChannelAsync().ConfigureAwait(false))
            {
                var q = await channel.DeclareQueueAsync().ConfigureAwait(false);
                var bp = new BasicProperties();
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(channel);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = await channel.ActivateConsumerAsync(consumer, q.QueueName, true).ConfigureAwait(false);

                await channel.PublishMessageAsync("", q.QueueName, bp, sendBody).ConfigureAwait(false);
                bool waitResFalse = are.WaitOne(2000);
                await channel.CancelConsumerAsync(tag).ConfigureAwait(false);

                Assert.IsTrue(waitResFalse);
                CollectionAssert.AreEqual(sendBody, consumeBody);
            }
        }

        [Test]
        public async Task CanNotModifyPayloadAfterPublish()
        {
            var cf = new ConnectionFactory();
            using(IConnection c = cf.CreateConnection())
            await using(IChannel channel = await c.CreateChannelAsync().ConfigureAwait(false))
            {
                var q = await channel.DeclareQueueAsync().ConfigureAwait(false);
                var bp = new BasicProperties();
                byte[] sendBody = new byte[1000];
                var consumer = new EventingBasicConsumer(channel);
                var are = new AutoResetEvent(false);
                bool modified = true;
                consumer.Received += (o, a) =>
                {
                    if (a.Body.Span.IndexOf((byte)1) < 0)
                    {
                        modified = false;
                    }
                    are.Set();
                };
                string tag = await channel.ActivateConsumerAsync(consumer, q.QueueName, true).ConfigureAwait(false);

                await channel.PublishMessageAsync("", q.QueueName, bp, sendBody).ConfigureAwait(false);
                sendBody.AsSpan().Fill(1);

                Assert.IsTrue(are.WaitOne(2000));
                Assert.IsFalse(modified, "Payload was modified after the return of BasicPublish");

                await channel.CancelConsumerAsync(tag).ConfigureAwait(false);
            }
        }
    }
}

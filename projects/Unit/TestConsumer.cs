using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConsumer
    {
        [Test]
        public async Task TestBasicRoundtripConcurrent()
        {
            var cf = new ConnectionFactory{ ConsumerDispatchConcurrency = 2 };
            using(IConnection c = cf.CreateConnection())
            await using(IChannel channel = await c.CreateChannelAsync().ConfigureAwait(false))
            {
                (string queueName, _, _) = await channel.DeclareQueueAsync().ConfigureAwait(false);
                BasicProperties bp = new BasicProperties();
                const string publish1 = "sync-hi-1";
                byte[] body = Encoding.UTF8.GetBytes(publish1);
                await channel.PublishMessageAsync("", queueName, bp, body).ConfigureAwait(false);
                const string publish2 = "sync-hi-2";
                body = Encoding.UTF8.GetBytes(publish2);
                await channel.PublishMessageAsync("", queueName, bp, body).ConfigureAwait(false);

                var consumer = new EventingBasicConsumer(channel);

                var publish1SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var publish2SyncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var maximumWaitTime = TimeSpan.FromSeconds(5);
                var tokenSource = new CancellationTokenSource(maximumWaitTime);
                tokenSource.Token.Register(() =>
                {
                    publish1SyncSource.TrySetResult(false);
                    publish2SyncSource.TrySetResult(false);
                });

                consumer.Received += (o, a) =>
                {
                    switch (Encoding.UTF8.GetString(a.Body.ToArray()))
                    {
                        case publish1:
                            publish1SyncSource.TrySetResult(true);
                            publish2SyncSource.Task.GetAwaiter().GetResult();
                            break;
                        case publish2:
                            publish2SyncSource.TrySetResult(true);
                            publish1SyncSource.Task.GetAwaiter().GetResult();
                            break;
                    }
                };

                await channel.ActivateConsumerAsync(consumer, queueName, true).ConfigureAwait(false);

                await Task.WhenAll(publish1SyncSource.Task, publish2SyncSource.Task);

                Assert.IsTrue(publish1SyncSource.Task.Result, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
                Assert.IsTrue(publish2SyncSource.Task.Result, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
            }
        }
    }
}

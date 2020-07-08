using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.Events;

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
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
                const string publish1 = "sync-hi-1";
                var body = Encoding.UTF8.GetBytes(publish1);
                m.BasicPublish("", q.QueueName, bp, body);
                const string publish2 = "sync-hi-2";
                body = Encoding.UTF8.GetBytes(publish2);
                m.BasicPublish("", q.QueueName, bp, body);

                var consumer = new EventingBasicConsumer(m);

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

                m.BasicConsume(q.QueueName, true, consumer);

                await Task.WhenAll(publish1SyncSource.Task, publish2SyncSource.Task);

                Assert.IsTrue(publish1SyncSource.Task.Result, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
                Assert.IsTrue(publish2SyncSource.Task.Result, $"Non concurrent dispatch lead to deadlock after {maximumWaitTime}");
            }
        }
    }
}

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Sdk;

namespace RabbitMQ.Client.Unit
{
    public class TestBasicPublish
    {
        [Fact]
        public void TestBasicRoundtripArray()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                var bp = new BasicProperties();
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, bp, sendBody);
                bool waitResFalse = are.WaitOne(5000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void TestBasicRoundtripCachedString()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                CachedString exchangeName = new CachedString(string.Empty);
                CachedString queueName = new CachedString(m.QueueDeclare().QueueName);
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(queueName.Value, true, consumer);

                m.BasicPublish(exchangeName, queueName, sendBody);
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void TestBasicRoundtripReadOnlyMemory()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                byte[] sendBody = System.Text.Encoding.UTF8.GetBytes("hi");
                byte[] consumeBody = null;
                var consumer = new EventingBasicConsumer(m);
                var are = new AutoResetEvent(false);
                consumer.Received += async (o, a) =>
                {
                    consumeBody = a.Body.ToArray();
                    are.Set();
                    await Task.Yield();
                };
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, new ReadOnlyMemory<byte>(sendBody));
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.True(waitResFalse);
                Assert.Equal(sendBody, consumeBody);
            }
        }

        [Fact]
        public void CanNotModifyPayloadAfterPublish()
        {
            var cf = new ConnectionFactory();
            using (IConnection c = cf.CreateConnection())
            using (IChannel m = c.CreateChannel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                byte[] sendBody = new byte[1000];
                var consumer = new EventingBasicConsumer(m);
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
                string tag = m.BasicConsume(q.QueueName, true, consumer);

                m.BasicPublish("", q.QueueName, sendBody);
                sendBody.AsSpan().Fill(1);

                Assert.True(are.WaitOne(2000));
                Assert.False(modified, "Payload was modified after the return of BasicPublish");

                m.BasicCancel(tag);
            }
        }

        [Fact]
        public void TestMaxMessageSize()
        {
            var re = new ManualResetEventSlim();
            const ushort maxMsgSize = 1024;

            int count = 0;
            byte[] msg0 = Encoding.UTF8.GetBytes("hi");

            var r = new System.Random();
            byte[] msg1 = new byte[maxMsgSize * 2];
            r.NextBytes(msg1);

            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.TopologyRecoveryEnabled = false;
            cf.MaxMessageSize = maxMsgSize;

            bool sawConnectionShutdown = false;
            bool sawChannelShutdown = false;
            bool sawConsumerRegistered = false;
            bool sawConsumerCancelled = false;

            using (IConnection c = cf.CreateConnection())
            {
                c.ConnectionShutdown += (o, a) =>
                {
                    sawConnectionShutdown = true;
                };

                Assert.Equal(maxMsgSize, cf.MaxMessageSize);
                Assert.Equal(maxMsgSize, cf.Endpoint.MaxMessageSize);
                Assert.Equal(maxMsgSize, c.Endpoint.MaxMessageSize);

                using (IChannel m = c.CreateChannel())
                {
                    m.ChannelShutdown += (o, a) =>
                    {
                        sawChannelShutdown = true;
                    };

                    m.CallbackException += (o, a) =>
                    {
                        throw new XunitException("Unexpected m.CallbackException");
                    };

                    QueueDeclareOk q = m.QueueDeclare();

                    var consumer = new EventingBasicConsumer(m);

                    consumer.Shutdown += (o, a) =>
                    {
                        re.Set();
                    };

                    consumer.Registered += (o, a) =>
                    {
                        sawConsumerRegistered = true;
                    };

                    consumer.Unregistered += (o, a) =>
                    {
                        throw new XunitException("Unexpected consumer.Unregistered");
                    };

                    consumer.ConsumerCancelled += (o, a) =>
                    {
                        sawConsumerCancelled = true;
                    };

                    consumer.Received += (o, a) =>
                    {
                        Interlocked.Increment(ref count);
                    };

                    string tag = m.BasicConsume(q.QueueName, true, consumer);

                    m.BasicPublish("", q.QueueName, msg0);
                    m.BasicPublish("", q.QueueName, msg1);
                    Assert.True(re.Wait(TimeSpan.FromSeconds(5)));

                    Assert.Equal(1, count);
                    Assert.True(sawConnectionShutdown);
                    Assert.True(sawChannelShutdown);
                    Assert.True(sawConsumerRegistered);
                    Assert.True(sawConsumerCancelled);
                }
            }
        }
    }
}

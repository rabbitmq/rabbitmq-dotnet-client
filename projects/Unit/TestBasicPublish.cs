using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestBasicPublish
    {
        [Test]
        public void TestBasicRoundtripArray()
        {
            var cf = new ConnectionFactory();
            using(IConnection c = cf.CreateConnection())
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
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
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.IsTrue(waitResFalse);
                CollectionAssert.AreEqual(sendBody, consumeBody);
            }
        }

        [Test]
        public void TestBasicRoundtripReadOnlyMemory()
        {
            var cf = new ConnectionFactory();
            using(IConnection c = cf.CreateConnection())
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
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

                m.BasicPublish("", q.QueueName, bp, new ReadOnlyMemory<byte>(sendBody));
                bool waitResFalse = are.WaitOne(2000);
                m.BasicCancel(tag);

                Assert.IsTrue(waitResFalse);
                CollectionAssert.AreEqual(sendBody, consumeBody);
            }
        }

        [Test]
        public void CanNotModifyPayloadAfterPublish()
        {
            var cf = new ConnectionFactory();
            using(IConnection c = cf.CreateConnection())
            using(IModel m = c.CreateModel())
            {
                QueueDeclareOk q = m.QueueDeclare();
                IBasicProperties bp = m.CreateBasicProperties();
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

                m.BasicPublish("", q.QueueName, bp, sendBody);
                sendBody.AsSpan().Fill(1);

                Assert.IsTrue(are.WaitOne(2000));
                Assert.IsFalse(modified, "Payload was modified after the return of BasicPublish");

                m.BasicCancel(tag);
            }
        }

        [Test]
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
            bool sawModelShutdown = false;
            bool sawConsumerRegistered = false;
            bool sawConsumerCancelled = false;

            using (IConnection c = cf.CreateConnection())
            {
                c.ConnectionShutdown += (o, a) =>
                {
                    sawConnectionShutdown= true;
                };

                Assert.AreEqual(maxMsgSize, cf.MaxMessageSize);
                Assert.AreEqual(maxMsgSize, cf.Endpoint.MaxMessageSize);
                Assert.AreEqual(maxMsgSize, c.Endpoint.MaxMessageSize);

                using (IModel m = c.CreateModel())
                {
                    m.ModelShutdown += (o, a) =>
                    {
                        sawModelShutdown= true;
                    };

                    m.CallbackException += (o, a) =>
                    {
                        Assert.Fail("Unexpected m.CallbackException");
                    };

                    QueueDeclareOk q = m.QueueDeclare();
                    IBasicProperties bp = m.CreateBasicProperties();

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
                        Assert.Fail("Unexpected consumer.Unregistered");
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

                    m.BasicPublish("", q.QueueName, bp, msg0);
                    m.BasicPublish("", q.QueueName, bp, msg1);
                    Assert.IsTrue(re.Wait(TimeSpan.FromSeconds(5)));

                    Assert.AreEqual(1, count);
                    Assert.IsTrue(sawConnectionShutdown);
                    Assert.IsTrue(sawModelShutdown);
                    Assert.IsTrue(sawConsumerRegistered);
                    Assert.IsTrue(sawConsumerCancelled);
                }
            }
        }
    }
}

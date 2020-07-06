using System;
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
    }
}

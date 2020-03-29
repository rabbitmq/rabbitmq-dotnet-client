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
                Assert.AreEqual(sendBody, consumeBody);
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
                Assert.AreEqual(sendBody, consumeBody);
            }
        }
    }
}

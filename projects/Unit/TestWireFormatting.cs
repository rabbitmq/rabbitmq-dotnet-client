
using NUnit.Framework;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestWireFormatting
    {
        [TestCase(new byte[] { (byte)'V' }, null)]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 5, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' }, "HELLO")]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 0 }, "")]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 5, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' }, new[] { (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' })]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 0 }, new byte[0])]
        [TestCase(new byte[] { (byte)'t', 1 }, true)]
        [TestCase(new byte[] { (byte)'t', 0 }, false)]
        [TestCase(new byte[] { (byte)'I', 0, 0, 0, 123 }, 123)]
        public void TestFieldWrite(byte[] expected, object value)
        {
            var actual = new byte[expected.Length];
            Assert.AreEqual(expected.Length, WireFormatting.WriteFieldValue(actual, 0, value));
            TestNetworkByteOrderSerialization.Check(actual, expected);
        }

        [TestCase(new byte[] { (byte)'V' }, null)]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 5, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' }, new[] { (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' })]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 0 }, "")]
        [TestCase(new byte[] { (byte)'S', 0, 0, 0, 0 }, new byte[0])]
        [TestCase(new byte[] { (byte)'t', 1 }, true)]
        [TestCase(new byte[] { (byte)'t', 0 }, false)]
        [TestCase(new byte[] { (byte)'I', 0, 0, 0, 123 }, 123)]
        public void TestFieldRead(byte[] actual, object value)
        {
            Assert.AreEqual(WireFormatting.ReadFieldValue(actual, 0, out int read), value);
            Assert.AreEqual(actual.Length, read);
        }
    }
}

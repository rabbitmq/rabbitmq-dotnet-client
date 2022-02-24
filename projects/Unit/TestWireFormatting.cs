using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    internal class TestWireFormatting : WireFormattingFixture
    {
        [TestCase("", 1 + 0)]
        [TestCase("1", 1 + 1)]
        [TestCase("12", 1 + 2)]
        [TestCase("ǽ", 1 + 2, Description = "Latin Small Letter AE With Acute (U+01FD) amounts to 2 bytes")]
        public void TestWriteShortstr_BytesWritten(string inputStr, int expectedBytesWritten)
        {
            byte[] arr = new byte[expectedBytesWritten];
            Assert.AreEqual(expectedBytesWritten, WireFormatting.WriteShortstr(arr, inputStr));
            Assert.AreEqual(expectedBytesWritten - 1, arr[0]);
        }

        [TestCase("12", 0, 0)]
        [TestCase("12", 10, 0)]
        [TestCase("12", 1, 1)]
        [TestCase("12", 10, 1)]
        [TestCase("12", 2, 2)]
        [TestCase("12345", 5, 5)]
        [TestCase("12", 20, 2)]
        [TestCase("12345", 50, 5)]
        [TestCase("ǽ", 2, 2, Description = "Latin Small Letter AE With Acute (U+01FD) amounts to 2 bytes. length byte + 2 bytes should not fit in span of length 2")]
        [TestCase("ǽ", 4, 2, Description = "Latin Small Letter AE With Acute (U+01FD) amounts to 2 bytes. length byte + 2 bytes should not fit in span of length 2")]
        public void TestWriteShortstr_FailsOnSpanLengthViolation(string inputStr, int bufferSize, int spanLength)
        {
            byte[] arr = new byte[bufferSize];
            Assert.Throws<ArgumentOutOfRangeException>(() => WireFormatting.WriteShortstr(arr.AsSpan(0, spanLength), inputStr));

            if (bufferSize > spanLength)
            {
                // Ensure that even though we got an exception, the method never wrote further than the provided span (possible due to unsafe code)
                for (int i = spanLength; i < bufferSize - spanLength; i++)
                {
                    Assert.Zero(arr[i]);
                }
            }
        }

        [TestCase("", 4 + 0)]
        [TestCase("1", 4 + 1)]
        [TestCase("12", 4 + 2)]
        [TestCase("ǽ", 4 + 2, Description = "Latin Small Letter AE With Acute (U+01FD) amounts to 2 bytes")]
        public void TestWriteLongstr_BytesWritten(string inputStr, int expectedBytesWritten)
        {
            byte[] arr = new byte[expectedBytesWritten];
            Assert.AreEqual(expectedBytesWritten, WireFormatting.WriteLongstr(arr, inputStr));

            int expectedLengthWritten = expectedBytesWritten - 4;
            Assert.AreEqual(expectedLengthWritten >> 24 & 0xFF, arr[0]);
            Assert.AreEqual(expectedLengthWritten >> 16 & 0xFF, arr[1]);
            Assert.AreEqual(expectedLengthWritten >> 8 & 0xFF, arr[2]);
            Assert.AreEqual(expectedLengthWritten & 0xFF, arr[3]);
        }

        [Test]
        public void TestWriteLongstr_BytesWritten_VeryLarge()
        {
            TestWriteLongstr_BytesWritten(new string('*', 0x01020304), 4 + 0x01020304);
        }

        [Test]
        public void TestWriteLongstr_BytesWritten_VeryLarge2()
        {
            TestWriteLongstr_BytesWritten(new string('ǽ', 0x01020304), 4 + (0x01020304 * 2));
        }

        [TestCase("12", 0, 0)]
        [TestCase("12", 10, 0)]
        [TestCase("12", 1, 1)]
        [TestCase("12", 10, 1)]
        [TestCase("12", 4, 4)]
        [TestCase("12", 10, 4)]
        [TestCase("12", 5, 5)]
        [TestCase("12345", 5, 5)]
        [TestCase("12", 15, 5)]
        [TestCase("12345", 15, 5)]
        [TestCase("ǽ", 2, 2, Description = "Latin Small Letter AE With Acute (U+01FD) amounts to 2 bytes. 4 length bytes + 2 bytes should not fit in span of length 2")]
        public void TestWriteLongstr_FailsOnSpanLengthViolation(string inputStr, int bufferSize, int spanLength)
        {
            byte[] arr = new byte[bufferSize];
            Assert.Throws<ArgumentOutOfRangeException>(() => WireFormatting.WriteLongstr(arr.AsSpan(0, spanLength), inputStr));

            if (bufferSize > spanLength)
            {
                // Ensure that even though we got an exception, the method never wrote further than the provided span (possible due to unsafe code)
                for (int i = spanLength; i < bufferSize - spanLength; i++)
                {
                    Assert.Zero(arr[i]);
                }
            }
        }

        [TestCaseSource(nameof(GetTestReadShortstrData))]
        public void TestReadShortstr_BytesRead(byte[] inputBuffer, int expectedBytesRead, string expectedString)
        {
            Assert.AreEqual(expectedString, WireFormatting.ReadShortstr(inputBuffer, out int bytesRead));
            Assert.AreEqual(expectedBytesRead, bytesRead);
        }

        private static object[][] GetTestReadShortstrData()
        {
            return new object[][]
            {
                new object[] { new byte[] { 0 }, 1, "" },
                new object[] { new byte[] { 1, (byte)'1' }, 1 + 1, "1" },
                new object[] { new byte[] { 2, (byte)'1', (byte)'2' }, 1 + 2, "12" },
                new object[] { new byte[] { 2, 0xC7, 0xBD }, 1 + 2, "ǽ" } };
        }

        [TestCaseSource(nameof(GetTestReadShortstrFailsOnSpanLengthViolationData))]
        public void TestReadShortstr_FailsOnSpanLengthViolation(byte[] inputBuffer)
        {
            int bytesRead = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => WireFormatting.ReadShortstr(inputBuffer, out bytesRead));
            Assert.AreEqual(0, bytesRead);
        }

        private static object[][] GetTestReadShortstrFailsOnSpanLengthViolationData()
        {
            return new object[][]
            {
                new object[] { new byte[] { 1 } },
                new object[] { new byte[] { 2, (byte)'1' } },
                new object[] { new byte[] { 255 } }
            };
        }

        [TestCaseSource(nameof(GetTestReadLongstrData))]
        public void TestReadLongstr_BytesRead(byte[] inputBuffer, int expectedBytesRead, string expectedString)
        {
            byte[] stringRead = WireFormatting.ReadLongstr(inputBuffer);
            Assert.True(Encoding.UTF8.GetBytes(expectedString).SequenceEqual(stringRead));
            Assert.AreEqual(expectedBytesRead, stringRead.Length + 4);
        }

        private static object[][] GetTestReadLongstrData()
        {
            return new object[][]
            {
                new object[] { new byte[] { 0, 0, 0, 0 }, 4, "" },
                new object[] { new byte[] { 0, 0, 0, 1, (byte)'1' }, 4 + 1, "1" },
                new object[] { new byte[] { 0, 0, 0, 2, (byte)'1', (byte)'2' }, 4 + 2, "12" },
                new object[] { new byte[] { 0, 0, 0, 2, 0xC7, 0xBD }, 4 + 2, "ǽ" },
            };
        }

        [Test]
        public void TestReadLongstr_BytesRead_VeryLarge()
        {
            var str = new string('*', 0x01020304);
            var chars = str.ToCharArray();
            var buffer = new byte[4 + Encoding.UTF8.GetMaxByteCount(chars.Length)];
            buffer[0] = 0x01;
            buffer[1] = 0x02;
            buffer[2] = 0x03;
            buffer[3] = 0x04;
            Encoding.UTF8.GetEncoder().GetBytes(chars, 0, chars.Length, buffer, 4, true);

            TestReadLongstr_BytesRead(buffer, 4 + 0x01020304, str);
        }

        [Test]
        public void TestReadLongstr_BytesRead_VeryLarge2()
        {
            var str = new string('ǽ', 0x01020304);
            var chars = str.ToCharArray();
            var buffer = new byte[4 + Encoding.UTF8.GetMaxByteCount(chars.Length)];
            buffer[0] = 0x02;
            buffer[1] = 0x04;
            buffer[2] = 0x06;
            buffer[3] = 0x08;
            Encoding.UTF8.GetEncoder().GetBytes(chars, 0, chars.Length, buffer, 4, true);

            TestReadLongstr_BytesRead(buffer, 4 + (0x01020304 * 2), str);
        }
    }
}

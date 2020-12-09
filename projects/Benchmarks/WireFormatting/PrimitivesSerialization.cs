using System;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    public class PrimitivesSerialization
    {
        Memory<byte> _decimalBuffer = new byte[8];
        Memory<byte> _longBuffer = new byte[4];
        Memory<byte> _longLongBuffer = new byte[8];
        Memory<byte> _shortBuffer = new byte[2];
        Memory<byte> _timestampBuffer = new byte[8];
        AmqpTimestamp _timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        [GlobalSetup]
        public void Setup()
        {
            WireFormatting.WriteDecimal(_decimalBuffer.Span, 123.45m);
            WireFormatting.WriteLong(_longBuffer.Span, 12345u);
            WireFormatting.WriteLonglong(_longLongBuffer.Span, 12345ul);
            WireFormatting.WriteShort(_shortBuffer.Span, 12345);
            WireFormatting.WriteTimestamp(_timestampBuffer.Span, _timestamp);
        }

        [Benchmark]
        public decimal DecimalRead() => WireFormatting.ReadDecimal(_decimalBuffer.Span);

        [Benchmark]
        public int DecimalWrite() => WireFormatting.WriteDecimal(_decimalBuffer.Span, 123.45m);

        [Benchmark]
        public int LongRead() => WireFormatting.ReadLong(_longBuffer.Span, out _);

        [Benchmark]
        public int LongWrite() => WireFormatting.WriteLong(_longBuffer.Span, 12345u);

        [Benchmark]
        public int LonglongRead() => WireFormatting.ReadLonglong(_longLongBuffer.Span, out _);

        [Benchmark]
        public int LonglongWrite() => WireFormatting.WriteLonglong(_longLongBuffer.Span, 12345ul);

        [Benchmark]
        public int ShortRead() => WireFormatting.ReadShort(_shortBuffer.Span, out _);

        [Benchmark]
        public int ShortWrite() => WireFormatting.WriteShort(_shortBuffer.Span, 12345);

        [Benchmark]
        public int TimestampRead() => WireFormatting.ReadTimestamp(_timestampBuffer.Span, out _);

        [Benchmark]
        public int TimestampWrite() => WireFormatting.WriteTimestamp(_timestampBuffer.Span, _timestamp);
    }
}

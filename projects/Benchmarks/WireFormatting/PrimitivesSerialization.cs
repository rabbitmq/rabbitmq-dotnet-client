using System;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Primitives")]
    public class PrimitivesBase
    {
        protected Memory<byte> _buffer = new byte[16];

        [GlobalSetup]
        public virtual void Setup() { }
    }

    public class PrimitivesDecimal : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteDecimal(_buffer.Span, 123.45m);

        [Benchmark]
        public decimal DecimalRead() => WireFormatting.ReadDecimal(_buffer.Span);

        [Benchmark]
        public int DecimalWrite() => WireFormatting.WriteDecimal(_buffer.Span, 123.45m);
    }

    public class PrimitivesLong : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteLong(_buffer.Span, 12345u);

        [Benchmark]
        public int LongRead() => WireFormatting.ReadLong(_buffer.Span, out _);

        [Benchmark]
        public int LongWrite() => WireFormatting.WriteLong(_buffer.Span, 12345u);
    }

    public class PrimitivesLonglong : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteLonglong(_buffer.Span, 12345ul);

        [Benchmark]
        public int LonglongRead() => WireFormatting.ReadLonglong(_buffer.Span, out _);

        [Benchmark]
        public int LonglongWrite() => WireFormatting.WriteLonglong(_buffer.Span, 12345ul);
    }

    public class PrimitivesShort : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteShort(_buffer.Span, 12345);

        [Benchmark]
        public int ShortRead() => WireFormatting.ReadShort(_buffer.Span, out _);

        [Benchmark]
        public int ShortWrite() => WireFormatting.WriteShort(_buffer.Span, 12345);
    }

    public class PrimitivesTimestamp : PrimitivesBase
    {
        AmqpTimestamp _timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public override void Setup() => WireFormatting.WriteTimestamp(_buffer.Span, _timestamp);

        [Benchmark]
        public int TimestampRead() => WireFormatting.ReadTimestamp(_buffer.Span, out _);

        [Benchmark]
        public int TimestampWrite() => WireFormatting.WriteTimestamp(_buffer.Span, _timestamp);
    }
}

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

    public class PrimitivesBool : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteBits(_buffer.Span, true, false, true, false, true);

        [Benchmark]
        public int BoolRead2() => WireFormatting.ReadBits(_buffer.Span, out bool _, out bool _);

        [Benchmark]
        public int BoolRead5() => WireFormatting.ReadBits(_buffer.Span, out bool _, out bool _, out bool _, out bool _, out bool _);

        [Benchmark]
        [Arguments(true, false)]
        public int BoolWrite2(bool param1, bool param2) => WireFormatting.WriteBits(_buffer.Span, param1, param2);

        [Benchmark]
        [Arguments(true, false)]
        public int BoolWrite5(bool param1, bool param2) => WireFormatting.WriteBits(_buffer.Span, param1, param2, param1, param2, param1);
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
        [Arguments(12345u)]
        public int LongWrite(uint value) => WireFormatting.WriteLong(_buffer.Span, value);
    }

    public class PrimitivesLonglong : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteLonglong(_buffer.Span, 12345ul);

        [Benchmark]
        public int LonglongRead() => WireFormatting.ReadLonglong(_buffer.Span, out _);

        [Benchmark]
        [Arguments(12345ul)]
        public int LonglongWrite(ulong value) => WireFormatting.WriteLonglong(_buffer.Span, value);
    }

    public class PrimitivesShort : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteShort(_buffer.Span, 12345);

        [Benchmark]
        public int ShortRead() => WireFormatting.ReadShort(_buffer.Span, out _);

        [Benchmark]
        [Arguments(12345)]
        public int ShortWrite(ushort value) => WireFormatting.WriteShort(_buffer.Span, value);
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

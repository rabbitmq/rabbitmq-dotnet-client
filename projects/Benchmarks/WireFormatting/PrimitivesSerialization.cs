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
        protected readonly Memory<byte> _buffer = new byte[16];

        [GlobalSetup]
        public virtual void Setup() { }
    }

    public class PrimitivesBool : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteBits(ref _buffer.Span.GetStart(), true, false, true, false, true);

        [Benchmark]
        public (bool, bool) BoolRead2()
        {
            WireFormatting.ReadBits(_buffer.Span, out bool v1, out bool v2);
            return (v1, v2);
        }

        [Benchmark]
        public (bool, bool, bool, bool, bool) BoolRead5()
        {
            WireFormatting.ReadBits(_buffer.Span, out bool v1, out bool v2, out bool v3, out bool v4, out bool v5);
            return (v1, v2, v3, v4, v5);
        }

        [Benchmark]
        [Arguments(true, false)]
        public int BoolWrite2(bool param1, bool param2) => WireFormatting.WriteBits(ref _buffer.Span.GetStart(), param1, param2);

        [Benchmark]
        [Arguments(true, false)]
        public int BoolWrite5(bool param1, bool param2) => WireFormatting.WriteBits(ref _buffer.Span.GetStart(), param1, param2, param1, param2, param1);
    }

    public class PrimitivesDecimal : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteDecimal(ref _buffer.Span.GetStart(), 123.45m);

        [Benchmark]
        public decimal DecimalRead() => WireFormatting.ReadDecimal(_buffer.Span);

        [Benchmark]
        public int DecimalWrite() => WireFormatting.WriteDecimal(ref _buffer.Span.GetStart(), 123.45m);
    }

    public class PrimitivesLong : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteLong(ref _buffer.Span.GetStart(), 12345u);

        [Benchmark]
        public uint LongRead()
        {
            WireFormatting.ReadLong(_buffer.Span, out uint v1);
            return v1;
        }

        [Benchmark]
        [Arguments(12345u)]
        public int LongWrite(uint value) => WireFormatting.WriteLong(ref _buffer.Span.GetStart(), value);
    }

    public class PrimitivesLonglong : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteLonglong(ref _buffer.Span.GetStart(), 12345ul);

        [Benchmark]
        public ulong LonglongRead()
        {
            WireFormatting.ReadLonglong(_buffer.Span, out ulong v1);
            return v1;
        }

        [Benchmark]
        [Arguments(12345ul)]
        public int LonglongWrite(ulong value) => WireFormatting.WriteLonglong(ref _buffer.Span.GetStart(), value);
    }

    public class PrimitivesShort : PrimitivesBase
    {
        public override void Setup() => WireFormatting.WriteShort(ref _buffer.Span.GetStart(), 12345);

        [Benchmark]
        public ushort ShortRead()
        {
            WireFormatting.ReadShort(_buffer.Span, out ushort v1);
            return v1;
        }

        [Benchmark]
        [Arguments(12345)]
        public int ShortWrite(ushort value) => WireFormatting.WriteShort(ref _buffer.Span.GetStart(), value);
    }

    public class PrimitivesTimestamp : PrimitivesBase
    {
        private AmqpTimestamp _timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public override void Setup() => WireFormatting.WriteTimestamp(ref _buffer.Span.GetStart(), _timestamp);

        [Benchmark]
        public AmqpTimestamp TimestampRead()
        {
            WireFormatting.ReadTimestamp(_buffer.Span, out AmqpTimestamp v1);
            return v1;
        }

        [Benchmark]
        public int TimestampWrite() => WireFormatting.WriteTimestamp(ref _buffer.Span.GetStart(), _timestamp);
    }
}

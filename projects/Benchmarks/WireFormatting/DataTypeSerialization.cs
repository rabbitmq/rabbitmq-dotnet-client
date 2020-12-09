using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using BenchmarkDotNet.Attributes;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Benchmarks
{
    [Config(typeof(Config))]
    [BenchmarkCategory("DataTypes")]
    public class DataTypeSerialization
    {
        protected readonly Memory<byte> _buffer = new Memory<byte>(new byte[16384]);
        protected readonly AmqpTimestamp _timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        [GlobalSetup]
        public virtual void SetUp() { }
    }

    public class DateTypeArraySerialization : DataTypeSerialization
    {
        readonly List<object> _emptyArray = new List<object>();
        Memory<byte> _emptyArrayBuffer;
        Memory<byte> _populatedArrayBuffer;
        List<object> _array;

        public override void SetUp()
        {
            _array = new List<object> { "longstring", 1234, 12.34m, _timestamp };
            _emptyArrayBuffer = new byte[WireFormatting.GetArrayByteCount(_emptyArray)];
            WireFormatting.WriteArray(_emptyArrayBuffer.Span, _emptyArray);

            _populatedArrayBuffer = new byte[WireFormatting.GetArrayByteCount(_array)];
            WireFormatting.WriteArray(_populatedArrayBuffer.Span, _array);
        }

        [Benchmark]
        public IList ArrayReadEmpty() => WireFormatting.ReadArray(_emptyArrayBuffer.Span, out _);

        [Benchmark]
        public IList ArrayReadPopulated() => WireFormatting.ReadArray(_populatedArrayBuffer.Span, out _);

        [Benchmark]
        public int ArrayWriteEmpty() => WireFormatting.WriteArray(_buffer.Span, _emptyArray);

        [Benchmark]
        public int ArrayWritePopulated() => WireFormatting.WriteArray(_buffer.Span, _array);
    }

    public class DataTypeTableSerialization : DataTypeSerialization
    {
        IDictionary<string, object> _emptyDict = new Dictionary<string, object>();
        IDictionary<string, object> _populatedDict;
        Memory<byte> _emptyDictionaryBuffer;
        Memory<byte> _populatedDictionaryBuffer;

        public override void SetUp()
        {
            _populatedDict = new Dictionary<string, object>
            {
                { "string", "Hello" },
                { "int", 1234 },
                { "uint", 1234u },
                { "decimal", 12.34m },
                { "timestamp", _timestamp },
                { "fieldtable", new Dictionary<string, object>(){ { "test", "test" } } },
                { "fieldarray", new List<object> { "longstring", 1234, 12.34m, _timestamp } }
            };

            _emptyDictionaryBuffer = new byte[WireFormatting.GetTableByteCount(_emptyDict)];
            WireFormatting.WriteTable(_emptyDictionaryBuffer.Span, _emptyDict);

            _populatedDictionaryBuffer = new byte[WireFormatting.GetTableByteCount(_populatedDict)];
            WireFormatting.WriteTable(_populatedDictionaryBuffer.Span, _populatedDict);
        }

        [Benchmark]
        public int TableReadEmpty() => WireFormatting.ReadDictionary(_emptyDictionaryBuffer.Span, out _);

        [Benchmark]
        public int TableReadPopulated() => WireFormatting.ReadDictionary(_populatedDictionaryBuffer.Span, out _);

        [Benchmark]
        public int TableWriteEmpty() => WireFormatting.WriteTable(_buffer.Span, _emptyDict);

        [Benchmark]
        public int TableWritePopulated() => WireFormatting.WriteTable(_buffer.Span, _populatedDict);
    }

    public class DataTypeLongStringSerialization : DataTypeSerialization
    {
        readonly string _longString = new string('X', 4096);
        readonly Memory<byte> _emptyLongStringBuffer = GenerateLongStringBuffer(string.Empty);
        readonly Memory<byte> _populatedLongStringBuffer = GenerateLongStringBuffer(new string('X', 4096));

        [Benchmark]
        public int LongstrReadEmpty() => WireFormatting.ReadLongstr(_emptyLongStringBuffer.Span, out _);

        [Benchmark]
        public int LongstrReadPopulated() => WireFormatting.ReadLongstr(_populatedLongStringBuffer.Span, out _);

        [Benchmark]
        public int LongstrWriteEmpty() => WireFormatting.WriteLongstr(_buffer.Span, string.Empty);

        [Benchmark]
        public int LongstrWritePopulated() => WireFormatting.WriteLongstr(_buffer.Span, _longString);

        private static byte[] GenerateLongStringBuffer(string val)
        {
            byte[] _buffer = new byte[5 + Encoding.UTF8.GetByteCount(val)];
            WireFormatting.WriteLongstr(_buffer, val);
            return _buffer;
        }
    }

    public class DataTypeShortStringSerialization : DataTypeSerialization
    {
        readonly string _shortString = new string('X', 255);
        readonly Memory<byte> _emptyShortStringBuffer = GenerateStringBuffer(string.Empty);
        readonly Memory<byte> _populatedShortStringBuffer = GenerateStringBuffer(new string('X', 255));

        [Benchmark]
        public int ShortstrReadEmpty() => WireFormatting.ReadShortstr(_emptyShortStringBuffer.Span, out _);

        [Benchmark]
        public int ShortstrReadPopulated() => WireFormatting.ReadShortstr(_populatedShortStringBuffer.Span, out _);

        [Benchmark]
        public int ShortstrWriteEmpty() => WireFormatting.WriteShortstr(_buffer.Span, string.Empty);

        [Benchmark]
        public int ShortstrWritePopulated() => WireFormatting.WriteShortstr(_buffer.Span, _shortString);

        private static byte[] GenerateStringBuffer(string val)
        {
            byte[] _buffer = new byte[2 + Encoding.UTF8.GetByteCount(val)];
            WireFormatting.WriteShortstr(_buffer, val);
            return _buffer;
        }
    }
}

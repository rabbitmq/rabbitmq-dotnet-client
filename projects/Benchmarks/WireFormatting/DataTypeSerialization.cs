﻿using System;
using System.Collections.Generic;

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

    public class DataTypeFieldSerialization : DataTypeSerialization
    {
        private readonly object _intObject = 5;
        private readonly string _shortString = new string('x', 100);
        private readonly byte[] _byteArray = new byte[0];
        private readonly Dictionary<string, object> _emptyDictionary = new Dictionary<string, object>();
        private readonly BinaryTableValue _binaryTableValue = new BinaryTableValue(new byte[0]);

        private Memory<byte> _fieldStringBuffer;
        private Memory<byte> _fieldIntBuffer;
        private Memory<byte> _fieldNullBuffer;
        private Memory<byte> _fieldArrayBuffer;
        private Memory<byte> _fieldDictBuffer;
        private Memory<byte> _fieldBinaryTableValueBuffer;

        public override void SetUp()
        {
            _fieldNullBuffer = new byte[WireFormatting.GetFieldValueByteCount((object)null)];
            WireFormatting.WriteFieldValue(_fieldNullBuffer.Span, 0, null);
            _fieldIntBuffer = new byte[WireFormatting.GetFieldValueByteCount(_intObject)];
            WireFormatting.WriteFieldValue(_fieldIntBuffer.Span, 0, _intObject);
            _fieldStringBuffer = new byte[WireFormatting.GetFieldValueByteCount(_shortString)];
            WireFormatting.WriteFieldValue(_fieldStringBuffer.Span, 0, _shortString);
            _fieldArrayBuffer = new byte[WireFormatting.GetFieldValueByteCount(_byteArray)];
            WireFormatting.WriteFieldValue(_fieldArrayBuffer.Span, 0, _byteArray);
            _fieldDictBuffer = new byte[WireFormatting.GetFieldValueByteCount(_emptyDictionary)];
            WireFormatting.WriteFieldValue(_fieldDictBuffer.Span, 0, _emptyDictionary);
            _fieldBinaryTableValueBuffer = new byte[WireFormatting.GetFieldValueByteCount(_binaryTableValue)];
            WireFormatting.WriteFieldValue(_fieldBinaryTableValueBuffer.Span, 0, _binaryTableValue);
        }

        [Benchmark]
        public object NullRead() => WireFormatting.ReadFieldValue(_fieldNullBuffer.Span, 0, out int _);
        [Benchmark]
        public object IntRead() => WireFormatting.ReadFieldValue(_fieldIntBuffer.Span, 0, out int _);
        [Benchmark]
        public object StringRead() => WireFormatting.ReadFieldValue(_fieldStringBuffer.Span, 0, out int _);
        [Benchmark]
        public object ArrayRead() => WireFormatting.ReadFieldValue(_fieldArrayBuffer.Span, 0, out int _);
        [Benchmark]
        public object DictRead() => WireFormatting.ReadFieldValue(_fieldDictBuffer.Span, 0, out int _);
        [Benchmark]
        public object BinaryTableValueRead() => WireFormatting.ReadFieldValue(_fieldBinaryTableValueBuffer.Span, 0, out int _);

        [Benchmark]
        public int NullWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, null);
        [Benchmark]
        public int IntWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, _intObject);
        [Benchmark]
        public int StringWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, _shortString);
        [Benchmark]
        public int ArrayWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, _byteArray);
        [Benchmark]
        public int DictWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, _emptyDictionary);
        [Benchmark]
        public int BinaryTableValueWrite() => WireFormatting.WriteFieldValue(_buffer.Span, 0, _binaryTableValue);

        [Benchmark]
        public int NullGetSize() => WireFormatting.GetFieldValueByteCount((object)null);
        [Benchmark]
        public int IntGetSize() => WireFormatting.GetFieldValueByteCount(_intObject);
        [Benchmark]
        public int StringGetSize() => WireFormatting.GetFieldValueByteCount(_shortString);
        [Benchmark]
        public int ArrayGetSize() => WireFormatting.GetFieldValueByteCount(_byteArray);
        [Benchmark]
        public int DictGetSize() => WireFormatting.GetFieldValueByteCount(_emptyDictionary);
        [Benchmark]
        public int BinaryTableValueGetSize() => WireFormatting.GetFieldValueByteCount(_binaryTableValue);
    }

    public class DataTypeArraySerialization : DataTypeSerialization
    {
        private readonly List<object> _emptyArray = new List<object>();
        private Memory<byte> _emptyArrayBuffer;
        private Memory<byte> _populatedArrayBuffer;
        private List<object> _array;

        public override void SetUp()
        {
            _array = new List<object> { "longstring", 1234, 12.34m, _timestamp };
            _emptyArrayBuffer = new byte[WireFormatting.GetArrayByteCount(_emptyArray)];
            WireFormatting.WriteArray(_emptyArrayBuffer.Span, 0, _emptyArray);

            _populatedArrayBuffer = new byte[WireFormatting.GetArrayByteCount(_array)];
            WireFormatting.WriteArray(_populatedArrayBuffer.Span, 0, _array);
        }

        [Benchmark]
        public int ArrayReadEmpty() => WireFormatting.ReadArray(_emptyArrayBuffer.Span, 0, out _);

        [Benchmark]
        public int ArrayReadPopulated() => WireFormatting.ReadArray(_populatedArrayBuffer.Span, 0, out _);

        [Benchmark]
        public int ArrayWriteEmpty() => WireFormatting.WriteArray(_buffer.Span, 0, _emptyArray);

        [Benchmark]
        public int ArrayWritePopulated() => WireFormatting.WriteArray(_buffer.Span, 0, _array);

        [Benchmark]
        public int ArrayGetSizeEmpty() => WireFormatting.GetArrayByteCount(_emptyArray);

        [Benchmark]
        public int ArrayGetSizePopulated() => WireFormatting.GetArrayByteCount(_array);
    }

    public class DataTypeTableSerialization : DataTypeSerialization
    {
        private IDictionary<string, object> _emptyDict = new Dictionary<string, object>();
        private IDictionary<string, object> _populatedDict;
        private Memory<byte> _emptyDictionaryBuffer;
        private Memory<byte> _populatedDictionaryBuffer;

        public override void SetUp()
        {
            _populatedDict = new Dictionary<string, object>
            {
                { "string", "Hello" },
                { "int", 1234 },
                { "uint", 1234u },
                { "decimal", 12.34m },
                { "timestamp", _timestamp },
                { "fieldtable", new Dictionary<string, object>{ { "test", "test" } } },
                { "fieldarray", new List<object> { "longstring", 1234, 12.34m, _timestamp } }
            };

            _emptyDictionaryBuffer = new byte[WireFormatting.GetTableByteCount(_emptyDict)];
            WireFormatting.WriteTable(_emptyDictionaryBuffer.Span, 0, _emptyDict);

            _populatedDictionaryBuffer = new byte[WireFormatting.GetTableByteCount(_populatedDict)];
            WireFormatting.WriteTable(_populatedDictionaryBuffer.Span, 0, _populatedDict);
        }

        [Benchmark]
        public int TableReadEmpty() => WireFormatting.ReadDictionary(_emptyDictionaryBuffer.Span, 0, out _);

        [Benchmark]
        public int TableReadPopulated() => WireFormatting.ReadDictionary(_populatedDictionaryBuffer.Span, 0, out _);

        [Benchmark]
        public int TableWriteEmpty() => WireFormatting.WriteTable(_buffer.Span, 0, _emptyDict);

        [Benchmark]
        public int TableWritePopulated() => WireFormatting.WriteTable(_buffer.Span, 0, _populatedDict);

        [Benchmark]
        public int TableGetSizeEmpty() => WireFormatting.GetTableByteCount(_emptyDict);

        [Benchmark]
        public int TableGetSizePopulated() => WireFormatting.GetTableByteCount(_populatedDict);
    }

    public class DataTypeLongStringSerialization : DataTypeSerialization
    {
        private readonly string _longString = new string('X', 4096);
        private readonly Memory<byte> _emptyLongStringBuffer = GenerateLongStringBuffer(string.Empty);
        private readonly Memory<byte> _populatedLongStringBuffer = GenerateLongStringBuffer(new string('X', 4096));

        [Benchmark]
        public int LongstrReadEmpty() => WireFormatting.ReadLongstr(_emptyLongStringBuffer.Span, 0, out _);

        [Benchmark]
        public int LongstrReadPopulated() => WireFormatting.ReadLongstr(_populatedLongStringBuffer.Span, 0, out _);

        [Benchmark]
        public int LongstrWriteEmpty() => WireFormatting.WriteLongstr(_buffer.Span, 0, string.Empty);

        [Benchmark]
        public int LongstrWritePopulated() => WireFormatting.WriteLongstr(_buffer.Span, 0, _longString);

        [Benchmark]
        public int LongstrGetSizeEmpty() => WireFormatting.GetLongstrByteCount(string.Empty);

        [Benchmark]
        public int LongstrGetSizePopulated() => WireFormatting.GetLongstrByteCount(_longString);

        private static byte[] GenerateLongStringBuffer(string val)
        {
            byte[] _buffer = new byte[WireFormatting.GetLongstrByteCount(val)];
            WireFormatting.WriteLongstr(_buffer, 0, val);
            return _buffer;
        }
    }

    public class DataTypeShortStringSerialization : DataTypeSerialization
    {
        private readonly string _shortString = new string('X', 255);
        private readonly Memory<byte> _emptyShortStringBuffer = GenerateStringBuffer(string.Empty);
        private readonly Memory<byte> _populatedShortStringBuffer = GenerateStringBuffer(new string('X', 255));

        [Benchmark]
        public int ShortstrReadEmpty() => WireFormatting.ReadShortstr(_emptyShortStringBuffer.Span, 0, out _);

        [Benchmark]
        public int ShortstrReadPopulated() => WireFormatting.ReadShortstr(_populatedShortStringBuffer.Span, 0, out _);

        [Benchmark]
        public int ShortstrWriteEmpty() => WireFormatting.WriteShortstr(_buffer.Span, 0, string.Empty);

        [Benchmark]
        public int ShortstrWritePopulated() => WireFormatting.WriteShortstr(_buffer.Span, 0, _shortString);

        [Benchmark]
        public int ShortstrGetSizeEmpty() => WireFormatting.GetShortstrByteCount(string.Empty);

        [Benchmark]
        public int ShortstrGetSizePopulated() => WireFormatting.GetShortstrByteCount(_shortString);

        private static byte[] GenerateStringBuffer(string val)
        {
            byte[] _buffer = new byte[WireFormatting.GetShortstrByteCount(val)];
            WireFormatting.WriteShortstr(_buffer, 0, val);
            return _buffer;
        }
    }
}

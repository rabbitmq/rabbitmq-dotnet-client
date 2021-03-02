// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal static class WireFormatting
    {
        public static readonly object TrueBoolean = true;
        public static readonly object FalseBoolean = false;

        // * DESCRIPTION TAKEN FROM MS REFERENCE SOURCE *
        // https://github.com/microsoft/referencesource/blob/master/mscorlib/system/decimal.cs
        // The lo, mid, hi, and flags fields contain the representation of the
        // Decimal value. The lo, mid, and hi fields contain the 96-bit integer
        // part of the Decimal. Bits 0-15 (the lower word) of the flags field are
        // unused and must be zero; bits 16-23 contain must contain a value between
        // 0 and 28, indicating the power of 10 to divide the 96-bit integer part
        // by to produce the Decimal value; bits 24-30 are unused and must be zero;
        // and finally bit 31 indicates the sign of the Decimal value, 0 meaning
        // positive and 1 meaning negative.
        readonly struct DecimalData
        {
            public readonly uint Flags;
            public readonly uint Hi;
            public readonly uint Lo;
            public readonly uint Mid;

            internal DecimalData(uint flags, uint hi, uint lo, uint mid)
            {
                Flags = flags;
                Hi = hi;
                Lo = lo;
                Mid = mid;
            }
        }

        private static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static decimal ReadDecimal(ReadOnlySpan<byte> span, int offset)
        {
            byte scale = span[offset];
            if (scale > 28)
            {
                ThrowInvalidDecimalScale(scale);
            }

            uint unsignedMantissa = NetworkOrderDeserializer.ReadUInt32(span, 1 + offset);
            var data = new DecimalData(((uint)(scale << 16)) | unsignedMantissa & 0x80000000, 0, unsignedMantissa & 0x7FFFFFFF, 0);
            return Unsafe.As<DecimalData, decimal>(ref data);
        }

        public static int ReadArray(ReadOnlySpan<byte> span, int offset, out List<object> result)
        {
            long arrayLength = NetworkOrderDeserializer.ReadUInt32(span, offset);
            if (arrayLength == 0)
            {
                result = null;
                return 4;
            }

            int arrayOffset = offset + 4;
            long arrayEnd = arrayOffset + arrayLength;
            result = new List<object>();
            while (arrayOffset < arrayEnd)
            {
                result.Add(ReadFieldValue(span, arrayOffset, out int fieldValueBytesRead));
                arrayOffset += fieldValueBytesRead;
            }

            return arrayOffset - offset;
        }

        public static object ReadFieldValue(ReadOnlySpan<byte> span, int offset, out int bytesRead)
        {
            char type = (char)span[offset];
            int valueOffset = offset + 1;
            switch (type)
            {
                case 'S':
                    bytesRead = 1 + ReadLongstr(span, valueOffset, out var bytes);
                    return bytes;
                case 't':
                    bytesRead = 2;
                    return span[valueOffset] != 0 ? TrueBoolean : FalseBoolean;
                case 'I':
                    bytesRead = 5;
                    return NetworkOrderDeserializer.ReadInt32(span, valueOffset);
                case 'V':
                    bytesRead = 1;
                    return null;
                default:
                    var val = ReadFieldValueSlow(span, offset, out bytesRead);
                    bytesRead++;
                    return val;
            }

            // Moved out of outer switch to have a shorter main method (improves performance)
            static object ReadFieldValueSlow(ReadOnlySpan<byte> span, int offset, out int bytesRead)
            {
                char type = (char)span[offset++];
                switch (type)
                {
                    case 'F':
                        bytesRead = ReadDictionary(span, offset, out var dictionary);
                        return dictionary;
                    case 'A':
                        bytesRead = ReadArray(span, offset, out List<object> arrayResult);
                        return arrayResult;
                    case 'l':
                        bytesRead = 8;
                        return NetworkOrderDeserializer.ReadInt64(span, offset);
                    case 'i':
                        bytesRead = 4;
                        return NetworkOrderDeserializer.ReadUInt32(span, offset);
                    case 'D':
                        bytesRead = 5;
                        return ReadDecimal(span, offset);
                    case 'B':
                        bytesRead = 1;
                        return span[offset];
                    case 'b':
                        bytesRead = 1;
                        return (sbyte)span[offset];
                    case 'd':
                        bytesRead = 8;
                        return NetworkOrderDeserializer.ReadDouble(span, offset);
                    case 'f':
                        bytesRead = 4;
                        return NetworkOrderDeserializer.ReadSingle(span, offset);
                    case 's':
                        bytesRead = 2;
                        return NetworkOrderDeserializer.ReadInt16(span, offset);
                    case 'T':
                        bytesRead = ReadTimestamp(span, offset, out var timestamp);
                        return timestamp;
                    case 'x':
                        bytesRead = ReadLongstr(span, offset, out var binaryTableResult);
                        return new BinaryTableValue(binaryTableResult);
                    default:
                        bytesRead = 0;
                        return ThrowInvalidTableValue(type);
                }
            }
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadLongstr(ReadOnlySpan<byte> span, int offset, out byte[] value)
        {
            uint byteCount = NetworkOrderDeserializer.ReadUInt32(span, offset);
            if (byteCount == 0)
            {
                value = Array.Empty<byte>();
                return 4;
            }

            if (byteCount > int.MaxValue)
            {
                value = null;
                return ThrowSyntaxErrorException(byteCount);
            }

            value = new byte[byteCount];
            Unsafe.CopyBlock(ref value[0], ref Unsafe.AsRef(span[offset + 4]), byteCount);
            return 4 + value.Length;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadShortstr(ReadOnlySpan<byte> span, int offset, out string value)
        {
            int byteCount = span[offset];
            if (byteCount == 0)
            {
                value = string.Empty;
                return 1;
            }

#if NETCOREAPP
            value = UTF8.GetString(span.Slice(1 + offset, byteCount));
#else

            unsafe
            {
                fixed (byte* val = &span[1 + offset])
                {
                    value = UTF8.GetString(val, byteCount);
                }
            }
#endif

            return 1 + byteCount;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(ReadOnlySpan<byte> span, int offset, out bool val)
        {
            val = (span[offset] & 0b0000_0001) != 0;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(ReadOnlySpan<byte> span, int offset, out bool val1, out bool val2)
        {
            byte bits = span[offset];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(ReadOnlySpan<byte> span, int offset, out bool val1, out bool val2, out bool val3)
        {
            byte bits = span[offset];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(ReadOnlySpan<byte> span, int offset, out bool val1, out bool val2, out bool val3, out bool val4)
        {
            byte bits = span[offset];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            val4 = (bits & 0b0000_1000) != 0;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(ReadOnlySpan<byte> span, int offset, out bool val1, out bool val2, out bool val3, out bool val4, out bool val5)
        {
            byte bits = span[offset];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            val4 = (bits & 0b0000_1000) != 0;
            val5 = (bits & 0b0001_0000) != 0;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadBits(byte first, byte second,
            out bool val1, out bool val2, out bool val3, out bool val4, out bool val5,
            out bool val6, out bool val7, out bool val8, out bool val9, out bool val10,
            out bool val11, out bool val12, out bool val13, out bool val14)
        {
            val1 = (first & 0b1000_0000) != 0;
            val2 = (first & 0b0100_0000) != 0;
            val3 = (first & 0b0010_0000) != 0;
            val4 = (first & 0b0001_0000) != 0;
            val5 = (first & 0b0000_1000) != 0;
            val6 = (first & 0b0000_0100) != 0;
            val7 = (first & 0b0000_0010) != 0;
            val8 = (first & 0b0000_0001) != 0;
            val9 = (second & 0b1000_0000) != 0;
            val10 = (second & 0b0100_0000) != 0;
            val11 = (second & 0b0010_0000) != 0;
            val12 = (second & 0b0001_0000) != 0;
            val13 = (second & 0b0000_1000) != 0;
            val14 = (second & 0b0000_0100) != 0;
            return 2;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadShort(ReadOnlySpan<byte> span, int offset, out ushort value)
        {
            value = NetworkOrderDeserializer.ReadUInt16(span, offset);
            return 2;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadLong(ReadOnlySpan<byte> span, int offset, out uint value)
        {
            value = NetworkOrderDeserializer.ReadUInt32(span, offset);
            return 4;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadLonglong(ReadOnlySpan<byte> span, int offset, out ulong value)
        {
            value = NetworkOrderDeserializer.ReadUInt64(span, offset);
            return 8;
        }

        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.</returns>
        public static int ReadDictionary(ReadOnlySpan<byte> span, int offset, out Dictionary<string, object> valueDictionary)
        {
            long tableLength = NetworkOrderDeserializer.ReadUInt32(span, offset);
            if (tableLength == 0)
            {
                valueDictionary = null;
                return 4;
            }

            int tableIndex = offset + 4;
            long tableEnd = tableIndex + tableLength;
            valueDictionary = new Dictionary<string, object>();
            while (tableIndex < tableEnd)
            {
                tableIndex += ReadShortstr(span, tableIndex, out string key);
                valueDictionary[key] = ReadFieldValue(span, tableIndex, out int valueBytesRead);
                tableIndex += valueBytesRead;
            }

            return tableIndex - offset;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int ReadTimestamp(ReadOnlySpan<byte> span, int offset, out AmqpTimestamp value)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            value = new AmqpTimestamp(NetworkOrderDeserializer.ReadInt64(span, offset));
            return 8;
        }

        public static int WriteArray(Span<byte> span, int offset, IList val)
        {
            if (val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0);
                return 4;
            }

            int arrayOffset = offset + 4;
            if (val is List<object> listVal)
            {
                return WriteArray(span, offset, listVal);
            }

            for (int index = 0; index < val.Count; index++)
            {
                arrayOffset += WriteFieldValue(span, arrayOffset, val[index]);
            }

            int bytesWritten = arrayOffset - offset;
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten - 4);
            return bytesWritten;
        }

        public static int WriteArray(Span<byte> span, int offset, List<object> val)
        {
            if (val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0);
                return 4;
            }

            int arrayOffset = offset + 4;

            for (int index = 0; index < val.Count; index++)
            {
                arrayOffset += WriteFieldValue(span, arrayOffset, val[index]);
            }

            int bytesWritten = arrayOffset - offset;
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten - 4);
            return bytesWritten;
        }

        public static int GetFieldValueByteCount(IList val)
        {
            if (val is List<object> listVal)
            {
                return GetFieldValueByteCount(listVal);
            }

            if (val.Count == 0)
            {
                return 5;
            }

            int byteCount = 5;
            for (int index = 0; index < val.Count; index++)
            {
                byteCount += GetFieldValueByteCount(val[index]);
            }

            return byteCount;
        }

        public static int GetArrayByteCount(List<object> val)
        {
            if (val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;
            for (int index = 0; index < val.Count; index++)
            {
                byteCount += GetFieldValueByteCount(val[index]);
            }

            return byteCount;
        }

        public static int GetFieldValueByteCount(List<object> val)
        {
            if (val.Count == 0)
            {
                return 5;
            }

            int byteCount = 5;
            for (int index = 0; index < val.Count; index++)
            {
                byteCount += GetFieldValueByteCount(val[index]);
            }

            return byteCount;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int GetByteCount(ReadOnlySpan<char> val) => val.IsEmpty ? 0 : UTF8.GetByteCount(val);
#else
        public static int GetByteCount(string val) => string.IsNullOrEmpty(val) ? 0 : UTF8.GetByteCount(val);
#endif

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int GetLongstrByteCount(ReadOnlySpan<char> val) => val.IsEmpty ? 5 : 5 + UTF8.GetByteCount(val);
#else
        public static int GetLongstrByteCount(string val) => string.IsNullOrEmpty(val) ? 5 : 5 + UTF8.GetByteCount(val);
#endif

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int GetShortstrByteCount(ReadOnlySpan<char> val) => val.IsEmpty ? 1 : 1 + UTF8.GetByteCount(val);
#else
        public static int GetShortstrByteCount(string val) => string.IsNullOrEmpty(val) ? 1 : 1 + UTF8.GetByteCount(val);
#endif

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int GetLongstrByteCount(ReadOnlySpan<byte> val) => val.IsEmpty ? 5 : 5 + val.Length;
#else
        public static int GetLongstrByteCount(byte[] val) => val.Length == 0 ? 5 : 5 + val.Length;
#endif

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteDecimal(Span<byte> span, int offset, decimal value)
        {
            // Cast the decimal to our struct to avoid the decimal.GetBits allocations.
            DecimalData data = Unsafe.As<decimal, DecimalData>(ref value);
            // According to the documentation :-
            //  - word 0: low-order "mantissa"
            //  - word 1, word 2: medium- and high-order "mantissa"
            //  - word 3: mostly reserved; "exponent" and sign bit
            // In one way, this is broader than AMQP: the mantissa is larger.
            // In another way, smaller: the exponent ranges 0-28 inclusive.
            // We need to be careful about the range of word 0, too: we can
            // only take 31 bits worth of it, since the sign bit needs to
            // fit in there too.
            if (data.Mid != 0 || // mantissa extends into middle word
                data.Hi != 0 || // mantissa extends into top word
                data.Lo < 0) // mantissa extends beyond 31 bits
            {
                return ThrowWireFormattingException(value);
            }

            span[offset] = (byte)((data.Flags >> 16) & 0xFF);
            NetworkOrderSerializer.WriteUInt32(ref span[1 + offset], (data.Flags & 0b1000_0000_0000_0000_0000_0000_0000_0000) | (data.Lo & 0b0111_1111_1111_1111_1111_1111_1111_1111));
            return 5;
        }

        public static int WriteFieldValue(Span<byte> span, int offset, object value)
        {
            ref var type = ref span[offset];

            if (value is null)
            {
                type = (byte)'V';
                return 1;
            }

            int valueOffset = offset + 1;

            // Order by likelihood of occurrence
            switch (value)
            {
                case string val:
                    type = (byte)'S';
                    return WriteLongstrFieldValue(span, valueOffset, val);
                case bool val:
                    type = (byte)'t';
                    span[valueOffset] = (byte)(val ? 1 : 0);
                    return 2;
                case int val:
                    type = (byte)'I';
                    NetworkOrderSerializer.WriteInt32(ref span[valueOffset], val);
                    return 5;
                case byte[] val:
                    type = (byte)'S';
                    return WriteLongstrFieldValue(span, valueOffset, val);
                default:
                    return 1 + WriteFieldValueSlow(span, offset, value);
            }

            // Moved out of outer switch to have a shorter main method (improves performance)
            static int WriteFieldValueSlow(Span<byte> span, int offset, object value)
            {
                // Order by likelihood of occurrence
                ref var type = ref span[offset++];
                switch (value)
                {
                    case float val:
                        type = (byte)'f';
                        NetworkOrderSerializer.WriteSingle(ref span[offset], val);
                        return 4;
                    case Dictionary<string, object> val:
                        type = (byte)'F';
                        return WriteTable(span, offset, val);
                    case IDictionary<string, object> val:
                        type = (byte)'F';
                        return WriteTable(span, offset, val);
                    case List<object> val:
                        type = (byte)'A';
                        return WriteArray(span, offset, val);
                    case IList val:
                        type = (byte)'A';
                        return WriteArray(span, offset, val);
                    case AmqpTimestamp val:
                        type = (byte)'T';
                        return WriteTimestamp(span, offset, val);
                    case double val:
                        type = (byte)'d';
                        NetworkOrderSerializer.WriteDouble(ref span[offset], val);
                        return 8;
                    case long val:
                        type = (byte)'l';
                        NetworkOrderSerializer.WriteInt64(ref span[offset], val);
                        return 8;
                    case byte val:
                        type = (byte)'B';
                        span[offset] = val;
                        return 1;
                    case sbyte val:
                        type = (byte)'b';
                        span[offset] = (byte)val;
                        return 1;
                    case short val:
                        type = (byte)'s';
                        NetworkOrderSerializer.WriteInt16(ref span[offset], val);
                        return 2;
                    case uint val:
                        type = (byte)'i';
                        NetworkOrderSerializer.WriteUInt32(ref span[offset], val);
                        return 4;
                    case decimal val:
                        type = (byte)'D';
                        return WriteDecimal(span, offset, val);
                    case IDictionary val:
                        type = (byte)'F';
                        return WriteTable(span, offset, val);
                    case BinaryTableValue val:
                        type = (byte)'x';
                        return WriteLongstr(span, offset, val.Bytes);
                    default:
                        return ThrowInvalidTableValue(value);
                }
            }
        }

        public static int GetFieldValueByteCount(object value)
        {
            if (value is null)
            {
                return 1;
            }

            // Order by likelihood of occurrence
            switch (value)
            {
                case bool _:
                case byte _:
                case sbyte _:
                    return 2;
                case short _:
                    return 3;
                case int _:
                case uint _:
                case float _:
                    return 5;
                case decimal _:
                    return 6;
                case double _:
                case long _:
                case AmqpTimestamp _:
                    return 9;
                case string val:
                    return GetLongstrByteCount(val);
                case byte[] val:
                    return GetLongstrByteCount(val);
                case Dictionary<string, object> val:
                    return GetFieldValueByteCount(val);
                case List<object> val:
                    return GetFieldValueByteCount(val);
                case IDictionary<string, object> val:
                    return GetFieldValueByteCount(val);
                case IList val:
                    return GetFieldValueByteCount(val);
                case IDictionary val:
                    return GetFieldValueByteCount(val);
                case BinaryTableValue val:
                    return 5 + val.Bytes.Length;
                default:
                    return ThrowInvalidTableValue(value);
            }
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteLong(Span<byte> span, int offset, uint val)
        {
            NetworkOrderSerializer.WriteUInt32(ref span[offset], val);
            return 4;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteLonglong(Span<byte> span, int offset, ulong val)
        {
            NetworkOrderSerializer.WriteUInt64(ref span[offset], val);
            return 8;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(ref byte destination, bool val)
        {
            destination = (byte)(val ? 1 : 0);
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(ref byte destination, bool val1, bool val2)
        {
            int bits = 0;
            if (val1)
            {
                bits |= 1 << 0;
            }

            if (val2)
            {
                bits |= 1 << 1;
            }

            destination = (byte)bits;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3)
        {
            int bits = 0;
            if (val1)
            {
                bits |= 1 << 0;
            }

            if (val2)
            {
                bits |= 1 << 1;
            }

            if (val3)
            {
                bits |= 1 << 2;
            }

            destination = (byte)bits;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3, bool val4)
        {
            int bits = 0;
            if (val1)
            {
                bits |= 1 << 0;
            }

            if (val2)
            {
                bits |= 1 << 1;
            }

            if (val3)
            {
                bits |= 1 << 2;
            }

            if (val4)
            {
                bits |= 1 << 3;
            }

            destination = (byte)bits;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3, bool val4, bool val5)
        {
            int bits = 0;
            if (val1)
            {
                bits |= 1 << 0;
            }

            if (val2)
            {
                bits |= 1 << 1;
            }

            if (val3)
            {
                bits |= 1 << 2;
            }

            if (val4)
            {
                bits |= 1 << 3;
            }

            if (val5)
            {
                bits |= 1 << 4;
            }

            destination = (byte)bits;
            return 1;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteBits(Span<byte> span, int offset,
            bool val1, bool val2, bool val3, bool val4, bool val5,
            bool val6, bool val7, bool val8, bool val9, bool val10,
            bool val11, bool val12, bool val13, bool val14)
        {
            int bits = 0;
            if (val9)
            {
                bits |= 1 << 7;
            }

            if (val10)
            {
                bits |= 1 << 6;
            }

            if (val11)
            {
                bits |= 1 << 5;
            }

            if (val12)
            {
                bits |= 1 << 4;
            }

            if (val13)
            {
                bits |= 1 << 3;
            }

            if (val14)
            {
                bits |= 1 << 2;
            }

            span[offset + 1] = (byte)bits;

            bits = 0;
            if (val1)
            {
                bits |= 1 << 7;
            }

            if (val2)
            {
                bits |= 1 << 6;
            }

            if (val3)
            {
                bits |= 1 << 5;
            }

            if (val4)
            {
                bits |= 1 << 4;
            }

            if (val5)
            {
                bits |= 1 << 3;
            }

            if (val6)
            {
                bits |= 1 << 2;
            }

            if (val7)
            {
                bits |= 1 << 1;
            }

            if (val8)
            {
                bits |= 1 << 0;
            }

            span[offset] = (byte)bits;
            return 2;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteLongstr(Span<byte> span, int offset, ReadOnlySpan<byte> val)
        {
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)val.Length);
            if (val.Length > 0)
            {
                Unsafe.CopyBlockUnaligned(ref span[offset + 4], ref Unsafe.AsRef(val[0]), (uint)val.Length);
            }

            return 4 + val.Length;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteLongstrFieldValue(Span<byte> span, int offset, ReadOnlySpan<byte> val)
        {
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)val.Length);
            if (val.Length > 0)
            {
                Unsafe.CopyBlockUnaligned(ref span[offset + 4], ref Unsafe.AsRef(val[0]), (uint)val.Length);
            }

            return 5 + val.Length;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteShort(Span<byte> span, int offset, ushort val)
        {
            NetworkOrderSerializer.WriteUInt16(ref span[offset], val);
            return 2;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteShortstr(Span<byte> span, int offset, ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                span[offset] = 0;
                return 1;
            }

            var length = value.Length;
            if (length <= byte.MaxValue)
            {
                span[offset] = (byte)length;
                Unsafe.CopyBlock(ref span[1 + offset], ref Unsafe.AsRef(value[0]), (uint)value.Length);
                return length + 1;
            }

            return ThrowArgumentTooLong(length);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteShortstr(Span<byte> span, int offset, string val)
        {
            int bytesWritten = 0;
            if (!string.IsNullOrEmpty(val))
            {
                int maxLength = (span.Length - offset) - 1;
                if (maxLength > byte.MaxValue)
                {
                    maxLength = byte.MaxValue;
                }
#if NETCOREAPP
                try
                {
                    bytesWritten = UTF8.GetBytes(val, span.Slice(1 + offset, maxLength));
                }
                catch (ArgumentException)
                {
                    return ThrowArgumentOutOfRangeException(val, maxLength);
                }
#else
                unsafe
                {
                    fixed (char* chars = val)
                    {
                        try
                        {
                            fixed (byte* bytes = &span[1 + offset])
                            {
                                bytesWritten = UTF8.GetBytes(chars, val.Length, bytes, maxLength);
                            }
                        }
                        catch (ArgumentException)
                        {
                            return ThrowArgumentOutOfRangeException(val, maxLength);
                        }
                    }
                }
#endif
            }

            span[offset] = (byte)bytesWritten;
            return 1 + bytesWritten;
        }


        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int WriteLongstr(Span<byte> span, int offset, ReadOnlySpan<char> val)
        {
            int bytesWritten = val.IsEmpty ? 0 : UTF8.GetBytes(val, span.Slice(offset + 4));
#else
        public static int WriteLongstr(Span<byte> span, int offset, string val)
        {
            static int GetBytes(Span<byte> span, string val)
            {
                unsafe
                {
                    fixed (char* chars = val)
                    fixed (byte* bytes = span)
                    {
                        return UTF8.GetBytes(chars, val.Length, bytes, span.Length);
                    }
                }
            }

            int bytesWritten = string.IsNullOrEmpty(val) ? 0 : GetBytes(span.Slice(offset + 4), val);
#endif
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten);
            return bytesWritten + 4;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
#if NETCOREAPP
        public static int WriteLongstrFieldValue(Span<byte> span, int offset, ReadOnlySpan<char> val)
        {
            int bytesWritten = val.IsEmpty ? 0 : UTF8.GetBytes(val, span.Slice(offset + 4));
#else
        public static int WriteLongstrFieldValue(Span<byte> span, int offset, string val)
        {
            static int GetBytes(Span<byte> span, string val)
            {
                unsafe
                {
                    fixed (char* chars = val)
                    fixed (byte* bytes = span)
                    {
                        return UTF8.GetBytes(chars, val.Length, bytes, span.Length);
                    }
                }
            }

            int bytesWritten = string.IsNullOrEmpty(val) ? 0 : GetBytes(span.Slice(offset + 4), val);
#endif
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten);
            return 5 + bytesWritten;
        }

        public static int WriteTable(Span<byte> span, int offset, IDictionary val)
        {
            if (val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0u);
                return 4;
            }

            // Let's only write after the length header.
            int dictIndex = offset + 4;
            foreach (DictionaryEntry entry in val)
            {
                dictIndex += WriteShortstr(span, dictIndex, entry.Key.ToString());
                dictIndex += WriteFieldValue(span, dictIndex, entry.Value);
            }

            int bytesWritten = dictIndex - offset;
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten - 4);
            return bytesWritten;
        }

        public static int WriteTable(Span<byte> span, int offset, IDictionary<string, object> val)
        {
            if (val is null)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0u);
                return 4;
            }

            if (val is Dictionary<string, object> dict)
            {
                return WriteTable(span, offset, dict);
            }

            if (val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0u);
                return 4;
            }

            // Let's only write after the length header.
            int dictIndex = offset + 4;
            foreach (KeyValuePair<string, object> entry in val)
            {
                dictIndex += WriteShortstr(span, dictIndex, entry.Key);
                dictIndex += WriteFieldValue(span, dictIndex, entry.Value);
            }

            int bytesWritten = dictIndex - offset;
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten - 4);
            return bytesWritten;
        }

        public static int WriteTable(Span<byte> span, int offset, Dictionary<string, object> val)
        {
            if (val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref span[offset], 0u);
                return 4;
            }

            // Let's only write after the length header.
            int dictIndex = offset + 4;
            foreach (KeyValuePair<string, object> entry in val)
            {
                dictIndex += WriteShortstr(span, dictIndex, entry.Key);
                dictIndex += WriteFieldValue(span, dictIndex, entry.Value);
            }

            int bytesWritten = dictIndex - offset;
            NetworkOrderSerializer.WriteUInt32(ref span[offset], (uint)bytesWritten - 4);
            return bytesWritten;
        }

        public static int GetTableByteCount(IDictionary val)
        {
            int byteCount = 4;

            foreach (DictionaryEntry entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key.ToString()) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetFieldValueByteCount(IDictionary val)
        {
            int byteCount = 5;

            foreach (DictionaryEntry entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key.ToString()) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetTableByteCount(IDictionary<string, object> val)
        {
            if (val is null)
            {
                return 4;
            }

            if (val is Dictionary<string, object> dict)
            {
                return GetTableByteCount(dict);
            }

            if (val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;

            foreach (KeyValuePair<string, object> entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetTableByteCount(Dictionary<string, object> val)
        {
            if (val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;

            foreach (KeyValuePair<string, object> entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetFieldValueByteCount(IDictionary<string, object> val)
        {
            if (val is null)
            {
                return 5;
            }

            if (val is Dictionary<string, object> dict)
            {
                return GetFieldValueByteCount(dict);
            }

            if (val.Count == 0)
            {
                return 5;
            }

            int byteCount = 5;

            foreach (KeyValuePair<string, object> entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetFieldValueByteCount(Dictionary<string, object> val)
        {
            if (val.Count == 0)
            {
                return 5;
            }

            int byteCount = 5;

            foreach (KeyValuePair<string, object> entry in val)
            {
                byteCount += GetShortstrByteCount(entry.Key) + GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static int WriteTimestamp(Span<byte> span, int offset, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            return WriteLonglong(span, offset, (ulong)val.UnixTime);
        }

        public static int ThrowArgumentTooLong(int length)
            => throw new ArgumentOutOfRangeException("value", $"Value exceeds the maximum allowed length of 255 bytes, was {length} long.");

        public static int ThrowArgumentOutOfRangeException(int orig, int expected)
            => throw new ArgumentOutOfRangeException("span", $"Span has not enough space ({orig} instead of {expected})");

        public static int ThrowArgumentOutOfRangeException(string val, int maxLength)
            => throw new ArgumentOutOfRangeException(nameof(val), val, $"Value exceeds the maximum allowed length of {maxLength} bytes.");

        public static int ThrowSyntaxErrorException(uint byteCount)
            => throw new SyntaxErrorException($"Long string too long; byte length={byteCount}, max={int.MaxValue}");

        private static int ThrowWireFormattingException(decimal value)
            => throw new WireFormattingException("Decimal overflow in AMQP encoding", value);

        private static int ThrowInvalidTableValue(char type)
            => throw new SyntaxErrorException($"Unrecognised type in table: {type}");

        private static int ThrowInvalidTableValue(object value)
            => throw new WireFormattingException($"Value of type '{value.GetType().Name}' cannot appear as table value", value);

        private static decimal ThrowInvalidDecimalScale(int scale)
            => throw new SyntaxErrorException($"Unrepresentable AMQP decimal table field: scale={scale}");
    }
}

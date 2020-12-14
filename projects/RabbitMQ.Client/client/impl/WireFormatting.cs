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
using System.Text;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal class WireFormatting
    {
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

        private static UTF8Encoding UTF8 = new UTF8Encoding();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(ReadOnlySpan<byte> span)
        {
            if (scale > 28)
            {
                ThrowInvalidDecimalScale(scale);
            }

            uint unsignedMantissa = NetworkOrderDeserializer.ReadUInt32(span.Slice(1));
            var data = new DecimalData(((uint)(scale << 16)) | unsignedMantissa & 0x80000000, 0, unsignedMantissa & 0x7FFFFFFF, 0);
            return Unsafe.As<DecimalData, decimal>(ref data);
        }

        public static void DecimalToAmqp(decimal value, out byte scale, out int mantissa)
        {
            // According to the documentation :-
            //  - word 0: low-order "mantissa"
            //  - word 1, word 2: medium- and high-order "mantissa"
            //  - word 3: mostly reserved; "exponent" and sign bit
            // In one way, this is broader than AMQP: the mantissa is larger.
            // In another way, smaller: the exponent ranges 0-28 inclusive.
            // We need to be careful about the range of word 0, too: we can
            // only take 31 bits worth of it, since the sign bit needs to
            // fit in there too.
            int[] bitRepresentation = decimal.GetBits(value);
            if (bitRepresentation[1] != 0 || // mantissa extends into middle word
                bitRepresentation[2] != 0 || // mantissa extends into top word
                bitRepresentation[0] < 0) // mantissa extends beyond 31 bits
            {
                throw new WireFormattingException("Decimal overflow in AMQP encoding", value);
            }
            scale = (byte)((((uint)bitRepresentation[3]) >> 16) & 0xFF);
            mantissa = (int)((((uint)bitRepresentation[3]) & 0x80000000) |
                             (((uint)bitRepresentation[0]) & 0x7FFFFFFF));
        }

        public static IList ReadArray(ReadOnlySpan<byte> span, out int bytesRead)
        {
            List<object> array = new List<object>();
            long arrayLength = NetworkOrderDeserializer.ReadUInt32(span);
            bytesRead = 4;
            while (bytesRead - 4 < arrayLength)
            {
                object value = ReadFieldValue(span.Slice(bytesRead), out int fieldValueBytesRead);
                bytesRead += fieldValueBytesRead;
                array.Add(value);
            }

            return array;
        }

        public static decimal ReadDecimal(ReadOnlySpan<byte> span)
        {
            byte scale = span[0];
            uint unsignedMantissa = NetworkOrderDeserializer.ReadUInt32(span.Slice(1));
            return AmqpToDecimal(scale, unsignedMantissa);
        }

        public static object ReadFieldValue(ReadOnlySpan<byte> span, out int bytesRead)
        {
            bytesRead = 1;
            switch ((char)span[0])
            {
                case 'S':
                    byte[] result = ReadLongstr(span.Slice(1));
                    bytesRead += result.Length + 4;
                    return result;
                case 'I':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadInt32(span.Slice(1));
                case 'i':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadUInt32(span.Slice(1));
                case 'D':
                    bytesRead += 5;
                    return ReadDecimal(span.Slice(1));
                case 'T':
                    bytesRead += 8;
                    return ReadTimestamp(span.Slice(1));
                case 'F':
                    Dictionary<string, object> tableResult = ReadTable(span.Slice(1), out int tableBytesRead);
                    bytesRead += tableBytesRead;
                    return tableResult;
                case 'A':
                    IList arrayResult = ReadArray(span.Slice(1), out int arrayBytesRead);
                    bytesRead += arrayBytesRead;
                    return arrayResult;
                case 'B':
                    bytesRead += 1;
                    return span[1];
                case 'b':
                    bytesRead += 1;
                    return (sbyte)span[1];
                case 'd':
                    bytesRead += 8;
                    return NetworkOrderDeserializer.ReadDouble(span.Slice(1));
                case 'f':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadSingle(span.Slice(1));
                case 'l':
                    bytesRead += 8;
                    return NetworkOrderDeserializer.ReadInt64(span.Slice(1));
                case 's':
                    bytesRead += 2;
                    return NetworkOrderDeserializer.ReadInt16(span.Slice(1));
                case 't':
                    bytesRead += 1;
                    return span[1] != 0;
                case 'x':
                    byte[] binaryTableResult = ReadLongstr(span.Slice(1));
                    bytesRead += binaryTableResult.Length + 4;
                    return new BinaryTableValue(binaryTableResult);
                case 'V':
                    return null;
                default:
                    throw new SyntaxErrorException($"Unrecognised type in table: {(char)span[0]}");
            }
        }

        public static byte[] ReadLongstr(ReadOnlySpan<byte> span)
        {
            uint byteCount = NetworkOrderDeserializer.ReadUInt32(span);
            if (byteCount > int.MaxValue)
            {
                throw new SyntaxErrorException($"Long string too long; byte length={byteCount}, max={int.MaxValue}");
            }

            return span.Slice(4, (int)byteCount).ToArray();
        }

        public static unsafe string ReadShortstr(ReadOnlySpan<byte> span, out int bytesRead)
        {
            int byteCount = span[0];
            if (byteCount == 0)
            {
                bytesRead = 1;
                return string.Empty;
            }
            if (span.Length >= byteCount + 1)
            {
                bytesRead = 1 + byteCount;
                fixed (byte* bytes = &span.Slice(1).GetPinnableReference())
                {
                    return Encoding.UTF8.GetString(bytes, byteCount);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(span), $"Span has not enough space ({span.Length} instead of {byteCount + 1})");
        }

        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.</returns>
        public static Dictionary<string, object> ReadTable(ReadOnlySpan<byte> span, out int bytesRead)
        {
            bytesRead = 4;
            long tableLength = NetworkOrderDeserializer.ReadUInt32(span);
            if (tableLength == 0)
            {
                return null;
            }

            Dictionary<string, object> table = new Dictionary<string, object>();
            while ((bytesRead - 4) < tableLength)
            {
                string key = ReadShortstr(span.Slice(bytesRead), out int keyBytesRead);
                bytesRead += keyBytesRead;
                object value = ReadFieldValue(span.Slice(bytesRead), out int valueBytesRead);
                bytesRead += valueBytesRead;

                if (!table.ContainsKey(key))
                {
                    table[key] = value;
                }
            }

            return table;
        }

        public static AmqpTimestamp ReadTimestamp(ReadOnlySpan<byte> span)
        {
            ulong stamp = NetworkOrderDeserializer.ReadUInt64(span);
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            return new AmqpTimestamp((long)stamp);
        }

        public static int WriteArray(Span<byte> span, IList val)
        {
            if (val == null)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0);
                return 4;
            }
            else
            {
                int bytesWritten = 0;
                for (int index = 0; index < val.Count; index++)
                {
                    bytesWritten += WriteFieldValue(span.Slice(4 + bytesWritten), val[index]);
                }

                NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten);
                return 4 + bytesWritten;
            }
        }

        public static int GetArrayByteCount(IList val)
        {
            int byteCount = 4;
            if (val == null)
            {
                return byteCount;
            }

            for (int index = 0; index < val.Count; index++)
            {
                byteCount += GetFieldValueByteCount(val[index]);
            }

            return byteCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP
        public static int GetByteCount(ReadOnlySpan<char> val) => val.IsEmpty ? 0 : UTF8.GetByteCount(val);
#else
        public static int GetByteCount(string val) => string.IsNullOrEmpty(val) ? 0 : UTF8.GetByteCount(val);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteDecimal(Span<byte> span, decimal value)
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

            span[0] = (byte)((data.Flags >> 16) & 0xFF);
            WriteLong(span.Slice(1), (data.Flags & 0b1000_0000_0000_0000_0000_0000_0000_0000) | (data.Lo & 0b0111_1111_1111_1111_1111_1111_1111_1111));
            return 5;
        }

        public static int WriteFieldValue(Span<byte> span, object value)
        {
            if (value == null)
            {
                span[0] = (byte)'V';
                return 1;
            }

            Span<byte> slice = span.Slice(1);
            switch (value)
            {
                case string val:
                    span[0] = (byte)'S';
                    return 1 + WriteLongstr(slice, val);
                case byte[] val:
                    span[0] = (byte)'S';
                    return 1 + WriteLongstr(slice, val);
                case int val:
                    span[0] = (byte)'I';
                    NetworkOrderSerializer.WriteInt32(slice, val);
                    return 5;
                case uint val:
                    span[0] = (byte)'i';
                    NetworkOrderSerializer.WriteUInt32(slice, val);
                    return 5;
                case decimal val:
                    span[0] = (byte)'D';
                    return 1 + WriteDecimal(slice, val);
                case AmqpTimestamp val:
                    span[0] = (byte)'T';
                    return 1 + WriteTimestamp(slice, val);
                case IDictionary val:
                    span[0] = (byte)'F';
                    return 1 + WriteTable(slice, val);
                case IList val:
                    span[0] = (byte)'A';
                    return 1 + WriteArray(slice, val);
                case byte val:
                    span[0] = (byte)'B';
                    span[1] = val;
                    return 2;
                case sbyte val:
                    span[0] = (byte)'b';
                    span[1] = (byte)val;
                    return 2;
                case double val:
                    span[0] = (byte)'d';
                    NetworkOrderSerializer.WriteDouble(slice, val);
                    return 9;
                case float val:
                    span[0] = (byte)'f';
                    NetworkOrderSerializer.WriteSingle(slice, val);
                    return 5;
                case long val:
                    span[0] = (byte)'l';
                    NetworkOrderSerializer.WriteInt64(slice, val);
                    return 9;
                case short val:
                    span[0] = (byte)'s';
                    NetworkOrderSerializer.WriteInt16(slice, val);
                    return 3;
                case bool val:
                    span[0] = (byte)'t';
                    span[1] = (byte)(val ? 1 : 0);
                    return 2;
                case BinaryTableValue val:
                    span[0] = (byte)'x';
                    return 1 + WriteLongstr(slice, val.Bytes);
                default:
                    throw new WireFormattingException($"Value of type '{value.GetType().Name}' cannot appear as table value", value);
            }
        }

        public static int GetFieldValueByteCount(object value)
        {
            switch (value)
            {
                case null:
                    return 1;
                case byte _:
                case sbyte _:
                case bool _:
                    return 2;
                case short _:
                    return 3;
                case int _:
                case uint _:
                case float _:
                    return 5;
                case decimal _:
                    return 6;
                case AmqpTimestamp _:
                case double _:
                case long _:
                    return 9;
                case string val:
                    return 5 + Encoding.UTF8.GetByteCount(val);
                case byte[] val:
                    return 5 + val.Length;
                case IDictionary val:
                    return 1 + GetTableByteCount(val);
                case IList val:
                    return 1 + GetArrayByteCount(val);
                case BinaryTableValue val:
                    return 5 + val.Bytes.Length;
                default:
                    throw new WireFormattingException($"Value of type '{value.GetType().Name}' cannot appear as table value", value);
            }
        }

        public static int WriteLong(Span<byte> span, uint val)
        {
            NetworkOrderSerializer.WriteUInt32(span, val);
            return 4;
        }

        public static int WriteLonglong(Span<byte> span, ulong val)
        {
            NetworkOrderSerializer.WriteUInt64(span, val);
            return 8;
        }

        public static int WriteLongstr(Span<byte> span, ReadOnlySpan<byte> val)
        {
            WriteLong(span, (uint)val.Length);
            val.CopyTo(span.Slice(4));
            return 4 + val.Length;
        }

        public static int WriteShort(Span<byte> span, ushort val)
        {
            NetworkOrderSerializer.WriteUInt16(span, val);
            return 2;
        }

        public static unsafe int WriteShortstr(Span<byte> span, string val)
        {
            int maxLength = span.Length - 1;
            if (maxLength > byte.MaxValue)
            {
                maxLength = byte.MaxValue;
            }
            fixed (char* chars = val)
            fixed (byte* bytes = &span.Slice(1).GetPinnableReference())
            {
                try
                {
                    int bytesWritten = Encoding.UTF8.GetBytes(chars, val.Length, bytes, maxLength);
                    span[0] = (byte)bytesWritten;
                    return bytesWritten + 1;
                }
                catch (ArgumentException)
                {
                    throw new ArgumentOutOfRangeException(nameof(val), val, $"Value exceeds the maximum allowed length of {maxLength} bytes.");
                }
            }
        }

        public static unsafe int WriteLongstr(Span<byte> span, string val)
        {
            fixed (char* chars = val)
            fixed (byte* bytes = &span.Slice(4).GetPinnableReference())
            {
                int bytesWritten = Encoding.UTF8.GetBytes(chars, val.Length, bytes, span.Length);
                NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten);
                return bytesWritten + 4;
            }
        }

        public static int WriteTable(Span<byte> span, IDictionary val)
        {
            if (val == null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0);
                return 4;
            }
            else
            {
                // Let's only write after the length header.
                Span<byte> slice = span.Slice(4);
                int bytesWritten = 0;
                foreach (DictionaryEntry entry in val)
                {
                    bytesWritten += WriteShortstr(slice.Slice(bytesWritten), entry.Key.ToString());
                    bytesWritten += WriteFieldValue(slice.Slice(bytesWritten), entry.Value);
                }

                NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten);
                return 4 + bytesWritten;
            }
        }

        public static int WriteTable(Span<byte> span, IDictionary<string, object> val)
        {
            if (val == null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0);
                return 4;
            }
            else
            {
                // Let's only write after the length header.
                Span<byte> slice = span.Slice(4);
                int bytesWritten = 0;
                if (val is Dictionary<string, object> dict)
                {
                    foreach (KeyValuePair<string, object> entry in dict)
                    {
                        bytesWritten += WriteShortstr(slice.Slice(bytesWritten), entry.Key);
                        bytesWritten += WriteFieldValue(slice.Slice(bytesWritten), entry.Value);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, object> entry in val)
                    {
                        bytesWritten += WriteShortstr(slice.Slice(bytesWritten), entry.Key);
                        bytesWritten += WriteFieldValue(slice.Slice(bytesWritten), entry.Value);
                    }
                }

                NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten);
                return 4 + bytesWritten;
            }
        }

        public static int GetTableByteCount(IDictionary val)
        {
            int byteCount = 4;
            if (val == null)
            {
                return byteCount;
            }

            foreach (DictionaryEntry entry in val)
            {
                byteCount += Encoding.UTF8.GetByteCount(entry.Key.ToString()) + 1;
                byteCount += GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetTableByteCount(IDictionary<string, object> val)
        {
            int byteCount = 4;
            if (val == null)
            {
                return byteCount;
            }

            if (val is Dictionary<string, object> dict)
            {
                foreach (KeyValuePair<string, object> entry in dict)
                {
                    byteCount += Encoding.UTF8.GetByteCount(entry.Key) + 1;
                    byteCount += GetFieldValueByteCount(entry.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> entry in val)
                {
                    byteCount += Encoding.UTF8.GetByteCount(entry.Key) + 1;
                    byteCount += GetFieldValueByteCount(entry.Value);
                }
            }

            return byteCount;
        }

        public static int WriteTimestamp(Span<byte> span, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            return WriteLonglong(span, (ulong)val.UnixTime);
        }

        public static int ThrowArgumentOutOfRangeException(int orig, int expected)
        {
            throw new ArgumentOutOfRangeException("span", $"Span has not enough space ({orig} instead of {expected})");
        }

        public static int ThrowArgumentOutOfRangeException(string val, int maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(val), val, $"Value exceeds the maximum allowed length of {maxLength} bytes.");
        }

        public static int ThrowSyntaxErrorException(uint byteCount)
        {
            throw new SyntaxErrorException($"Long string too long; byte length={byteCount}, max={int.MaxValue}");
        }

        private static int ThrowWireFormattingException(decimal value)
        {
            throw new WireFormattingException("Decimal overflow in AMQP encoding", value);
        }

        private static decimal ThrowInvalidDecimalScale(int scale)
        {
            throw new SyntaxErrorException($"Unrepresentable AMQP decimal table field: scale={scale}");
        }
    }
}

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(ReadOnlySpan<byte> span)
        {
            byte scale = span[0];
            if (scale > 28)
            {
                ThrowInvalidDecimalScale(scale);
            }

            uint unsignedMantissa = NetworkOrderDeserializer.ReadUInt32(span.Slice(1));
            var data = new DecimalData(((uint)(scale << 16)) | unsignedMantissa & 0x80000000, 0, unsignedMantissa & 0x7FFFFFFF, 0);
            return Unsafe.As<DecimalData, decimal>(ref data);
        }

        public static IList ReadArray(ReadOnlySpan<byte> span, out int bytesRead)
        {
            bytesRead = 4;
            long arrayLength = NetworkOrderDeserializer.ReadUInt32(span);
            if (arrayLength == 0)
            {
                return null;
            }
            List<object> array = new List<object>();
            while (bytesRead - 4 < arrayLength)
            {
                array.Add(ReadFieldValue(span.Slice(bytesRead), out int fieldValueBytesRead));
                bytesRead += fieldValueBytesRead;
            }

            return array;
        }

        public static object ReadFieldValue(ReadOnlySpan<byte> span, out int bytesRead)
        {
            switch ((char)span[0])
            {
                case 'S':
                    bytesRead = 1 + ReadLongstr(span.Slice(1), out var bytes);
                    return bytes;
                case 't':
                    bytesRead = 2;
                    return span[1] != 0 ? TrueBoolean : FalseBoolean;
                case 'I':
                    bytesRead = 5;
                    return NetworkOrderDeserializer.ReadInt32(span.Slice(1));
                case 'V':
                    bytesRead = 1;
                    return null;
                default:
                    return ReadFieldValueSlow(span, out bytesRead);
            }

            // Moved out of outer switch to have a shorter main method (improves performance)
            static object ReadFieldValueSlow(ReadOnlySpan<byte> span, out int bytesRead)
            {
                var slice = span.Slice(1);
                switch ((char)span[0])
                {
                    case 'F':
                        bytesRead = 1 + ReadDictionary(slice, out var dictionary);
                        return dictionary;
                    case 'A':
                        IList arrayResult = ReadArray(slice, out int arrayBytesRead);
                        bytesRead = 1 + arrayBytesRead;
                        return arrayResult;
                    case 'l':
                        bytesRead = 9;
                        return NetworkOrderDeserializer.ReadInt64(slice);
                    case 'i':
                        bytesRead = 5;
                        return NetworkOrderDeserializer.ReadUInt32(slice);
                    case 'D':
                        bytesRead = 6;
                        return ReadDecimal(slice);
                    case 'B':
                        bytesRead = 2;
                        return span[1];
                    case 'b':
                        bytesRead = 2;
                        return (sbyte)span[1];
                    case 'd':
                        bytesRead = 9;
                        return NetworkOrderDeserializer.ReadDouble(slice);
                    case 'f':
                        bytesRead = 5;
                        return NetworkOrderDeserializer.ReadSingle(slice);
                    case 's':
                        bytesRead = 3;
                        return NetworkOrderDeserializer.ReadInt16(slice);
                    case 'T':
                        bytesRead = 1 + ReadTimestamp(slice, out var timestamp);
                        return timestamp;
                    case 'x':
                        bytesRead = 1 + ReadLongstr(slice, out var binaryTableResult);
                        return new BinaryTableValue(binaryTableResult);
                    default:
                        bytesRead = 0;
                        return ThrowInvalidTableValue((char)span[0]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadLongstr(ReadOnlySpan<byte> span, out byte[] value)
        {
            uint byteCount = NetworkOrderDeserializer.ReadUInt32(span);
            if (byteCount > int.MaxValue)
            {
                value = null;
                return ThrowSyntaxErrorException(byteCount);
            }

            value = span.Slice(4, (int)byteCount).ToArray();
            return 4 + value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadShortstr(ReadOnlySpan<byte> span, out string value)
        {
            int byteCount = span[0];
            if (byteCount == 0)
            {
                value = string.Empty;
                return 1;
            }

            // equals span.Length >= byteCount + 1
            if (span.Length > byteCount)
            {
#if NETCOREAPP
                value = UTF8.GetString(span.Slice(1, byteCount));
#else
                unsafe
                {
                    fixed (byte* bytes = span.Slice(1))
                    {
                        value = UTF8.GetString(bytes, byteCount);
                    }
                }

#endif
                return 1 + byteCount;
            }

            value = string.Empty;
            return ThrowArgumentOutOfRangeException(span.Length, byteCount + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span, out bool val)
        {
            val = (span[0] & 0b0000_0001) != 0;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span, out bool val1, out bool val2)
        {
            byte bits = span[0];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span, out bool val1, out bool val2, out bool val3)
        {
            byte bits = span[0];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span, out bool val1, out bool val2, out bool val3, out bool val4)
        {
            byte bits = span[0];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            val4 = (bits & 0b0000_1000) != 0;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span, out bool val1, out bool val2, out bool val3, out bool val4, out bool val5)
        {
            byte bits = span[0];
            val1 = (bits & 0b0000_0001) != 0;
            val2 = (bits & 0b0000_0010) != 0;
            val3 = (bits & 0b0000_0100) != 0;
            val4 = (bits & 0b0000_1000) != 0;
            val5 = (bits & 0b0001_0000) != 0;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadBits(ReadOnlySpan<byte> span,
            out bool val1, out bool val2, out bool val3, out bool val4, out bool val5,
            out bool val6, out bool val7, out bool val8, out bool val9, out bool val10,
            out bool val11, out bool val12, out bool val13, out bool val14)
        {
            byte bits = span[0];
            val1 = (bits & 0b1000_0000) != 0;
            val2 = (bits & 0b0100_0000) != 0;
            val3 = (bits & 0b0010_0000) != 0;
            val4 = (bits & 0b0001_0000) != 0;
            val5 = (bits & 0b0000_1000) != 0;
            val6 = (bits & 0b0000_0100) != 0;
            val7 = (bits & 0b0000_0010) != 0;
            val8 = (bits & 0b0000_0001) != 0;
            bits = span[1];
            val9 = (bits & 0b1000_0000) != 0;
            val10 = (bits & 0b0100_0000) != 0;
            val11 = (bits & 0b0010_0000) != 0;
            val12 = (bits & 0b0001_0000) != 0;
            val13 = (bits & 0b0000_1000) != 0;
            val14 = (bits & 0b0000_0100) != 0;
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadShort(ReadOnlySpan<byte> span, out ushort value)
        {
            value = NetworkOrderDeserializer.ReadUInt16(span);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadLong(ReadOnlySpan<byte> span, out uint value)
        {
            value = NetworkOrderDeserializer.ReadUInt32(span);
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadLonglong(ReadOnlySpan<byte> span, out ulong value)
        {
            value = NetworkOrderDeserializer.ReadUInt64(span);
            return 8;
        }

        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.</returns>
        public static int ReadDictionary(ReadOnlySpan<byte> span, out Dictionary<string, object> valueDictionary)
        {
            long tableLength = NetworkOrderDeserializer.ReadUInt32(span);
            if (tableLength == 0)
            {
                valueDictionary = null;
                return 4;
            }

            span = span.Slice(4);
            valueDictionary = new Dictionary<string, object>();
            int bytesRead = 0;
            while (bytesRead < tableLength)
            {
                bytesRead += ReadShortstr(span.Slice(bytesRead), out string key);
                valueDictionary[key] = ReadFieldValue(span.Slice(bytesRead), out int valueBytesRead);
                bytesRead += valueBytesRead;
            }

            return 4 + bytesRead;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadTimestamp(ReadOnlySpan<byte> span, out AmqpTimestamp value)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            value = new AmqpTimestamp((long)NetworkOrderDeserializer.ReadUInt64(span));
            return 8;
        }

        public static int WriteArray(Span<byte> span, IList val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0);
                return 4;
            }

            int bytesWritten = 4;
            for (int index = 0; index < val.Count; index++)
            {
                bytesWritten += WriteFieldValue(span.Slice(bytesWritten), val[index]);
            }

            NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten - 4u);
            return bytesWritten;
        }

        public static int GetArrayByteCount(IList val)
        {
            if (val is null || val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;
            if (val is List<object> valList)
            {
                for (int index = 0; index < valList.Count; index++)
                {
                    byteCount += GetFieldValueByteCount(valList[index]);
                }
            }
            else
            {
                for (int index = 0; index < val.Count; index++)
                {
                    byteCount += GetFieldValueByteCount(val[index]);
                }
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
            // Order by likelihood of occurrence
            Span<byte> slice = span.Slice(1);
            ref var type = ref span[0];
            switch (value)
            {
                case null:
                    type = (byte)'V';
                    return 1;
                case string val:
                    type = (byte)'S';
                    return 1 + WriteLongstr(slice, val);
                case bool val:
                    type = (byte)'t';
                    span[1] = (byte)(val ? 1 : 0);
                    return 2;
                case int val:
                    type = (byte)'I';
                    NetworkOrderSerializer.WriteInt32(slice, val);
                    return 5;
                case byte[] val:
                    type = (byte)'S';
                    return 1 + WriteLongstr(slice, val);
                default:
                    return WriteFieldValueSlow(span, value);
            }

            // Moved out of outer switch to have a shorter main method (improves performance)
            static int WriteFieldValueSlow(Span<byte> span, object value)
            {
                // Order by likelihood of occurrence
                ref var type = ref span[0];
                Span<byte> slice = span.Slice(1);
                switch (value)
                {
                    case float val:
                        type = (byte)'f';
                        NetworkOrderSerializer.WriteSingle(slice, val);
                        return 5;
                    case IDictionary<string,object> val:
                        type = (byte)'F';
                        return 1 + WriteTable(slice, val);
                    case IList val:
                        type = (byte)'A';
                        return 1 + WriteArray(slice, val);
                    case AmqpTimestamp val:
                        type = (byte)'T';
                        return 1 + WriteTimestamp(slice, val);
                    case double val:
                        type = (byte)'d';
                        NetworkOrderSerializer.WriteDouble(slice, val);
                        return 9;
                    case long val:
                        type = (byte)'l';
                        NetworkOrderSerializer.WriteInt64(slice, val);
                        return 9;
                    case byte val:
                        type = (byte)'B';
                        span[1] = val;
                        return 2;
                    case sbyte val:
                        type = (byte)'b';
                        span[1] = (byte)val;
                        return 2;
                    case short val:
                        type = (byte)'s';
                        NetworkOrderSerializer.WriteInt16(slice, val);
                        return 3;
                    case uint val:
                        type = (byte)'i';
                        NetworkOrderSerializer.WriteUInt32(slice, val);
                        return 5;
                    case decimal val:
                        type = (byte)'D';
                        return 1 + WriteDecimal(slice, val);
                    case IDictionary val:
                        type = (byte)'F';
                        return 1 + WriteTable(slice, val);
                    case BinaryTableValue val:
                        type = (byte)'x';
                        return 1 + WriteLongstr(slice, val.Bytes);
                    default:
                        return ThrowInvalidTableValue(value);
                }
            }
        }

        public static int GetFieldValueByteCount(object value)
        {
            // Order by likelihood of occurrence
            switch (value)
            {
                case null:
                    return 1;
                case string val:
                    return 5 + GetByteCount(val);
                case bool _:
                    return 2;
                case int _:
                case float _:
                    return 5;
                case byte[] val:
                    return 5 + val.Length;
                case IDictionary<string, object> val:
                    return 1 + GetTableByteCount(val);
                case IList val:
                    return 1 + GetArrayByteCount(val);
                case AmqpTimestamp _:
                case double _:
                case long _:
                    return 9;
                case byte _:
                case sbyte _:
                    return 2;
                case short _:
                    return 3;
                case uint _:
                    return 5;
                case decimal _:
                    return 6;
                case IDictionary val:
                    return 1 + GetTableByteCount(val);
                case BinaryTableValue val:
                    return 5 + val.Bytes.Length;
                default:
                    return ThrowInvalidTableValue(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLong(Span<byte> span, uint val)
        {
            NetworkOrderSerializer.WriteUInt32(span, val);
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLonglong(Span<byte> span, ulong val)
        {
            NetworkOrderSerializer.WriteUInt64(span, val);
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span, bool val)
        {
            span[0] = (byte)(val ? 1 : 0);
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span, bool val1, bool val2)
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
            span[0] = (byte)bits;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span, bool val1, bool val2, bool val3)
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

            span[0] = (byte)bits;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span, bool val1, bool val2, bool val3, bool val4)
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

            span[0] = (byte)bits;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span, bool val1, bool val2, bool val3, bool val4, bool val5)
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
            span[0] = (byte)bits;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(Span<byte> span,
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
            span[1] = (byte)bits;

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
            span[0] = (byte)bits;
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLongstr(Span<byte> span, ReadOnlySpan<byte> val)
        {
            WriteLong(span, (uint)val.Length);
            val.CopyTo(span.Slice(4));
            return 4 + val.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShort(Span<byte> span, ushort val)
        {
            NetworkOrderSerializer.WriteUInt16(span, val);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShortstr(Span<byte> span, ReadOnlySpan<byte> value)
        {
            var length = value.Length;
            if (length <= byte.MaxValue)
            {
                span[0] = (byte)length;
                value.CopyTo(span.Slice(1));
                return length + 1;
            }

            return ThrowArgumentTooLong(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShortstr(Span<byte> span, string val)
        {
            int bytesWritten = 0;
            if (!string.IsNullOrEmpty(val))
            {
                int maxLength = span.Length - 1;
                if (maxLength > byte.MaxValue)
                {
                    maxLength = byte.MaxValue;
                }
#if NETCOREAPP
                try
                {
                    bytesWritten = UTF8.GetBytes(val, span.Slice(1, maxLength));
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
                            fixed (byte* bytes = span.Slice(1))
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

            span[0] = (byte)bytesWritten;
            return bytesWritten + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP
        public static int WriteLongstr(Span<byte> span, ReadOnlySpan<char> val)
        {
            int bytesWritten = val.IsEmpty ? 0 : UTF8.GetBytes(val, span.Slice(4));
#else
        public static int WriteLongstr(Span<byte> span, string val)
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

            int bytesWritten = string.IsNullOrEmpty(val) ? 0 : GetBytes(span.Slice(4), val);
#endif
            NetworkOrderSerializer.WriteUInt32(span, (uint)bytesWritten);
            return bytesWritten + 4;
        }

        public static int WriteTable(Span<byte> span, IDictionary val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0u);
                return 4;
            }

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

        public static int WriteTable(Span<byte> span, IDictionary<string, object> val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(span, 0);
                return 4;
            }

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

        public static int GetTableByteCount(IDictionary val)
        {
            if (val is null || val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;
            foreach (DictionaryEntry entry in val)
            {
                byteCount += GetByteCount(entry.Key.ToString()) + 1;
                byteCount += GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int GetTableByteCount(IDictionary<string, object> val)
        {
            if (val is null || val.Count == 0)
            {
                return 4;
            }

            int byteCount = 4;
            if (val is Dictionary<string, object> dict)
            {
                foreach (KeyValuePair<string, object> entry in dict)
                {
                    byteCount += GetByteCount(entry.Key) + 1;
                    byteCount += GetFieldValueByteCount(entry.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> entry in val)
                {
                    byteCount += GetByteCount(entry.Key) + 1;
                    byteCount += GetFieldValueByteCount(entry.Value);
                }
            }

            return byteCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTimestamp(Span<byte> span, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            return WriteLonglong(span, (ulong)val.UnixTime);
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

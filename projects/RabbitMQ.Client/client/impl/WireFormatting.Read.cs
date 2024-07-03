// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal static partial class WireFormatting
    {
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

        public static IList? ReadArray(ReadOnlySpan<byte> span, out int bytesRead)
        {
            bytesRead = 4;
            long arrayLength = NetworkOrderDeserializer.ReadUInt32(span);
            if (arrayLength == 0)
            {
                return null;
            }
            List<object?> array = new List<object?>();
            while (bytesRead - 4 < arrayLength)
            {
                array.Add(ReadFieldValue(span.Slice(bytesRead), out int fieldValueBytesRead));
                bytesRead += fieldValueBytesRead;
            }

            return array;
        }

        public static object? ReadFieldValue(ReadOnlySpan<byte> span, out int bytesRead)
        {
            switch ((char)span[0])
            {
                case 'S':
                    bytesRead = 1 + ReadLongstr(span.Slice(1), out byte[] bytes);
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
            static object? ReadFieldValueSlow(ReadOnlySpan<byte> span, out int bytesRead)
            {
                ReadOnlySpan<byte> slice = span.Slice(1);
                switch ((char)span[0])
                {
                    case 'F':
                        bytesRead = 1 + ReadDictionary(slice, out Dictionary<string, object?>? dictionary);
                        return dictionary;
                    case 'A':
                        IList? arrayResult = ReadArray(slice, out int arrayBytesRead);
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
                    case 'u':
                        bytesRead = 3;
                        return NetworkOrderDeserializer.ReadUInt16(slice);
                    case 'T':
                        bytesRead = 1 + ReadTimestamp(slice, out AmqpTimestamp timestamp);
                        return timestamp;
                    case 'x':
                        bytesRead = 1 + ReadLongstr(slice, out byte[] binaryTableResult);
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
                value = null!;
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
        public static int ReadDictionary(ReadOnlySpan<byte> span, out Dictionary<string, object?>? valueDictionary)
        {
            long tableLength = NetworkOrderDeserializer.ReadUInt32(span);
            if (tableLength == 0)
            {
                valueDictionary = null;
                return 4;
            }

            span = span.Slice(4);
            valueDictionary = new Dictionary<string, object?>();
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

        public static int ThrowSyntaxErrorException(uint byteCount)
            => throw new SyntaxErrorException($"Long string too long; byte length={byteCount}, max={int.MaxValue}");

        private static int ThrowInvalidTableValue(char type)
            => throw new SyntaxErrorException($"Unrecognised type in table: {type}");

        private static void ThrowInvalidDecimalScale(int scale)
            => throw new SyntaxErrorException($"Unrepresentable AMQP decimal table field: scale={scale}");
    }
}

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

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal static partial class WireFormatting
    {
        public static int WriteArray(ref byte destination, IList val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref destination, 0);
                return 4;
            }

            int bytesWritten = 4;
            for (int index = 0; index < val.Count; index++)
            {
                bytesWritten += WriteFieldValue(ref destination.GetOffset(bytesWritten), val[index]);
            }

            NetworkOrderSerializer.WriteUInt32(ref destination, (uint)bytesWritten - 4u);
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
        public static int WriteDecimal(ref byte destination, decimal value)
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

            destination = (byte)((data.Flags >> 16) & 0xFF);
            WriteLong(ref destination.GetOffset(1), (data.Flags & 0b1000_0000_0000_0000_0000_0000_0000_0000) | (data.Lo & 0b0111_1111_1111_1111_1111_1111_1111_1111));
            return 5;
        }

        public static int WriteFieldValue(ref byte destination, object value)
        {
            if (value == null)
            {
                destination = (byte)'V';
                return 1;
            }

            // Order by likelihood of occurrence
            ref byte fieldValue = ref destination.GetOffset(1);
            switch (value)
            {
                case string val:
                    destination = (byte)'S';
                    return 1 + WriteLongstr(ref fieldValue, val);
                case bool val:
                    destination = (byte)'t';
                    fieldValue = val.ToByte();
                    return 2;
                case int val:
                    destination = (byte)'I';
                    NetworkOrderSerializer.WriteInt32(ref fieldValue, val);
                    return 5;
                case byte[] val:
                    destination = (byte)'S';
                    return 1 + WriteLongstr(ref fieldValue, val);
                default:
                    return WriteFieldValueSlow(ref destination, ref fieldValue, value);
            }

            // Moved out of outer switch to have a shorter main method (improves performance)
            static int WriteFieldValueSlow(ref byte destination, ref byte fieldValue, object value)
            {
                // Order by likelihood of occurrence
                switch (value)
                {
                    case float val:
                        destination = (byte)'f';
                        NetworkOrderSerializer.WriteSingle(ref fieldValue, val);
                        return 5;
                    case IDictionary<string, object> val:
                        destination = (byte)'F';
                        return 1 + WriteTable(ref fieldValue, val);
                    case IList val:
                        destination = (byte)'A';
                        return 1 + WriteArray(ref fieldValue, val);
                    case AmqpTimestamp val:
                        destination = (byte)'T';
                        return 1 + WriteTimestamp(ref fieldValue, val);
                    case double val:
                        destination = (byte)'d';
                        NetworkOrderSerializer.WriteDouble(ref fieldValue, val);
                        return 9;
                    case long val:
                        destination = (byte)'l';
                        NetworkOrderSerializer.WriteInt64(ref fieldValue, val);
                        return 9;
                    case byte val:
                        destination = (byte)'B';
                        fieldValue = val;
                        return 2;
                    case sbyte val:
                        destination = (byte)'b';
                        fieldValue = (byte)val;
                        return 2;
                    case short val:
                        destination = (byte)'s';
                        NetworkOrderSerializer.WriteInt16(ref fieldValue, val);
                        return 3;
                    case uint val:
                        destination = (byte)'i';
                        NetworkOrderSerializer.WriteUInt32(ref fieldValue, val);
                        return 5;
                    case decimal val:
                        destination = (byte)'D';
                        return 1 + WriteDecimal(ref fieldValue, val);
                    case IDictionary val:
                        destination = (byte)'F';
                        return 1 + WriteTable(ref fieldValue, val);
                    case BinaryTableValue val:
                        destination = (byte)'x';
                        return 1 + WriteLongstr(ref fieldValue, val.Bytes);
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
        public static int WriteLong(ref byte destination, uint val)
        {
            NetworkOrderSerializer.WriteUInt32(ref destination, val);
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLonglong(ref byte destination, ulong val)
        {
            NetworkOrderSerializer.WriteUInt64(ref destination, val);
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(ref byte destination, bool val)
        {
            destination = val.ToByte();
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(ref byte destination, bool val1, bool val2)
        {
            int a = val1.ToByte() + val2.ToByte() * 2;
            destination = (byte)a;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3)
        {
            int a = val1.ToByte() + val2.ToByte() * 2 + val3.ToByte() * 4;
            destination = (byte)a;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3, bool val4)
        {
            int a = val1.ToByte() + val2.ToByte() * 2 + val3.ToByte() * 4 + val4.ToByte() * 8;
            destination = (byte)a;
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteBits(ref byte destination, bool val1, bool val2, bool val3, bool val4, bool val5)
        {
            int a = val1.ToByte() + val2.ToByte() * 2 + val3.ToByte() * 4 + val4.ToByte() * 8;
            int b = val5.ToByte();
            destination = (byte)(a | (b << 4));
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLongstr(ref byte destination, ReadOnlySpan<byte> val)
        {
            WriteLong(ref destination, (uint)val.Length);
            Unsafe.CopyBlockUnaligned(ref destination.GetOffset(4), ref val.GetStart(), (uint)val.Length);
            return 4 + val.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShort(ref byte destination, ushort val)
        {
            NetworkOrderSerializer.WriteUInt16(ref destination, val);
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShortstr(ref byte destination, ReadOnlySpan<byte> value)
        {
            var length = value.Length;
            if (length <= byte.MaxValue)
            {
                destination = (byte)length;
                Unsafe.CopyBlockUnaligned(ref destination.GetOffset(1), ref value.GetStart(), (uint)value.Length);
                return length + 1;
            }

            return ThrowArgumentTooLong(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteShortstr(ref byte destination, string val)
        {
            int bytesWritten = 0;
            if (!string.IsNullOrEmpty(val))
            {
                unsafe
                {

                    ref byte valDestination = ref destination.GetOffset(1);
                    fixed (char* chars = val)
                    fixed (byte* bytes = &valDestination)
                    {
                        bytesWritten = UTF8.GetBytes(chars, val.Length, bytes, byte.MaxValue);
                    }
                }
            }

            destination = unchecked((byte)bytesWritten);
            return bytesWritten + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLongstr(ref byte destination, string val)
        {
            static int GetBytes(ref byte destination, string val)
            {
                unsafe
                {
                    fixed (char* chars = val)
                    fixed (byte* bytes = &destination)
                    {
                        return UTF8.GetBytes(chars, val.Length, bytes, int.MaxValue);
                    }
                }
            }

            int bytesWritten = string.IsNullOrEmpty(val) ? 0 : GetBytes(ref destination.GetOffset(4), val);
            NetworkOrderSerializer.WriteUInt32(ref destination, (uint)bytesWritten);
            return bytesWritten + 4;
        }

        public static int WriteTable(ref byte destination, IDictionary val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref destination, 0u);
                return 4;
            }

            // Let's only write after the length header.
            int bytesWritten = 4;
            foreach (DictionaryEntry entry in val)
            {
                bytesWritten += WriteShortstr(ref destination.GetOffset(bytesWritten), entry.Key.ToString());
                bytesWritten += WriteFieldValue(ref destination.GetOffset(bytesWritten), entry.Value);
            }

            NetworkOrderSerializer.WriteUInt32(ref destination, (uint)(bytesWritten - 4));
            return bytesWritten;
        }

        public static int WriteTable(ref byte destination, IDictionary<string, object> val)
        {
            if (val is null || val.Count == 0)
            {
                NetworkOrderSerializer.WriteUInt32(ref destination, 0);
                return 4;
            }

            // Let's only write after the length header.
            int bytesWritten = 4;
            if (val is Dictionary<string, object> dict)
            {
                foreach (KeyValuePair<string, object> entry in dict)
                {
                    bytesWritten += WriteShortstr(ref destination.GetOffset(bytesWritten), entry.Key);
                    bytesWritten += WriteFieldValue(ref destination.GetOffset(bytesWritten), entry.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> entry in val)
                {
                    bytesWritten += WriteShortstr(ref destination.GetOffset(bytesWritten), entry.Key);
                    bytesWritten += WriteFieldValue(ref destination.GetOffset(bytesWritten), entry.Value);
                }
            }

            NetworkOrderSerializer.WriteUInt32(ref destination, (uint)(bytesWritten - 4));
            return bytesWritten;
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
        public static int WriteTimestamp(ref byte destination, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            return WriteLonglong(ref destination, (ulong)val.UnixTime);
        }

        public static int ThrowArgumentTooLong(int length)
            => throw new ArgumentOutOfRangeException("value", $"Value exceeds the maximum allowed length of 255 bytes, was {length} long.");

        public static int ThrowArgumentOutOfRangeException(int orig, int expected)
            => throw new ArgumentOutOfRangeException("span", $"Span has not enough space ({orig} instead of {expected})");

        private static int ThrowWireFormattingException(decimal value)
            => throw new WireFormattingException("Decimal overflow in AMQP encoding", value);

        private static int ThrowInvalidTableValue(object value)
            => throw new WireFormattingException($"Value of type '{value.GetType().Name}' cannot appear as table value", value);
    }
}

// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal class WireFormatting
    {
        public static decimal AmqpToDecimal(byte scale, uint unsignedMantissa)
        {
            if (scale > 28)
            {
                throw new SyntaxErrorException($"Unrepresentable AMQP decimal table field: scale={scale}");
            }

            return new decimal(
                // The low 32 bits of a 96-bit integer
                lo: (int)(unsignedMantissa & 0x7FFFFFFF),
                // The middle 32 bits of a 96-bit integer.
                mid: 0,
                // The high 32 bits of a 96-bit integer.
                hi: 0,
                isNegative: (unsignedMantissa & 0x80000000) != 0,
                // A power of 10 ranging from 0 to 28.
                scale: scale);
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

        public static IList ReadArray(ReadOnlyMemory<byte> memory, out int bytesRead)
        {
            uint arrayLength = NetworkOrderDeserializer.ReadUInt32(memory);
            List<object> array = new List<object>((int)arrayLength);
            bytesRead = 4;
            while (bytesRead - 4 < arrayLength)
            {
                object value = ReadFieldValue(memory.Slice(bytesRead), out int fieldValueBytesRead);
                bytesRead += fieldValueBytesRead;
                array.Add(value);
            }

            return array;
        }

        public static decimal ReadDecimal(ReadOnlyMemory<byte> memory)
        {
            byte scale = memory.Span[0];
            uint unsignedMantissa = NetworkOrderDeserializer.ReadUInt32(memory.Slice(1));
            return AmqpToDecimal(scale, unsignedMantissa);
        }

        public static object ReadFieldValue(ReadOnlyMemory<byte> memory, out int bytesRead)
        {
            bytesRead = 1;
            ReadOnlyMemory<byte> slice = memory.Slice(1);
            switch ((char)memory.Span[0])
            {
                case 'S':
                    byte[] result = ReadLongstr(slice);
                    bytesRead += result.Length + 4;
                    return result;
                case 'I':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadInt32(slice);
                case 'i':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadUInt32(slice);
                case 'D':
                    bytesRead += 5;
                    return ReadDecimal(slice);
                case 'T':
                    bytesRead += 8;
                    return ReadTimestamp(slice);
                case 'F':
                    Dictionary<string, object> tableResult = ReadTable(slice, out int tableBytesRead);
                    bytesRead += tableBytesRead;
                    return tableResult;
                case 'A':
                    IList arrayResult = ReadArray(slice, out int arrayBytesRead);
                    bytesRead += arrayBytesRead;
                    return arrayResult;
                case 'B':
                    bytesRead += 1;
                    return slice.Span[0];
                case 'b':
                    bytesRead += 1;
                    return (sbyte)slice.Span[0];
                case 'd':
                    bytesRead += 8;
                    return NetworkOrderDeserializer.ReadDouble(slice);
                case 'f':
                    bytesRead += 4;
                    return NetworkOrderDeserializer.ReadSingle(slice);
                case 'l':
                    bytesRead += 8;
                    return NetworkOrderDeserializer.ReadInt64(slice);
                case 's':
                    bytesRead += 2;
                    return NetworkOrderDeserializer.ReadInt16(slice);
                case 't':
                    bytesRead += 1;
                    return slice.Span[0] != 0;
                case 'x':
                    byte[] binaryTableResult = ReadLongstr(slice);
                    bytesRead += binaryTableResult.Length + 4;
                    return new BinaryTableValue(binaryTableResult);
                case 'V':
                    return null;
                default:
                    throw new SyntaxErrorException($"Unrecognised type in table: {(char)memory.Span[0]}");
            }
        }

        public static byte[] ReadLongstr(ReadOnlyMemory<byte> memory)
        {
            int byteCount = (int)NetworkOrderDeserializer.ReadUInt32(memory);
            if (byteCount > int.MaxValue)
            {
                throw new SyntaxErrorException($"Long string too long; byte length={byteCount}, max={int.MaxValue}");
            }

            return memory.Slice(4, byteCount).ToArray();
        }

        public static string ReadShortstr(ReadOnlyMemory<byte> memory, out int bytesRead)
        {
            int byteCount = memory.Span[0];
            ReadOnlyMemory<byte> stringSlice = memory.Slice(1, byteCount);
            if (MemoryMarshal.TryGetArray(stringSlice, out ArraySegment<byte> segment))
            {
                bytesRead = 1 + byteCount;
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            }

            throw new InvalidOperationException("Unable to get ArraySegment from memory");
        }

        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.</returns>
        public static Dictionary<string, object> ReadTable(ReadOnlyMemory<byte> memory, out int bytesRead)
        {
            Dictionary<string, object> table = new Dictionary<string, object>();
            long tableLength = NetworkOrderDeserializer.ReadUInt32(memory);
            bytesRead = 4;
            while ((bytesRead - 4) < tableLength)
            {
                string key = ReadShortstr(memory.Slice(bytesRead), out int keyBytesRead);
                bytesRead += keyBytesRead;
                object value = ReadFieldValue(memory.Slice(bytesRead), out int valueBytesRead);
                bytesRead += valueBytesRead;

                if (!table.ContainsKey(key))
                {
                    table[key] = value;
                }
            }

            return table;
        }

        public static AmqpTimestamp ReadTimestamp(ReadOnlyMemory<byte> memory)
        {
            ulong stamp = NetworkOrderDeserializer.ReadUInt64(memory);
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            return new AmqpTimestamp((long)stamp);
        }

        public static int WriteArray(Memory<byte> memory, IList val)
        {
            if (val == null)
            {
                NetworkOrderSerializer.WriteUInt32(memory, 0);
                return 4;
            }
            else
            {
                int bytesWritten = 0;
                for (int index = 0; index < val.Count; index++)
                {
                    bytesWritten += WriteFieldValue(memory.Slice(4 + bytesWritten), val[index]);
                }

                NetworkOrderSerializer.WriteUInt32(memory, (uint)bytesWritten);
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

        public static int WriteDecimal(Memory<byte> memory, decimal value)
        {
            DecimalToAmqp(value, out byte scale, out int mantissa);
            memory.Span[0] = scale;
            return 1 + WriteLong(memory.Slice(1), (uint)mantissa);
        }

        public static int WriteFieldValue(Memory<byte> memory, object value)
        {
            if (value == null)
            {
                memory.Span[0] = (byte)'V';
                return 1;
            }

            Memory<byte> slice = memory.Slice(1);
            switch (value)
            {
                case string val:
                    memory.Span[0] = (byte)'S';
                    if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                    {
                        int bytesWritten = Encoding.UTF8.GetBytes(val, 0, val.Length, segment.Array, segment.Offset +  5);
                        NetworkOrderSerializer.WriteUInt32(slice, (uint)bytesWritten);
                        return 5 + bytesWritten;
                    }

                    throw new WireFormattingException("Unable to get array segment from memory.");
                case byte[] val:
                    memory.Span[0] = (byte)'S';
                    return 1 + WriteLongstr(slice, val);
                case int val:
                    memory.Span[0] = (byte)'I';
                    NetworkOrderSerializer.WriteInt32(slice, val);
                    return 5;
                case uint val:
                    memory.Span[0] = (byte)'i';
                    NetworkOrderSerializer.WriteUInt32(slice, val);
                    return 5;
                case decimal val:
                    memory.Span[0] = (byte)'D';
                    return 1 + WriteDecimal(slice, val);
                case AmqpTimestamp val:
                    memory.Span[0] = (byte)'T';
                    return 1 + WriteTimestamp(slice, val);
                case IDictionary val:
                    memory.Span[0] = (byte)'F';
                    return 1 + WriteTable(slice, val);
                case IList val:
                    memory.Span[0] = (byte)'A';
                    return 1 + WriteArray(slice, val);
                case byte val:
                    memory.Span[0] = (byte)'B';
                    memory.Span[1] = val;
                    return 2;
                case sbyte val:
                    memory.Span[0] = (byte)'b';
                    memory.Span[1] = (byte)val;
                    return 2;
                case double val:
                    memory.Span[0] = (byte)'d';
                    NetworkOrderSerializer.WriteDouble(slice, val);
                    return 9;
                case float val:
                    memory.Span[0] = (byte)'f';
                    NetworkOrderSerializer.WriteSingle(slice, val);
                    return 5;
                case long val:
                    memory.Span[0] = (byte)'l';
                    NetworkOrderSerializer.WriteInt64(slice, val);
                    return 9;
                case short val:
                    memory.Span[0] = (byte)'s';
                    NetworkOrderSerializer.WriteInt16(slice, val);
                    return 3;
                case bool val:
                    memory.Span[0] = (byte)'t';
                    memory.Span[1] = (byte)(val ? 1 : 0);
                    return 2;
                case BinaryTableValue val:
                    memory.Span[0] = (byte)'x';
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

        public static int WriteLong(Memory<byte> memory, uint val)
        {
            NetworkOrderSerializer.WriteUInt32(memory, val);
            return 4;
        }

        public static int WriteLonglong(Memory<byte> memory, ulong val)
        {
            NetworkOrderSerializer.WriteUInt64(memory, val);
            return 8;
        }

        public static int WriteLongstr(Memory<byte> memory, byte[] val)
        {
            return WriteLongstr(memory, val, 0, val.Length);
        }

        public static int WriteLongstr(Memory<byte> memory, byte[] val, int index, int count)
        {
            WriteLong(memory, (uint)count);
            val.AsMemory(index, count).CopyTo(memory.Slice(4));
            return 4 + count;
        }

        public static int WriteShort(Memory<byte> memory, ushort val)
        {
            NetworkOrderSerializer.WriteUInt16(memory, val);
            return 2;
        }

        public static int WriteShortstr(Memory<byte> memory, string val)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
            {
                int bytesWritten = Encoding.UTF8.GetBytes(val, 0, val.Length, segment.Array, segment.Offset + 1);
                if (bytesWritten <= byte.MaxValue)
                {
                    memory.Span[0] = (byte)bytesWritten;
                   return bytesWritten + 1;
                }

                throw new ArgumentOutOfRangeException(nameof(val), val, "Value exceeds the maximum allowed length of 255 bytes.");
            }

            throw new WireFormattingException("Unable to get array segment from memory.");
        }

        public static int WriteTable(Memory<byte> memory, IDictionary val)
        {
            if (val == null)
            {
                NetworkOrderSerializer.WriteUInt32(memory, 0);
                return 4;
            }
            else
            {
                // Let's only write after the length header.
                Memory<byte> slice = memory.Slice(4);
                int bytesWritten = 0;
                foreach (DictionaryEntry entry in val)
                {
                    bytesWritten += WriteShortstr(slice.Slice(bytesWritten), entry.Key.ToString());
                    bytesWritten += WriteFieldValue(slice.Slice(bytesWritten), entry.Value);
                }

                NetworkOrderSerializer.WriteUInt32(memory, (uint)bytesWritten);
                return 4 + bytesWritten;
            }
        }

        public static int WriteTable(Memory<byte> memory, IDictionary<string, object> val)
        {
            if (val == null)
            {
                NetworkOrderSerializer.WriteUInt32(memory, 0);
                return 4;
            }
            else
            {
                // Let's only write after the length header.
                Memory<byte> slice = memory.Slice(4);
                int bytesWritten = 0;
                foreach (KeyValuePair<string, object> entry in val)
                {
                    bytesWritten += WriteShortstr(slice.Slice(bytesWritten), entry.Key);
                    bytesWritten += WriteFieldValue(slice.Slice(bytesWritten), entry.Value);
                }

                NetworkOrderSerializer.WriteUInt32(memory, (uint)bytesWritten);
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

            foreach (KeyValuePair<string, object> entry in val)
            {
                byteCount += Encoding.UTF8.GetByteCount(entry.Key) + 1;
                byteCount += GetFieldValueByteCount(entry.Value);
            }

            return byteCount;
        }

        public static int WriteTimestamp(Memory<byte> memory, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            return WriteLonglong(memory, (ulong)val.UnixTime);
        }
    }
}

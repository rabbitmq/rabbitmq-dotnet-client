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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.src.client.impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class WireFormatting
    {
        public static decimal AmqpToDecimal(byte scale, uint unsignedMantissa)
        {
            if (scale > 28)
            {
                throw new SyntaxError("Unrepresentable AMQP decimal table field: " +
                                      "scale=" + scale);
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

        public static IList ReadArray(BinaryBufferReader reader)
        {
            IList array = new List<object>();
            long arrayLength = reader.ReadUInt32();

            int unreaded = reader.Unreaded.Length;
            while ((unreaded - reader.Unreaded.Length) < arrayLength)
            {
                object value = ReadFieldValue(reader);
                array.Add(value);
            }
            return array;
        }

        public static decimal ReadDecimal(BinaryBufferReader reader)
        {
            byte scale = ReadOctet(reader);
            uint unsignedMantissa = ReadLong(reader);
            return AmqpToDecimal(scale, unsignedMantissa);
        }

        public static object ReadFieldValue(BinaryBufferReader reader)
        {
            byte discriminator = reader.ReadByte();

            object value;
            switch (discriminator)
            {
                case WireConstants.String:
                    value = ReadLongstr(reader);
                    break;
                case WireConstants.Int:
                    value = reader.ReadInt32();
                    break;
                case WireConstants.Uint:
                    value = reader.ReadUInt32();
                    break;
                case WireConstants.Decimal:
                    value = ReadDecimal(reader);
                    break;
                case WireConstants.Timestamp:
                    value = ReadTimestamp(reader);
                    break;
                case WireConstants.Dictionary:
                    value = ReadTable(reader);
                    break;
                case WireConstants.List:
                    value = ReadArray(reader);
                    break;
                case WireConstants.Byte:
                    value = reader.ReadByte();
                    break;
                case WireConstants.Sbyte:
                    value = reader.ReadSByte();
                    break;
                case WireConstants.Double:
                    value = reader.ReadDouble();
                    break;
                case WireConstants.Float:
                    value = reader.ReadSingle();
                    break;
                case WireConstants.Long:
                    value = reader.ReadInt64();
                    break;
                case WireConstants.Short:
                    value = reader.ReadInt16();
                    break;
                case WireConstants.Bool:
                    value = ReadOctet(reader) != 0;
                    break;
                case WireConstants.TableValue:
                    value = new BinaryTableValue(ReadLongstr(reader));
                    break;
                case WireConstants.Null:
                    value = null;
                    break;

                default:
                    throw new SyntaxError("Unrecognised type in table: " +
                                          (char)discriminator);
            }
            return value;
        }


        public static uint ReadLong(BinaryBufferReader reader)
        {
            return reader.ReadUInt32();
        }

        public static ulong ReadLonglong(BinaryBufferReader reader)
        {
            return reader.ReadUInt64();
        }

        public static byte[] ReadLongstr(BinaryBufferReader reader)
        {
            uint byteCount = reader.ReadUInt32();
            if (byteCount > int.MaxValue)
            {
                throw new SyntaxError("Long string too long; " +
                                      "byte length=" + byteCount + ", max=" + int.MaxValue);
            }
            return reader.ReadBytes((int)byteCount);
        }

        public static byte ReadOctet(BinaryBufferReader reader)
        {
            return reader.ReadByte();
        }

        public static ushort ReadShort(BinaryBufferReader reader)
        {
            return reader.ReadUInt16();
        }

#if NETSTANDARD2_1
        public static string ReadShortstr(BinaryBufferReader reader)
        {
            int byteCount = reader.ReadByte();
            Span<byte> span = stackalloc byte[byteCount]; //always less then 256 bytes
            reader.ReadBytes(span);
            string returnValue = Encoding.UTF8.GetString(span);
            return returnValue;
        }
#else
        public static string ReadShortstr(BinaryBufferReader reader)
        {
            int byteCount = reader.ReadByte();
            byte[] readBytes = ArrayPool<byte>.Shared.Rent(byteCount);
            reader.ReadBytes(new Span<byte>(readBytes, 0, byteCount));
            string returnValue = Encoding.UTF8.GetString(readBytes, 0, byteCount);
            ArrayPool<byte>.Shared.Return(readBytes);
            return returnValue;
        }
#endif


        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>.</returns>
        public static IDictionary<string, object> ReadTable(BinaryBufferReader reader)
        {
            IDictionary<string, object> table = new Dictionary<string, object>();
            long tableLength = reader.ReadUInt32();

            int unreaded = reader.Unreaded.Length;
            while ((unreaded - reader.Unreaded.Length) < tableLength)
            {
                string key = ReadShortstr(reader);
                object value = ReadFieldValue(reader);

                if (!table.ContainsKey(key))
                {
                    table[key] = value;
                }
            }

            return table;
        }

        public static AmqpTimestamp ReadTimestamp(BinaryBufferReader reader)
        {
            ulong stamp = ReadLonglong(reader);
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            return new AmqpTimestamp((long)stamp);
        }

        public static void WriteArray(BinaryBufferWriter writer, IList val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                Span<byte> buffer = writer.GetBufferWithAdvance(sizeof(uint));
                int patchPosition = writer.Position;
                foreach (object entry in val)
                {
                    WriteFieldValue(writer, entry);
                }
                int savedPosition = writer.Position;
                int tableLength = savedPosition - patchPosition;
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)tableLength);
            }
        }

        public static void WriteDecimal(BinaryBufferWriter writer, decimal value)
        {
            DecimalToAmqp(value, out byte scale, out int mantissa);
            WriteOctet(writer, scale);
            WriteLong(writer, (uint)mantissa);
        }

        public static void WriteFieldValue(BinaryBufferWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    WriteOctet(writer, WireConstants.Null);
                    break;
                case string val:
                    WriteOctet(writer, WireConstants.String);
                    WriteLongstr(writer, val);
                    break;
                case byte[] val:
                    WriteOctet(writer, WireConstants.Array);
                    WriteLongstr(writer, val, val.Length);
                    break;
                case int val:
                    WriteOctet(writer, WireConstants.Int);
                    writer.Write(val);
                    break;
                case uint val:
                    WriteOctet(writer, WireConstants.Uint);
                    writer.Write(val);
                    break;
                case decimal val:
                    WriteOctet(writer, WireConstants.Decimal);
                    WriteDecimal(writer, val);
                    break;
                case AmqpTimestamp val:
                    WriteOctet(writer, WireConstants.Timestamp);
                    WriteTimestamp(writer, val);
                    break;
                case IDictionary val:
                    WriteOctet(writer, WireConstants.Dictionary);
                    WriteTable(writer, val);
                    break;
                case IList val:
                    WriteOctet(writer, WireConstants.List);
                    WriteArray(writer, val);
                    break;
                case byte val:
                    WriteOctet(writer, WireConstants.Byte);
                    writer.Write(val);
                    break;
                case sbyte val:
                    WriteOctet(writer, WireConstants.Sbyte);
                    writer.Write(val);
                    break;
                case double val:
                    WriteOctet(writer, WireConstants.Double);
                    writer.Write(val);
                    break;
                case float val:
                    WriteOctet(writer, WireConstants.Float);
                    writer.Write(val);
                    break;
                case long val:
                    WriteOctet(writer, WireConstants.Long);
                    writer.Write(val);
                    break;
                case short val:
                    WriteOctet(writer, WireConstants.Short);
                    writer.Write(val);
                    break;
                case bool val:
                    WriteOctet(writer, WireConstants.Bool);
                    WriteOctet(writer, (byte)(val ? 1 : 0));
                    break;
                case BinaryTableValue val:
                    WriteOctet(writer, WireConstants.TableValue);
                    WriteLongstr(writer, val.Bytes, val.Bytes.Length);
                    break;
                default:
                    throw new WireFormattingException(
                        $"Value of type '{value.GetType().Name}' cannot appear as table value",
                        value);
            }
        }

        public static void WriteLong(BinaryBufferWriter writer, uint val)
        {
            writer.Write(val);
        }

        public static void WriteLonglong(BinaryBufferWriter writer, ulong val)
        {
            writer.Write(val);
        }

#if NETSTANDARD2_1
        public static void WriteLongstr(BinaryBufferWriter writer, ReadOnlySpan<byte> val)
        {
            WriteLong(writer, (uint)val.Length);
            writer.Write(val);
        }

        public static void WriteLongstr(BinaryBufferWriter writer, string val)
        {
            int length = Encoding.UTF8.GetMaxByteCount(val.Length);
            Span<byte> buffer = writer.GetBuffer(length + sizeof(uint));
            int byteCount = Encoding.UTF8.GetBytes(val, buffer.Slice(sizeof(uint)));
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)byteCount);
            writer.Advance(byteCount + sizeof(uint));
        }
#else
        public static void WriteLongstr(BinaryBufferWriter writer, string val)
        {
            int maxLength = Encoding.UTF8.GetMaxByteCount(val.Length);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(maxLength);
            try
            {
                int bytesUsed = Encoding.UTF8.GetBytes(val, 0, val.Length, bytes, 0);
                WriteLong(writer, (uint)bytesUsed);
                writer.Write(bytes, 0, bytesUsed);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
#endif

        public static void WriteLongstr(BinaryBufferWriter writer, byte[] val, int length)
        {
            WriteLong(writer, (uint)length);
            writer.Write(val, 0, length);
        }


        public static void WriteOctet(BinaryBufferWriter writer, byte val)
        {
            writer.Write(val);
        }

        public static void WriteShort(BinaryBufferWriter writer, ushort val)
        {
            writer.Write(val);
        }

#if NETSTANDARD2_1
        public static void WriteShortstr(BinaryBufferWriter writer, string val)
        {
            int length = Encoding.UTF8.GetMaxByteCount(val.Length);
            Span<byte> buffer = writer.GetBuffer(length + sizeof(byte));
            
            int byteCount = Encoding.UTF8.GetBytes(val, buffer.Slice(sizeof(byte)));
            if (byteCount > 255)
            {
                throw new WireFormattingException("Short string too long; " +
                                                  "UTF-8 encoded length=" + length + ", max=255");
            }
            buffer[0] = (byte)byteCount;
            writer.Advance(byteCount + sizeof(byte));
        }
#else
        public static void WriteShortstr(BinaryBufferWriter writer, string val)
        {
            int length = Encoding.UTF8.GetMaxByteCount(val.Length);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(length + sizeof(byte));
            try
            {
                int bytesUsed = Encoding.UTF8.GetBytes(val, 0, val.Length, bytes, 0);
                if (bytesUsed > 255)
                {
                    throw new WireFormattingException("Short string too long; " +
                                                      "UTF-8 encoded length=" + bytesUsed + ", max=255");
                }
                writer.Write((byte)bytesUsed);
                writer.Write(new ReadOnlySpan<byte>( bytes, 0, bytesUsed));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

#endif

        ///<summary>Writes an AMQP "table" to the writer.</summary>
        ///<remarks>
        ///<para>
        /// In this method, we assume that the stream that backs our
        /// NetworkBinaryWriter is a positionable stream - which it is
        /// currently (see Frame.m_accumulator, Frame.GetWriter and
        /// Command.Transmit).
        ///</para>
        ///<para>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t
        /// x and V types and the AMQP 0-9-1 A type.
        ///</para>
        ///</remarks>
        public static void WriteTable(BinaryBufferWriter writer, IDictionary val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                Span<byte> buffer = writer.GetBufferWithAdvance(sizeof(uint));
                int patchPosition = writer.Position;

                foreach (DictionaryEntry entry in val)
                {
                    WriteShortstr(writer, entry.Key.ToString());
                    WriteFieldValue(writer, entry.Value);
                }

                // Now, backpatch the table length.
                int savedPosition = writer.Position;
                int tableLength = savedPosition - patchPosition;
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)tableLength);
            }
        }


        ///<summary>Writes an AMQP "table" to the writer.</summary>
        ///<remarks>
        ///<para>
        /// In this method, we assume that the stream that backs our
        /// NetworkBinaryWriter is a positionable stream - which it is
        /// currently (see Frame.m_accumulator, Frame.GetWriter and
        /// Command.Transmit).
        ///</para>
        ///<para>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t
        /// x and V types and the AMQP 0-9-1 A type.
        ///</para>
        ///</remarks>
        public static void WriteTable(BinaryBufferWriter writer, IDictionary<string, object> val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                Span<byte> buffer = writer.GetBufferWithAdvance(sizeof(uint));
                int patchPosition = writer.Position;

                foreach (var entry in val)
                {
                    WriteShortstr(writer, entry.Key);
                    WriteFieldValue(writer, entry.Value);
                }

                // Now, backpatch the table length.
                int savedPosition = writer.Position;
                int tableLength = savedPosition - patchPosition;
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)tableLength);
            }
        }

        public static void WriteTimestamp(BinaryBufferWriter writer, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            WriteLonglong(writer, (ulong)val.UnixTime);
        }
    }
}

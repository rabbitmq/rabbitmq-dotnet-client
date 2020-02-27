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
//  at http://www.mozilla.org/MPL/
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
using System.IO;
using System.Text;
using RabbitMQ.Client.Exceptions;
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

        public static IList ReadArray(NetworkBinaryReader reader)
        {
            IList array = new List<object>();
            long arrayLength = reader.ReadUInt32();
            var backingStream = reader.BaseStream;
            long startPosition = backingStream.Position;
            while ((backingStream.Position - startPosition) < arrayLength)
            {
                object value = ReadFieldValue(reader);
                array.Add(value);
            }
            return array;
        }

        public static decimal ReadDecimal(NetworkBinaryReader reader)
        {
            byte scale = ReadOctet(reader);
            uint unsignedMantissa = ReadLong(reader);
            return AmqpToDecimal(scale, unsignedMantissa);
        }

        public static object ReadFieldValue(NetworkBinaryReader reader)
        {
            object value = null;
            byte discriminator = reader.ReadByte();
            switch ((char)discriminator)
            {
                case 'S':
                    value = ReadLongstr(reader);
                    break;
                case 'I':
                    value = reader.ReadInt32();
                    break;
                case 'i':
                    value = reader.ReadUInt32();
                    break;
                case 'D':
                    value = ReadDecimal(reader);
                    break;
                case 'T':
                    value = ReadTimestamp(reader);
                    break;
                case 'F':
                    value = ReadTable(reader);
                    break;
                case 'A':
                    value = ReadArray(reader);
                    break;
                case 'B':
                    value = reader.ReadByte();
                    break;
                case 'b':
                    value = reader.ReadSByte();
                    break;
                case 'd':
                    value = reader.ReadDouble();
                    break;
                case 'f':
                    value = reader.ReadSingle();
                    break;
                case 'l':
                    value = reader.ReadInt64();
                    break;
                case 's':
                    value = reader.ReadInt16();
                    break;
                case 't':
                    value = (ReadOctet(reader) != 0);
                    break;
                case 'x':
                    value = new BinaryTableValue(ReadLongstr(reader));
                    break;
                case 'V':
                    value = null;
                    break;

                default:
                    throw new SyntaxError("Unrecognised type in table: " +
                                          (char)discriminator);
            }
            return value;
        }

        public static uint ReadLong(NetworkBinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        public static ulong ReadLonglong(NetworkBinaryReader reader)
        {
            return reader.ReadUInt64();
        }

        public static byte[] ReadLongstr(NetworkBinaryReader reader)
        {
            uint byteCount = reader.ReadUInt32();
            if (byteCount > int.MaxValue)
            {
                throw new SyntaxError("Long string too long; " +
                                      "byte length=" + byteCount + ", max=" + int.MaxValue);
            }
            return reader.ReadBytes((int)byteCount);
        }

        public static byte ReadOctet(NetworkBinaryReader reader)
        {
            return reader.ReadByte();
        }

        public static ushort ReadShort(NetworkBinaryReader reader)
        {
            return reader.ReadUInt16();
        }

        public static string ReadShortstr(NetworkBinaryReader reader)
        {
            int byteCount = reader.ReadByte();
            byte[] readBytes = reader.ReadBytes(byteCount);
            return Encoding.UTF8.GetString(readBytes, 0, readBytes.Length);
        }

        ///<summary>Reads an AMQP "table" definition from the reader.</summary>
        ///<remarks>
        /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
        /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t,
        /// x and V types and the AMQP 0-9-1 A type.
        ///</remarks>
        /// <returns>A <seealso cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>.</returns>
        public static IDictionary<string, object> ReadTable(NetworkBinaryReader reader)
        {
            IDictionary<string, object> table = new Dictionary<string, object>();
            long tableLength = reader.ReadUInt32();

            var backingStream = reader.BaseStream;
            long startPosition = backingStream.Position;
            while ((backingStream.Position - startPosition) < tableLength)
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

        public static AmqpTimestamp ReadTimestamp(NetworkBinaryReader reader)
        {
            ulong stamp = ReadLonglong(reader);
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentWriter.WriteTimestamp and AmqpTimestamp itself
            return new AmqpTimestamp((long)stamp);
        }

        public static void WriteArray(NetworkBinaryWriter writer, IList val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                var backingStream = writer.BaseStream;
                long patchPosition = backingStream.Position;
                writer.Write((uint)0); // length of table - will be backpatched
                foreach (object entry in val)
                {
                    WriteFieldValue(writer, entry);
                }
                long savedPosition = backingStream.Position;
                long tableLength = savedPosition - patchPosition - 4; // offset for length word
                backingStream.Seek(patchPosition, SeekOrigin.Begin);
                writer.Write((uint)tableLength);
                backingStream.Seek(savedPosition, SeekOrigin.Begin);
            }
        }

        public static void WriteDecimal(NetworkBinaryWriter writer, decimal value)
        {
            DecimalToAmqp(value, out var scale, out var mantissa);
            WriteOctet(writer, scale);
            WriteLong(writer, (uint)mantissa);
        }

        public static void WriteFieldValue(NetworkBinaryWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    WriteOctet(writer, (byte)'V');
                    break;
                case string val:
                    WriteOctet(writer, (byte)'S');
                    WriteLongstr(writer, Encoding.UTF8.GetBytes(val));
                    break;
                case byte[] val:
                    WriteOctet(writer, (byte)'S');
                    WriteLongstr(writer, val);
                    break;
                case int val:
                    WriteOctet(writer, (byte)'I');
                    writer.Write(val);
                    break;
                case uint val:
                    WriteOctet(writer, (byte)'i');
                    writer.Write(val);
                    break;
                case decimal val:
                    WriteOctet(writer, (byte)'D');
                    WriteDecimal(writer, val);
                    break;
                case AmqpTimestamp val:
                    WriteOctet(writer, (byte)'T');
                    WriteTimestamp(writer, val);
                    break;
                case IDictionary val:
                    WriteOctet(writer, (byte)'F');
                    WriteTable(writer, val);
                    break;
                case IList val:
                    WriteOctet(writer, (byte)'A');
                    WriteArray(writer, val);
                    break;
                case byte val:
                    WriteOctet(writer, (byte)'B');
                    writer.Write(val);
                    break;
                case sbyte val:
                    WriteOctet(writer, (byte)'b');
                    writer.Write(val);
                    break;
                case double val:
                    WriteOctet(writer, (byte)'d');
                    writer.Write(val);
                    break;
                case float val:
                    WriteOctet(writer, (byte)'f');
                    writer.Write(val);
                    break;
                case long val:
                    WriteOctet(writer, (byte)'l');
                    writer.Write(val);
                    break;
                case short val:
                    WriteOctet(writer, (byte)'s');
                    writer.Write(val);
                    break;
                case bool val:
                    WriteOctet(writer, (byte)'t');
                    WriteOctet(writer, (byte)(val ? 1 : 0));
                    break;
                case BinaryTableValue val:
                    WriteOctet(writer, (byte)'x');
                    WriteLongstr(writer, val.Bytes);
                    break;
                default:
                    throw new WireFormattingException(
                        $"Value of type '{value.GetType().Name}' cannot appear as table value",
                        value);
            }
        }

        public static void WriteLong(NetworkBinaryWriter writer, uint val)
        {
            writer.Write(val);
        }

        public static void WriteLonglong(NetworkBinaryWriter writer, ulong val)
        {
            writer.Write(val);
        }

        public static void WriteLongstr(NetworkBinaryWriter writer, byte[] val)
        {
            WriteLong(writer, (uint)val.Length);
            writer.Write(val);
        }

        public static void WriteOctet(NetworkBinaryWriter writer, byte val)
        {
            writer.Write(val);
        }

        public static void WriteShort(NetworkBinaryWriter writer, ushort val)
        {
            writer.Write(val);
        }

        public static void WriteShortstr(NetworkBinaryWriter writer, string val)
        {
            var bytes = Encoding.UTF8.GetBytes(val);
            var length = bytes.Length;
            
            if (length > 255)
            {
                throw new WireFormattingException("Short string too long; " +
                                                  "UTF-8 encoded length=" + length + ", max=255");
            }

            writer.Write((byte)length);
            writer.Write(bytes);
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
        public static void WriteTable(NetworkBinaryWriter writer, IDictionary val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                var backingStream = writer.BaseStream;
                long patchPosition = backingStream.Position;
                writer.Write((uint)0); // length of table - will be backpatched

                foreach (DictionaryEntry entry in val)
                {
                    WriteShortstr(writer, entry.Key.ToString());
                    WriteFieldValue(writer, entry.Value);
                }

                // Now, backpatch the table length.
                long savedPosition = backingStream.Position;
                long tableLength = savedPosition - patchPosition - 4; // offset for length word
                backingStream.Seek(patchPosition, SeekOrigin.Begin);
                writer.Write((uint)tableLength);
                backingStream.Seek(savedPosition, SeekOrigin.Begin);
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
        public static void WriteTable(NetworkBinaryWriter writer, IDictionary<string, object> val)
        {
            if (val == null)
            {
                writer.Write((uint)0);
            }
            else
            {
                var backingStream = writer.BaseStream;
                long patchPosition = backingStream.Position;
                writer.Write((uint)0); // length of table - will be backpatched

                foreach (var entry in val)
                {
                    WriteShortstr(writer, entry.Key);
                    WriteFieldValue(writer, entry.Value);
                }

                // Now, backpatch the table length.
                long savedPosition = backingStream.Position;
                long tableLength = savedPosition - patchPosition - 4; // offset for length word
                backingStream.Seek(patchPosition, SeekOrigin.Begin);
                writer.Write((uint)tableLength);
                backingStream.Seek(savedPosition, SeekOrigin.Begin);
            }
        }

        public static void WriteTimestamp(NetworkBinaryWriter writer, AmqpTimestamp val)
        {
            // 0-9 is afaict silent on the signedness of the timestamp.
            // See also MethodArgumentReader.ReadTimestamp and AmqpTimestamp itself
            WriteLonglong(writer, (ulong)val.UnixTime);
        }
    }
}

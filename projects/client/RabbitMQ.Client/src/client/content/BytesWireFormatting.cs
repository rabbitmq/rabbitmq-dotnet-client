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
using System.Text;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Content
{
    /// <summary>
    /// Internal support class for use in reading and
    /// writing information binary-compatible with QPid's "BytesMessage" wire encoding.
    /// </summary>
    public static class BytesWireFormatting
    {
        public static int Read(BinaryBufferReader reader, byte[] target, int offset, int count)
        {
            return reader.ReadBytes(new Span<byte>(target, offset, count));
        }

        public static byte ReadByte(BinaryBufferReader reader)
        {
            return reader.ReadByte();
        }

        public static byte[] ReadBytes(BinaryBufferReader reader, int count)
        {
            return reader.ReadBytes(count);
        }

        public static char ReadChar(BinaryBufferReader reader)
        {
            return (char)reader.ReadUInt16();
        }

        public static double ReadDouble(BinaryBufferReader reader)
        {
            return reader.ReadDouble();
        }

        public static short ReadInt16(BinaryBufferReader reader)
        {
            return reader.ReadInt16();
        }

        public static int ReadInt32(BinaryBufferReader reader)
        {
            return reader.ReadInt32();
        }

        public static long ReadInt64(BinaryBufferReader reader)
        {
            return reader.ReadInt64();
        }

        public static float ReadSingle(BinaryBufferReader reader)
        {
            return reader.ReadSingle();
        }

        public static string ReadString(BinaryBufferReader reader)
        {
            ushort length = reader.ReadUInt16();

            string returnValue = null;
            byte[] bytes = null;
            try
            {
#if NETSTANDARD2_1
                Span<byte> span = length < 512 ? stackalloc byte[length] : (bytes = ArrayPool<byte>.Shared.Rent(length));
                reader.ReadBytes(span);
                returnValue = Encoding.UTF8.GetString(span);
#else
                bytes = ArrayPool<byte>.Shared.Rent(length);
                reader.ReadBytes(new Span<byte>(bytes, 0, length));
                returnValue = Encoding.UTF8.GetString(bytes, 0, length);
#endif
            }
            finally
            {
                if (bytes != null)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }

            return returnValue;
        }


        public static void Write(BinaryBufferWriter writer, byte[] source, int offset, int count)
        {
            writer.Write(source, offset, count);
        }

        public static void WriteByte(BinaryBufferWriter writer, byte value)
        {
            writer.Write(value);
        }

        public static void WriteBytes(BinaryBufferWriter writer, byte[] source)
        {
            Write(writer, source, 0, source.Length);
        }

        public static void WriteChar(BinaryBufferWriter writer, char value)
        {
            writer.Write((ushort)value);
        }

        public static void WriteDouble(BinaryBufferWriter writer, double value)
        {
            writer.Write(value);
        }

        public static void WriteInt16(BinaryBufferWriter writer, short value)
        {
            writer.Write(value);
        }

        public static void WriteInt32(BinaryBufferWriter writer, int value)
        {
            writer.Write(value);
        }

        public static void WriteInt64(BinaryBufferWriter writer, long value)
        {
            writer.Write(value);
        }

        public static void WriteSingle(BinaryBufferWriter writer, float value)
        {
            writer.Write(value);
        }

        public static void WriteString(BinaryBufferWriter writer, string value)
        {
#if NETSTANDARD2_1
            byte[] bytes = null;
            int length = value.Length; 
            try
            {
                // actually max array length is 1024 bytes
                Span<byte> span = length < 512 ? stackalloc byte[length] : (bytes = ArrayPool<byte>.Shared.Rent(length));
                int byteCount = Encoding.UTF8.GetBytes(value, span);
                writer.Write((ushort)byteCount);
                writer.Write(span.Slice(0, byteCount));
            }
            finally
            {
                if (bytes != null)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
#else
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
#endif

        }
    }
}

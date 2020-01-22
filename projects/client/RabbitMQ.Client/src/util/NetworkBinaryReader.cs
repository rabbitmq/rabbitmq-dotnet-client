// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security;
using System.Text;

namespace RabbitMQ.Util
{
    /// <summary>
    /// Subclass of BinaryReader that reads integers etc in correct network order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kludge to compensate for .NET's broken little-endian-only BinaryReader.
    /// Relies on BinaryReader always being little-endian.
    /// </para>
    /// </remarks>
    public class NetworkBinaryReader : BinaryReader
    {
        private static readonly Encoding encoding = new UTF8Encoding();

        // Not particularly efficient. To be more efficient, we could
        // reuse BinaryReader's implementation details: m_buffer and
        // FillBuffer, if they weren't private
        // members. Private/protected claim yet another victim, film
        // at 11. (I could simply cut-n-paste all that good code from
        // BinaryReader, but two wrongs do not make a right)

        /// <summary>
        /// Construct a NetworkBinaryReader over the given input stream.
        /// </summary>
        public NetworkBinaryReader(Stream input) : base(input, encoding)
        {
        }

        /// <summary>
        /// Construct a NetworkBinaryReader over the given input
        /// stream, reading strings using the given encoding.
        /// </summary>
        public NetworkBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override double ReadDouble()
        {
            byte[] bytes = ReadBytes(8);
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(bytes)));
            }
            else
            {
                return BitConverter.ToDouble(bytes, 0);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override short ReadInt16()
        {
            return BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(ReadBytes(2)));
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override int ReadInt32()
        {
            return BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(ReadBytes(4)));
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override long ReadInt64()
        {
            return BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(ReadBytes(8)));
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override float ReadSingle()
        {
            byte[] bytes = ReadBytes(4);
            byte temp = bytes[0];
            bytes[0] = bytes[3];
            bytes[3] = temp;
            temp = bytes[1];
            bytes[1] = bytes[2];
            bytes[2] = temp;
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override ushort ReadUInt16()
        {
            return BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(ReadBytes(2)));
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override uint ReadUInt32()
        {
            return BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(ReadBytes(4)));
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override ulong ReadUInt64()
        {
            return BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(ReadBytes(8)));
        }
    }
}

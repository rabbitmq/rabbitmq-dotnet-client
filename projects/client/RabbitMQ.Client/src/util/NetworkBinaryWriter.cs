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
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace RabbitMQ.Util
{
    /// <summary>
    /// Subclass of BinaryWriter that writes integers etc in correct network order.
    /// </summary>
    ///
    /// <remarks>
    /// <p>
    /// Kludge to compensate for .NET's broken little-endian-only BinaryWriter.
    /// </p><p>
    /// See also NetworkBinaryReader.
    /// </p>
    /// </remarks>
    public class NetworkBinaryWriter : BinaryWriter
    {
        private static readonly Encoding encoding = new UTF8Encoding(false, true);

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input stream.
        /// </summary>
        public NetworkBinaryWriter(Stream output) : base(output, encoding)
        {
        }

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input
        /// stream, reading strings using the given encoding.
        /// </summary>
        public NetworkBinaryWriter(Stream output, Encoding encoding) : base(output, encoding)
        {
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(short i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(2);
            BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(0, 2), i);
            Write(bytes, 0, 2);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(ushort i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(2);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0, 2), i);
            Write(bytes, 0, 2);
            ArrayPool<byte>.Shared.Return(bytes);

        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(int i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(4);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), i);
            Write(bytes, 0, 4);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(uint i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(4);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), i);
            Write(bytes, 0, 4);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(long i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(8);
            BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(0, 8), i);
            Write(bytes, 0, 8);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(ulong i)
        {
            byte[] bytes = ArrayPool<byte>.Shared.Rent(8);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(0, 8), i);
            Write(bytes, 0, 8);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(float f)
        {
            byte[] bytes;
            if (BitConverter.IsLittleEndian)
            {
                bytes = BitConverter.GetBytes(f);
                byte temp = bytes[0];
                bytes[0] = bytes[3];
                bytes[3] = temp;
                temp = bytes[1];
                bytes[1] = bytes[2];
                bytes[2] = temp;
                Write(bytes);
            }
            else
            {
                Write(BitConverter.GetBytes(f));
            }
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(double d)
        {
            byte[] bytes;
            if (BitConverter.IsLittleEndian)
            {
                bytes = BitConverter.GetBytes(d);
                byte temp = bytes[0];
                bytes[0] = bytes[7];
                bytes[7] = temp;
                temp = bytes[1];
                bytes[1] = bytes[6];
                bytes[6] = temp;
                temp = bytes[2];
                bytes[2] = bytes[5];
                bytes[5] = temp;
                temp = bytes[3];
                bytes[3] = bytes[4];
                bytes[4] = temp;
                Write(bytes);
            }
            else
            {
                Write(BitConverter.GetBytes(d));
            }
        }
    }
}

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
using System.IO;
using System.Runtime.InteropServices;
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
    class NetworkBinaryReader : BinaryReader
    {
        // Not particularly efficient. To be more efficient, we could
        // reuse BinaryReader's implementation details: m_buffer and
        // FillBuffer, if they weren't private
        // members. Private/protected claim yet another victim, film
        // at 11. (I could simply cut-n-paste all that good code from
        // BinaryReader, but two wrongs do not make a right)

        /// <summary>
        /// Construct a NetworkBinaryReader over the given input stream.
        /// </summary>
        public NetworkBinaryReader(Stream input) : base(input, Encoding.UTF8)
        {
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override double ReadDouble()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(8))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 8);
                Read(slice);
                return NetworkOrderDeserializer.ReadDouble(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override short ReadInt16()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(2))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 2);
                Read(slice);
                return NetworkOrderDeserializer.ReadInt16(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override int ReadInt32()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(4))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 4);
                Read(slice);
                return NetworkOrderDeserializer.ReadInt32(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override long ReadInt64()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(8))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 8);
                Read(slice);
                return NetworkOrderDeserializer.ReadInt64(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override float ReadSingle()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(4))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 4);
                Read(slice);
                return NetworkOrderDeserializer.ReadSingle(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override ushort ReadUInt16()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(2))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 2);
                Read(slice);
                return NetworkOrderDeserializer.ReadUInt16(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override uint ReadUInt32()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(4))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 4);
                Read(slice);
                return NetworkOrderDeserializer.ReadUInt32(slice);
            }
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public override ulong ReadUInt64()
        {
            using (IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(8))
            {
                Memory<byte> slice = memory.Memory.Slice(0, 8);
                Read(slice);
                return NetworkOrderDeserializer.ReadUInt64(slice);
            }
        }

        public int Read(Memory<byte> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
            {
                int numRead = Read(segment.Array, segment.Offset, segment.Count);
                if (numRead > segment.Count)
                {
                    throw new IOException("Read to far :(");
                }

                return numRead;
            }

            throw new IOException("Unable to get array from memory.");
        }
    }
}

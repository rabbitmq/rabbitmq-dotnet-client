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
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
    public class NetworkBinaryReader 
    {
        private readonly Stream input;

        public long Position { get { return input.Position; } }

        public NetworkBinaryReader(Stream input) 
        {
            this.input = input;
        }
        
        #region Sync

        public double ReadDouble()
        {
            byte[] bytes = new byte[8];
            input.Read(bytes, 0, 8);
            return BitConverter.ToDouble(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public short ReadInt16()
        {
            byte[] bytes = new byte[2];
            input.Read(bytes, 0, 2);
            return BitConverter.ToInt16(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }

        public byte[] ReadBytes(int size)
        {
            byte[] bytes = new byte[size];
            input.Read(bytes, 0, size);
            return bytes;
        }

        public int ReadInt32()
        {
            byte[] bytes = new byte[4];
            input.Read(bytes, 0, 4);
            return BitConverter.ToInt32(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public long ReadInt64()
        {
            byte[] bytes = new byte[8];
            input.Read(bytes, 0, 8);
            return BitConverter.ToInt64(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public int ReadByte()
        {
            return input.ReadByte();
        }

        public float ReadSingle()
        {
            byte[] bytes = new byte[4];
            input.Read(bytes, 0, 4);
            return BitConverter.ToSingle(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public ushort ReadUInt16()
        {
            byte[] bytes = new byte[2];
            input.Read(bytes, 0, 2);
            return BitConverter.ToUInt16(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }
        public char ReadChar()
        {
            byte[] bytes = new byte[2];
            input.Read(bytes, 0, 2);
            return BitConverter.ToChar(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }

        public uint ReadUInt32()
        {
            byte[] bytes = new byte[4];
            input.Read(bytes, 0, 4);
            return BitConverter.ToUInt32(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public ulong ReadUInt64()
        {
            byte[] bytes = new byte[8];
            input.Read(bytes, 0, 8);
            return BitConverter.ToUInt64(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public int Read(byte[] target, int offset, int count)
        {
            return input.Read(target, offset, count);   
        }

        #endregion

        #region A-Sync

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<double> ReadDoubleAsync()
        {
            byte[] bytes = new byte[8];
            await input.ReadAsync(bytes, 0, 8);
            return BitConverter.ToDouble(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<short> ReadInt16Async()
        {
            byte[] bytes = new byte[2];
            await input.ReadAsync(bytes, 0, 2);
            return BitConverter.ToInt16(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }
        public async Task<char> ReadCharAsync()
        {
            byte[] bytes = new byte[2];
            await input.ReadAsync(bytes, 0, 2);
            return BitConverter.ToChar(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }

        public async Task<byte[]> ReadBytesAsync(int size)
        {
            byte[] bytes = new byte[size];
            await input.ReadAsync(bytes, 0, size);
            return bytes;
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<int> ReadInt32Async()
        {
            byte[] bytes = new byte[4];
            await input.ReadAsync(bytes, 0, 4);
            return BitConverter.ToInt32(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<long> ReadInt64Async()
        {
            byte[] bytes = new byte[8];
            await input.ReadAsync(bytes, 0, 8);
            return BitConverter.ToInt64(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        internal async Task<sbyte> ReadSByteAsync()
        {
            byte[] bytes = new byte[1];
            await input.ReadAsync(bytes, 0, 1);
            return (sbyte)bytes[0];
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<float> ReadSingleAsync()
        {
            byte[] bytes = new byte[4];
            await input.ReadAsync(bytes, 0, 4);
            return BitConverter.ToSingle(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<ushort> ReadUInt16Async()
        {
            byte[] bytes = new byte[2];
            await input.ReadAsync(bytes, 0, 2);
            return BitConverter.ToUInt16(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<uint> ReadUInt32Async()
        {
            byte[] bytes = new byte[4];
            await input.ReadAsync(bytes, 0, 4);
            return BitConverter.ToUInt32(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        /// <summary>
        /// Override BinaryReader's method for network-order.
        /// </summary>
        public async Task<ulong> ReadUInt64Async()
        {
            byte[] bytes = new byte[8];
            await input.ReadAsync(bytes, 0, 8);
            return BitConverter.ToUInt64(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0);
        }

        public Task<int> ReadAsync(byte[] target, int offset, int count)
        {
            return input.ReadAsync(target, offset, count);
        }

        #endregion
    }
}

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
    public class NetworkBinaryWriter 
    {
        private readonly Stream output;
        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input stream.
        /// </summary>
        public NetworkBinaryWriter(Stream output) //: base(output)
        {
            this.output = output;
        }

        public long Position { get { return output.Position; } }

        internal void Flush()
        {
            output.Flush();
        }

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input
        /// stream, reading strings using the given encoding.
        /// </summary>
        //public NetworkBinaryWriter(Stream output, Encoding encoding) : base(output, encoding)
        //{
        //}

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteInt16(short i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0, 2);
        }
        public void Write(byte[] i, int offset, int length)
        {
            output.Write(i, offset, length);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteUShort(ushort i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0, 2);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteInt32(int i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 4);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteUInt32(uint i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 4);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteInt64(long i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 8);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteUInt64(ulong i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 8);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteSingle(float i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 4);
        }
        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public void WriteDouble(double i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[8] { bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0] } : bytes, 0, 8);
        }

        internal void Seek(long patchPosition, SeekOrigin begin)
        {
            output.Seek(patchPosition, begin);
        }

        internal void WriteSByte(sbyte value)
        {
            output.WriteByte((byte)value);
        }

        internal void WriteByte(byte val)
        {
            output.WriteByte(val);
        }

        internal void WriteUInt16(ushort i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0, 2);
        }
        internal void WriteChar(char i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            output.Write(BitConverter.IsLittleEndian ? new byte[2] { bytes[1], bytes[0] } : bytes, 0, 2);
        }
    }
}

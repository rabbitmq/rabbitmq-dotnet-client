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
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading.Tasks;

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
        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input stream.
        /// </summary>
        public NetworkBinaryWriter(Stream output) : base(output)
        {
        }

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input
        /// stream, reading strings using the given encoding.
        /// </summary>
        public NetworkBinaryWriter(Stream output, Encoding encoding) : base(output, encoding)
        {
        }

        ///<summary>Helper method for constructing a temporary
        ///BinaryWriter streaming into a fresh MemoryStream
        ///provisioned with the given initialSize.</summary>
        public static BinaryWriter TemporaryBinaryWriter(int initialSize)
        {
            return new BinaryWriter(new MemoryStream(initialSize));
        }

        ///<summary>Helper method for extracting the byte[] contents
        ///of a BinaryWriter over a MemoryStream, such as constructed
        ///by TemporaryBinaryWriter.</summary>
        public static byte[] TemporaryContents(BinaryWriter w)
        {
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(short i)
        {
            Write((byte)((i & 0xFF00) >> 8));
            Write((byte)(i & 0x00FF));
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(ushort i)
        {
            Write((byte)((i & 0xFF00) >> 8));
            Write((byte)(i & 0x00FF));
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(int i)
        {
            Write((byte)((i & 0xFF000000) >> 24));
            Write((byte)((i & 0x00FF0000) >> 16));
            Write((byte)((i & 0x0000FF00) >> 8));
            Write((byte)(i & 0x000000FF));
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(uint i)
        {
            Write((byte)((i & 0xFF000000) >> 24));
            Write((byte)((i & 0x00FF0000) >> 16));
            Write((byte)((i & 0x0000FF00) >> 8));
            Write((byte)(i & 0x000000FF));
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(long i)
        {
            var i1 = (uint)(i >> 32);
            var i2 = (uint)i;
            Write(i1);
            Write(i2);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(ulong i)
        {
            var i1 = (uint)(i >> 32);
            var i2 = (uint)i;
            Write(i1);
            Write(i2);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(float f)
        {
            BinaryWriter w = TemporaryBinaryWriter(4);
            w.Write(f);
            byte[] wrongBytes = TemporaryContents(w);
            Write(wrongBytes[3]);
            Write(wrongBytes[2]);
            Write(wrongBytes[1]);
            Write(wrongBytes[0]);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public override void Write(double d)
        {
            BinaryWriter w = TemporaryBinaryWriter(8);
            w.Write(d);
            byte[] wrongBytes = TemporaryContents(w);
            Write(wrongBytes[7]);
            Write(wrongBytes[6]);
            Write(wrongBytes[5]);
            Write(wrongBytes[4]);
            Write(wrongBytes[3]);
            Write(wrongBytes[2]);
            Write(wrongBytes[1]);
            Write(wrongBytes[0]);
        }
    }

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
    [Serializable]
    public class AsyncNetworkBinaryWriter : IDisposable
    {
        public static readonly AsyncNetworkBinaryWriter Null = new AsyncNetworkBinaryWriter();

        protected Stream OutStream;
        private byte[] _buffer;    // temp space for writing primitives to.
        private Encoding _encoding;
        private Encoder _encoder;

        [OptionalField]  // New in .NET FX 4.5.  False is the right default value.
        private bool _leaveOpen;

        // This field should never have been serialized and has not been used since before v2.0.
        // However, this type is serializable, and we need to keep the field name around when deserializing.
        // Also, we'll make .NET FX 4.5 not break if it's missing.
#pragma warning disable 169
        [OptionalField]
        private char[] _tmpOneCharBuffer;
#pragma warning restore 169

        // Perf optimization stuff
        private byte[] _largeByteBuffer;  // temp space for writing chars.
        private int _maxChars;   // max # of chars we can put in _largeByteBuffer
        // Size should be around the max number of chars/string * Encoding's max bytes/char
        private const int LargeByteBufferSize = 256;

        protected AsyncNetworkBinaryWriter()
        {
            OutStream = Stream.Null;
            _buffer = new byte[16];
            _encoding = new UTF8Encoding(false, true);
            _encoder = _encoding.GetEncoder();
        }

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input stream.
        /// </summary>
        public AsyncNetworkBinaryWriter(Stream output) : this(output, new UTF8Encoding(false, true), false)
        {
        }

        /// <summary>
        /// Construct a NetworkBinaryWriter over the given input
        /// stream, reading strings using the given encoding.
        /// </summary>
        public AsyncNetworkBinaryWriter(Stream output, Encoding encoding) : this(output, encoding, false)
        {
        }

        public AsyncNetworkBinaryWriter(Stream output, Encoding encoding, bool leaveOpen)
        {
            OutStream = output;
            _buffer = new byte[16];
            _encoding = encoding;
            _encoder = _encoding.GetEncoder();
            _leaveOpen = leaveOpen;
        }

        // Closes this writer and releases any system resources associated with the
        // writer. Following a call to Close, any operations on the writer
        // may raise exceptions. 
        public virtual Task Close()
        {
            if (_leaveOpen)
            {
                return OutStream.FlushAsync();
            }
            else
            {
                OutStream.Close();
                return Task.FromResult(0);
            }
        }

        public virtual Task Flush()
        {
            return OutStream.FlushAsync();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_leaveOpen)
                    OutStream.Flush();
                else
                    OutStream.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /*
         * Returns the stream associate with the writer. It flushes all pending
         * writes before returning. All subclasses should override Flush to
         * ensure that all buffered data is sent to the stream.
         */
        public virtual Stream BaseStream
        {
            get
            {
                OutStream.Flush();
                return OutStream;
            }
        }

        ///<summary>Helper method for constructing a temporary
        ///BinaryWriter streaming into a fresh MemoryStream
        ///provisioned with the given initialSize.</summary>
        private static AsyncNetworkBinaryWriter TemporaryWriter(int initialSize)
        {
            return new AsyncNetworkBinaryWriter(new MemoryStream(initialSize));
        }

        ///<summary>Helper method for extracting the byte[] contents
        ///of a BinaryWriter over a MemoryStream, such as constructed
        ///by TemporaryWriter.</summary>
        private static byte[] TemporaryContents(AsyncNetworkBinaryWriter w)
        {
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(short i)
        {
            await WriteAsync((byte)((i & 0xFF00) >> 8)).ConfigureAwait(false);
            await WriteAsync((byte)(i & 0x00FF)).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(ushort i)
        {
            await WriteAsync((byte)((i & 0xFF00) >> 8)).ConfigureAwait(false);
            await WriteAsync((byte)(i & 0x00FF)).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(int i)
        {
            await WriteAsync((byte)((i & 0xFF000000) >> 24)).ConfigureAwait(false);
            await WriteAsync((byte)((i & 0x00FF0000) >> 16)).ConfigureAwait(false);
            await WriteAsync((byte)((i & 0x0000FF00) >> 8)).ConfigureAwait(false);
            await WriteAsync((byte)(i & 0x000000FF)).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(uint i)
        {
            await WriteAsync((byte)((i & 0xFF000000) >> 24)).ConfigureAwait(false);
            await WriteAsync((byte)((i & 0x00FF0000) >> 16)).ConfigureAwait(false);
            await WriteAsync((byte)((i & 0x0000FF00) >> 8)).ConfigureAwait(false);
            await WriteAsync((byte)(i & 0x000000FF)).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(long i)
        {
            var i1 = (uint)(i >> 32);
            var i2 = (uint)i;
            await WriteAsync(i1).ConfigureAwait(false);
            await WriteAsync(i2).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(ulong i)
        {
            var i1 = (uint)(i >> 32);
            var i2 = (uint)i;
            await WriteAsync(i1).ConfigureAwait(false);
            await WriteAsync(i2).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(float f)
        {
            AsyncNetworkBinaryWriter w = TemporaryWriter(4);
            await w.WriteAsync(f).ConfigureAwait(false);
            byte[] wrongBytes = TemporaryContents(w);
            await WriteAsync(wrongBytes[3]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[2]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[1]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[0]).ConfigureAwait(false);
        }

        /// <summary>
        /// Override BinaryWriter's method for network-order.
        /// </summary>
        public async Task Write(double d)
        {
            AsyncNetworkBinaryWriter w = TemporaryWriter(8);
            await w.WriteAsync(d);
            byte[] wrongBytes = TemporaryContents(w);
            await WriteAsync(wrongBytes[7]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[6]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[5]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[4]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[3]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[2]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[1]).ConfigureAwait(false);
            await WriteAsync(wrongBytes[0]).ConfigureAwait(false);
        }

        public Task Write(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            return OutStream.WriteAsync(buffer, 0, buffer.Length);
        }

        public virtual Task Write(byte[] buffer, int index, int count)
        {
            return OutStream.WriteAsync(buffer, index, count);
        }

        // Writes a four-byte unsigned integer to this stream. The current position
        // of the stream is advanced by four.
        // 
        Task WriteAsync(uint value)
        {
            _buffer[0] = (byte)value;
            _buffer[1] = (byte)(value >> 8);
            _buffer[2] = (byte)(value >> 16);
            _buffer[3] = (byte)(value >> 24);
            return OutStream.WriteAsync(_buffer, 0, 4);
        }

        Task WriteAsync(byte value)
        {
            byte[] oneByteArray = new byte[1];
            oneByteArray[0] = value;
            return OutStream.WriteAsync(oneByteArray, 0, 1);
        }

        // Writes a float to this stream. The current position of the stream is
        // advanced by four.
        // 
        private Task WriteAsync(float value)
        {
            uint TmpValue = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            _buffer[0] = (byte)TmpValue;
            _buffer[1] = (byte)(TmpValue >> 8);
            _buffer[2] = (byte)(TmpValue >> 16);
            _buffer[3] = (byte)(TmpValue >> 24);
            return OutStream.WriteAsync(_buffer, 0, 4);
        }

        private Task WriteAsync(double value)
        {
            ulong TmpValue = BitConverter.ToUInt64(BitConverter.GetBytes(value), 0);
            _buffer[0] = (byte)TmpValue;
            _buffer[1] = (byte)(TmpValue >> 8);
            _buffer[2] = (byte)(TmpValue >> 16);
            _buffer[3] = (byte)(TmpValue >> 24);
            _buffer[4] = (byte)(TmpValue >> 32);
            _buffer[5] = (byte)(TmpValue >> 40);
            _buffer[6] = (byte)(TmpValue >> 48);
            _buffer[7] = (byte)(TmpValue >> 56);
            return OutStream.WriteAsync(_buffer, 0, 8);
        }
    }
}

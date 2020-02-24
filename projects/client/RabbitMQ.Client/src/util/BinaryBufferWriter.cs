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
using System.Runtime.CompilerServices;

namespace RabbitMQ.Util
{
    public class BinaryBufferWriter : IDisposable
    {
        private byte[] _buffer;
        private const int DEFAULT_BUFFER_SIZE = 4096;

        public BinaryBufferWriter(int initialSizeHint = DEFAULT_BUFFER_SIZE)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(initialSizeHint);
        }

        private Span<byte> span => _buffer.AsSpan(Position);

        public ReadOnlySpan<byte> Buffer => _buffer.AsSpan(0, Position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapasity(int count)
        {
            if (Position + count > _buffer.Length)
            {
                Resize();
            }
        }
        
        private void Resize()
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);

            _buffer.AsSpan().CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }

        public int Position { get; private set; }

        public Span<byte> GetBuffer(int count)
        {
            EnsureCapasity(count);
            var buffer = span.Slice(0, count);
            return buffer;
        }

        public Span<byte> GetBufferWithAdvance(int count)
        {
            EnsureCapasity(count);
            var buffer = span.Slice(0, count);
            Advance(count);
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            Position += count;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        public void Write(byte i)
        {
            int size = sizeof(byte);
            EnsureCapasity(size);
            span[0] = i;
            Advance(size);
        }

        public void Write(sbyte i)
        {
            Write((byte)i);
        }

        public void Write(short i)
        {
            var size = sizeof(short);
            EnsureCapasity(size);
            BinaryPrimitives.WriteInt16BigEndian(span, i);
            Advance(size);
        }

        public void Write(ushort i)
        {
            var size = sizeof(ushort);
            EnsureCapasity(size);
            BinaryPrimitives.WriteUInt16BigEndian(span, i);
            Advance(size);
        }

        public void Write(int i)
        {
            var size = sizeof(int);
            EnsureCapasity(size);
            BinaryPrimitives.WriteInt32BigEndian(span, i);
            Advance(size);
        }

        public void Write(uint i)
        {
            var size = sizeof(uint);
            EnsureCapasity(size);
            BinaryPrimitives.WriteUInt32BigEndian(span, i);
            Advance(size);
        }

        public void Write(long i)
        {
            var size = sizeof(long);
            EnsureCapasity(size);
            BinaryPrimitives.WriteInt64BigEndian(span, i);
            Advance(size);
        }

        public void Write(ulong i)
        {
            var size = sizeof(ulong);
            EnsureCapasity(size);
            BinaryPrimitives.WriteUInt64BigEndian(span, i);
            Advance(size);
        }

#if NETSTANDARD2_1
        public void Write(float f)
        {
            int size = sizeof(float);
            EnsureCapasity(size);
            BitConverter.TryWriteBytes(span, f);
            if (BitConverter.IsLittleEndian)
            {
                span.Slice(0, size).Reverse();
            }
            Advance(size);
        }

        public void Write(double d)
        {
            int size = sizeof(double);
            EnsureCapasity(size);
            BitConverter.TryWriteBytes(span, d);
            if (BitConverter.IsLittleEndian)
            {
                span.Slice(0, size).Reverse();
            }

            Advance(size);
        }
#else
        public void Write(float f)
        {
            int size = sizeof(float);
            EnsureCapasity(size);
            Span<byte> bytes = BitConverter.GetBytes(f);
            if (BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }
            bytes.CopyTo(span);
            Advance(size);
        }

        public void Write(double d)
        {
            int size = sizeof(double);
            EnsureCapasity(size);
            Span<byte> bytes = BitConverter.GetBytes(d);
            if (BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }
            bytes.CopyTo(span);
            Advance(size);
        }
#endif

        public void Write(ReadOnlySpan<byte> input)
        {
            int size = input.Length;
            EnsureCapasity(size);
            input.CopyTo(span);
            Advance(size);
        }

        public void Write(byte[] buffer, int start, int count)
        {
            Write(new ReadOnlySpan<byte>(buffer, start, count));
        }
    }
}

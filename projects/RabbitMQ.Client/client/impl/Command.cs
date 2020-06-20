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
using System.Runtime.InteropServices;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    class Command : IDisposable
    {
        // EmptyFrameSize, 8 = 1 + 2 + 4 + 1
        // - 1 byte of frame type
        // - 2 bytes of channel number
        // - 4 bytes of frame payload length
        // - 1 byte of payload trailer FrameEnd byte
        private const int EmptyFrameSize = 8;
        private readonly bool _returnBufferOnDispose;

        internal Command(MethodBase method) : this(method, null, null, false)
        {
        }

        public Command(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body, bool returnBufferOnDispose)
        {
            Method = method;
            Header = header;
            Body = body;
            _returnBufferOnDispose = returnBufferOnDispose;
        }

        public ReadOnlyMemory<byte> Body { get; private set; }

        internal ContentHeaderBase Header { get; private set; }

        internal MethodBase Method { get; private set; }

        internal void Transmit(ushort channelNumber, Connection connection)
        {
            int maxBodyPayloadBytes = (int)(connection.FrameMax == 0 ? int.MaxValue : connection.FrameMax - EmptyFrameSize);
            var size = GetMaxSize(maxBodyPayloadBytes);
            var memory = new Memory<byte>(ArrayPool<byte>.Shared.Rent(size), 0, size);
            var span = memory.Span;

            var offset = Framing.Method.WriteTo(span, channelNumber, Method);
            if (Method.HasContent)
            {
                int remainingBodyBytes = Body.Length;
                offset += Framing.Header.WriteTo(span.Slice(offset), channelNumber, Header, remainingBodyBytes);
                var bodySpan = Body.Span;
                while (remainingBodyBytes > 0)
                {
                    int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                    offset += Framing.BodySegment.WriteTo(span.Slice(offset), channelNumber, bodySpan.Slice(bodySpan.Length - remainingBodyBytes, frameSize));
                    remainingBodyBytes -= frameSize;
                }
            }

            if (offset != size)
            {
                throw new InvalidOperationException($"Serialized to wrong size, expect {size}, offset {offset}");
            }

            connection.Write(memory);
        }

        private int GetMaxSize(int maxPayloadBytes)
        {
            if (!Method.HasContent)
            {
                return Framing.Method.FrameSize + Method.GetRequiredBufferSize();
            }

            return Framing.Method.FrameSize + Method.GetRequiredBufferSize() +
                   Framing.Header.FrameSize + Header.GetRequiredPayloadBufferSize() +
                   Framing.BodySegment.FrameSize * GetBodyFrameCount(maxPayloadBytes) + Body.Length;
        }

        private int GetBodyFrameCount(int maxPayloadBytes)
        {
            if (maxPayloadBytes == int.MaxValue)
            {
                return 1;
            }

            return (Body.Length + maxPayloadBytes - 1) / maxPayloadBytes;
        }

        public void Dispose()
        {
            if(_returnBufferOnDispose && MemoryMarshal.TryGetArray(Body, out ArraySegment<byte> segment))
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }
    }
}

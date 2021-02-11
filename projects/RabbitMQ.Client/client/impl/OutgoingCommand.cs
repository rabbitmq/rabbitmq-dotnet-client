// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace RabbitMQ.Client.Impl
{
    internal interface IOutgoingCommand
    {
        MethodBase Method { get; }
        ReadOnlyMemory<byte> SerializeToFrames(ushort channelNumber, uint frameMax);
    }

    internal readonly struct OutgoingCommand : IOutgoingCommand
    {
        private readonly MethodBase _method;
        public MethodBase Method => _method;

        public OutgoingCommand(MethodBase method)
        {
            _method = method;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlyMemory<byte> SerializeToFrames(ushort channelNumber, uint frameMax)
        {
            int size = Method.GetRequiredBufferSize() + Framing.Method.FrameSize;

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(size);
            int offset = Framing.Method.WriteTo(rentedArray, channelNumber, Method);
#if DEBUG
            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
#endif

            return new ReadOnlyMemory<byte>(rentedArray, 0, size);
        }
    }

    internal readonly struct OutgoingContentCommand : IOutgoingCommand
    {
        public MethodBase Method => _method;
        private readonly MethodBase _method;
        private readonly ContentHeaderBase _header;
        private readonly ReadOnlyMemory<byte> _body;

        public OutgoingContentCommand(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body)
        {
            _method = method;
            _header = header;
            _body = body;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlyMemory<byte> SerializeToFrames(ushort channelNumber, uint frameMax)
        {
            int maxBodyPayloadBytes = (int)(frameMax == 0 ? int.MaxValue : frameMax - Framing.BaseFrameSize);
            int size = Method.GetRequiredBufferSize() +
                _header.GetRequiredPayloadBufferSize() +
                Framing.BodySegment.FrameSize * (maxBodyPayloadBytes == int.MaxValue ? 1 : (_body.Length + maxBodyPayloadBytes - 1) / maxBodyPayloadBytes) + _body.Length
                + Framing.Method.FrameSize + Framing.Header.FrameSize;

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(size);
            Span<byte> span = rentedArray.AsSpan(0, size);

            int offset = Framing.Method.WriteTo(span, channelNumber, Method);
            int remainingBodyBytes = _body.Length;
            offset += Framing.Header.WriteTo(span.Slice(offset), channelNumber, _header, remainingBodyBytes);
            if (remainingBodyBytes > 0)
            {
                ReadOnlySpan<byte> bodySpan = _body.Span;
                while (remainingBodyBytes > 0)
                {
                    int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                    offset += Framing.BodySegment.WriteTo(span.Slice(offset), channelNumber, bodySpan.Slice(bodySpan.Length - remainingBodyBytes, frameSize));
                    remainingBodyBytes -= frameSize;
                }
            }

#if DEBUG
            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
#endif

            return new ReadOnlyMemory<byte>(rentedArray, 0, size);
        }
    }
}

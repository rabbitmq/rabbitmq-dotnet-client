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

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal static class MethodBaseExtensions
    {
        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static ReadOnlyMemory<byte> SerializeToFrames<T>(this T method, ushort channelNumber) where T : MethodBase
        {
            int size = method.GetRequiredBufferSize() + Framing.Method.FrameSize;

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(size);
            int offset = Framing.Method.WriteTo(rentedArray, channelNumber, method);
            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return new ReadOnlyMemory<byte>(rentedArray, 0, size);
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        internal static int SerializeToFrames(this ReadOnlySpan<byte> body, Span<byte> span, ushort channelNumber, int maxBodyPayloadBytes)
        {
            int remainingBodyBytes = body.Length;
            int offset = 0;
            while (remainingBodyBytes > 0)
            {
                int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                offset += Framing.BodySegment.WriteTo(span.Slice(offset), channelNumber, body.Slice(body.Length - remainingBodyBytes, frameSize));
                remainingBodyBytes -= frameSize;
            }

            return offset;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public static ReadOnlyMemory<byte> SerializeToFrames<TMethod, THeader>(this TMethod method, THeader header, ReadOnlyMemory<byte> body, ushort channelNumber, uint frameMax)
            where TMethod : MethodBase
            where THeader : ContentHeaderBase
        {
            int maxBodyPayloadBytes = (int)(frameMax == 0 ? int.MaxValue : frameMax - Framing.BaseFrameSize);
            int size = method.GetRequiredBufferSize() + header.GetRequiredPayloadBufferSize() +
                Framing.BodySegment.FrameSize * (maxBodyPayloadBytes == int.MaxValue ? 1 : (body.Length + maxBodyPayloadBytes - 1) / maxBodyPayloadBytes) + body.Length
                + Framing.Method.FrameSize + Framing.Header.FrameSize;
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(size);
            int offset = Framing.Method.WriteTo(rentedArray, channelNumber, method);
            offset += Framing.Header.WriteTo(rentedArray.AsSpan(offset), channelNumber, header, body.Length);
            if (!body.IsEmpty)
            {
                offset += body.Span.SerializeToFrames(rentedArray.AsSpan(offset), channelNumber, maxBodyPayloadBytes);
            }

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return new ReadOnlyMemory<byte>(rentedArray, 0, size);
        }
    }

    internal readonly struct OutgoingContentCommand
    {
        private readonly MethodBase _method;
        private readonly ContentHeaderBase _header;
        private readonly ReadOnlyMemory<byte> _body;

        public OutgoingContentCommand(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body)
        {
            _method = method;
            _header = header;
            _body = body;
        }

        [MethodImpl(RabbitMQMethodImplOptions.Optimized)]
        public readonly ReadOnlyMemory<byte> SerializeToFrames(ushort channelNumber, uint frameMax)
        {
            int maxBodyPayloadBytes = (int)(frameMax == 0 ? int.MaxValue : frameMax - Framing.BaseFrameSize);

            int size = _header.GetRequiredPayloadBufferSize() +
                Framing.BodySegment.FrameSize * (maxBodyPayloadBytes == int.MaxValue ? 1 : (_body.Length + maxBodyPayloadBytes - 1) / maxBodyPayloadBytes) + _body.Length
                + Framing.Method.FrameSize + Framing.Header.FrameSize;

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] rentedArray;
            int offset;

            switch (_method)
            {
                case BasicPublish basicPublish:
                    size += basicPublish.GetRequiredBufferSize();
                    rentedArray = ArrayPool<byte>.Shared.Rent(size);
                    offset = Framing.Method.WriteTo(rentedArray, channelNumber, basicPublish);
                    break;
                case BasicPublishMemory basicPublishMemory:
                    size += basicPublishMemory.GetRequiredBufferSize();
                    rentedArray = ArrayPool<byte>.Shared.Rent(size);
                    offset = Framing.Method.WriteTo(rentedArray, channelNumber, basicPublishMemory);
                    break;
                default:
                    size += _method.GetRequiredBufferSize();
                    rentedArray = ArrayPool<byte>.Shared.Rent(size);
                    offset = Framing.Method.WriteTo(rentedArray, channelNumber, _method);
                    break;
            }

            offset += Framing.Header.WriteTo(rentedArray.AsSpan(offset), channelNumber, _header, _body.Length);
            if (!_body.IsEmpty)
            {
                offset += _body.Span.SerializeToFrames(rentedArray.AsSpan(offset), channelNumber, maxBodyPayloadBytes);
            }

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return new ReadOnlyMemory<byte>(rentedArray, 0, size);
        }
    }
}

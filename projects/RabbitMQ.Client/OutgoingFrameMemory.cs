// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Diagnostics;
using static RabbitMQ.Client.Impl.Framing;

namespace RabbitMQ.Client
{
    internal readonly struct OutgoingFrameMemory : IDisposable
    {
        internal OutgoingFrameMemory(byte[] rentedMethodAndHeader, int methodAndHeaderLength)
        {
            _rentedMethodAndHeader = rentedMethodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = default;
            _rentedBody = null;
            _channelNumber = 0;
            _maxBodyPayloadBytes = 0;
            Size = methodAndHeaderLength;
        }

        internal OutgoingFrameMemory(
            byte[] rentedMethodAndHeader,
            int methodAndHeaderLength,
            ReadOnlyMemory<byte> body,
            byte[] rentedBody,
            ushort channelNumber,
            int maxBodyPayloadBytes,
            int totalSize)
        {
            _rentedMethodAndHeader = rentedMethodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = body;
            _rentedBody = rentedBody;
            _channelNumber = channelNumber;
            _maxBodyPayloadBytes = maxBodyPayloadBytes;
            Size = totalSize;
        }

        internal readonly int Size { get; }

        private readonly byte[] _rentedMethodAndHeader;
        private readonly int _methodAndHeaderLength;
        private readonly ushort _channelNumber;
        private readonly int _maxBodyPayloadBytes;
        private readonly ReadOnlyMemory<byte> _body;
        private readonly byte[]? _rentedBody;

        internal readonly void WriteTo(IBufferWriter<byte> bufferWriter)
        {
            Debug.Assert(_rentedMethodAndHeader != null);

            // Write the pre-serialized portion that all messages will always have
            ReadOnlySpan<byte> methodAndHeader = _rentedMethodAndHeader.AsSpan(0, _methodAndHeaderLength);
            bufferWriter.Write(methodAndHeader);

            if (_body.Length == 0)
            {
                return;
            }

            ReadOnlySpan<byte> bodySpan = _body.Span;
            int remainingBodyBytes = bodySpan.Length;
            int bodyOffset = 0;

            while (remainingBodyBytes > 0)
            {
                int payloadSize = remainingBodyBytes > _maxBodyPayloadBytes ? _maxBodyPayloadBytes : remainingBodyBytes;
                BodySegment.WriteTo(bufferWriter, _channelNumber, bodySpan.Slice(bodyOffset, payloadSize));
                remainingBodyBytes -= payloadSize;
                bodyOffset += payloadSize;
            }
        }

        public void Dispose()
        {
            Debug.Assert(_rentedMethodAndHeader != null);
            ArrayPool<byte>.Shared.Return(_rentedMethodAndHeader);

            if (_rentedBody != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedBody);
            }
        }
    }
}

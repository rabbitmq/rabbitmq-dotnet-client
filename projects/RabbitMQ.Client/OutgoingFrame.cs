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
    internal struct OutgoingFrame : IDisposable
    {
        private IMemoryOwner<byte>? _methodAndHeader;
        private readonly int _methodAndHeaderLength;
        private IMemoryOwner<byte>? _body;
        private readonly int _bodyLength;
        private readonly int _maxBodyPayloadBytes;
        private readonly ushort _channelNumber;

        internal OutgoingFrame(
            IMemoryOwner<byte> methodAndHeader,
            int methodAndHeaderLength)
        {
            _methodAndHeader = methodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = null;
            _bodyLength = 0;
            _channelNumber = 0;
            _maxBodyPayloadBytes = 0;
            Size = methodAndHeaderLength;
        }

        internal OutgoingFrame(
            IMemoryOwner<byte> methodAndHeader,
            int methodAndHeaderLength,
            IMemoryOwner<byte> body,
            int bodyLength,
            ushort channelNumber,
            int maxBodyPayloadBytes,
            int totalSize)
        {
            _methodAndHeader = methodAndHeader;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = body;
            _bodyLength = bodyLength;
            _channelNumber = channelNumber;
            _maxBodyPayloadBytes = maxBodyPayloadBytes;
            Size = totalSize;
        }

        internal int Size { get; }

        internal readonly void WriteTo(IBufferWriter<byte> writer)
        {
            Debug.Assert(_methodAndHeader is not null);
            ReadOnlySpan<byte> methodAndHeader = _methodAndHeader!.Memory.Span.Slice(0, _methodAndHeaderLength);
            writer.Write(methodAndHeader);

            if (_bodyLength == 0)
            {
                return;
            }

            Debug.Assert(_body is not null);
            ReadOnlySpan<byte> bodySpan = _body!.Memory.Span.Slice(0, _bodyLength);
            int remainingBodyBytes = bodySpan.Length;
            int bodyOffset = 0;

            while (remainingBodyBytes > 0)
            {
                int payloadSize = remainingBodyBytes > _maxBodyPayloadBytes ? _maxBodyPayloadBytes : remainingBodyBytes;
                BodySegment.WriteTo(writer, _channelNumber, bodySpan.Slice(bodyOffset, payloadSize));
                remainingBodyBytes -= payloadSize;
                bodyOffset += payloadSize;
            }
        }

        public void Dispose()
        {
            IMemoryOwner<byte>? memoryOwner = _methodAndHeader;
            _methodAndHeader = null;
            if (memoryOwner is not null)
            {
                memoryOwner.Dispose();
                _body?.Dispose();
                _body = default;
            }
        }
    }
}

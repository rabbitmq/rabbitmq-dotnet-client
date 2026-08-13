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
        private byte[]? _methodAndHeaderArray;
        private readonly int _methodAndHeaderLength;
        private ReadOnlySequence<byte> _body;
        private IDisposable? _bodyOwner;
        private readonly int _maxBodyPayloadBytes;
        private readonly ushort _channelNumber;

        internal OutgoingFrame(
            byte[] methodAndHeaderArray,
            int methodAndHeaderLength)
        {
            _methodAndHeaderArray = methodAndHeaderArray;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = default;
            _bodyOwner = null;
            _channelNumber = 0;
            _maxBodyPayloadBytes = 0;
            Size = methodAndHeaderLength;
        }

        internal OutgoingFrame(
            byte[] methodAndHeaderArray,
            int methodAndHeaderLength,
            ReadOnlyMemory<byte> body,
            IDisposable? bodyOwner,
            ushort channelNumber,
            int maxBodyPayloadBytes,
            int totalSize)
            : this(methodAndHeaderArray, methodAndHeaderLength, new ReadOnlySequence<byte>(body),
                  bodyOwner, channelNumber, maxBodyPayloadBytes, totalSize)
        {
        }

        internal OutgoingFrame(
            byte[] methodAndHeaderArray,
            int methodAndHeaderLength,
            in ReadOnlySequence<byte> body,
            IDisposable? bodyOwner,
            ushort channelNumber,
            int maxBodyPayloadBytes,
            int totalSize)
        {
            _methodAndHeaderArray = methodAndHeaderArray;
            _methodAndHeaderLength = methodAndHeaderLength;
            _body = body;
            _bodyOwner = bodyOwner;
            _channelNumber = channelNumber;
            _maxBodyPayloadBytes = maxBodyPayloadBytes;
            Size = totalSize;
        }

        internal int Size { get; }

        internal readonly void WriteTo(IBufferWriter<byte> writer)
        {
            Debug.Assert(_methodAndHeaderArray is not null);
            ReadOnlySpan<byte> methodAndHeader = _methodAndHeaderArray.AsSpan(0, _methodAndHeaderLength);
            writer.Write(methodAndHeader);

            long remainingBodyBytes = _body.Length;
            if (remainingBodyBytes == 0)
            {
                return;
            }

            if (_body.IsSingleSegment)
            {
                ReadOnlySpan<byte> bodySpan = _body.First.Span;
                int bodyOffset = 0;

                while (bodyOffset < bodySpan.Length)
                {
                    int payloadSize = Math.Min(bodySpan.Length - bodyOffset, _maxBodyPayloadBytes);
                    BodySegment.WriteTo(writer, _channelNumber, bodySpan.Slice(bodyOffset, payloadSize));
                    bodyOffset += payloadSize;
                }

                return;
            }

            // Multi-segment body: body frame boundaries are independent of segment boundaries,
            // so each frame payload is sliced out of the sequence and may itself span segments.
            ReadOnlySequence<byte> remainingBody = _body;
            while (remainingBodyBytes > 0)
            {
                int payloadSize = (int)Math.Min(remainingBodyBytes, _maxBodyPayloadBytes);
                BodySegment.WriteTo(writer, _channelNumber, remainingBody.Slice(0, payloadSize));
                remainingBody = remainingBody.Slice(payloadSize);
                remainingBodyBytes -= payloadSize;
            }
        }

        public void Dispose()
        {
            byte[]? array = _methodAndHeaderArray;
            _methodAndHeaderArray = null;
            if (array is not null)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
            _bodyOwner?.Dispose();
            _bodyOwner = null;
            _body = default;
        }
    }
}

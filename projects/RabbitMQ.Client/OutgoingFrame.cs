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
using System.Threading.Tasks;
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
            ReadOnlySequence<byte> body,
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

        private const int FlushThresholdBytes = 64 * 1024;

        internal readonly async ValueTask WriteToAsync(IBufferWriter<byte> writer, Func<ValueTask> flushAsync)
        {
            Debug.Assert(_methodAndHeaderArray is not null);
            writer.Write(_methodAndHeaderArray.AsSpan(0, _methodAndHeaderLength));

            // A multi-segment body is sliced per frame and may span segments within one frame.
            bool isSingleSegment = _body.IsSingleSegment;
            ReadOnlySequence<byte> remainingBody = _body;
            long remainingBodyBytes = remainingBody.Length;
            int unflushedBytes = _methodAndHeaderLength;

            while (remainingBodyBytes > 0)
            {
                int payloadSize = (int)Math.Min(remainingBodyBytes, _maxBodyPayloadBytes);
                ReadOnlySequence<byte> payload = remainingBody.Slice(0, payloadSize);
                unflushedBytes += isSingleSegment
                    ? BodySegment.WriteTo(writer, _channelNumber, payload.First.Span)
                    : BodySegment.WriteTo(writer, _channelNumber, payload);
                remainingBody = remainingBody.Slice(payloadSize);
                remainingBodyBytes -= payloadSize;

                // The pipe holds a copy of every frame written since the last flush. The caller flushes after the last one.
                if (unflushedBytes >= FlushThresholdBytes && remainingBodyBytes > 0)
                {
                    await flushAsync().ConfigureAwait(false);
                    unflushedBytes = 0;
                }
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
            IDisposable? bodyOwner = _bodyOwner;
            _bodyOwner = null;
            _body = default;
            BodyOwner.Dispose(bodyOwner);
        }
    }
}

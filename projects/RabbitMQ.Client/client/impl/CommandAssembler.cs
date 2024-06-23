// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Logging;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
#nullable enable
    internal sealed class CommandAssembler
    {
        private const int MaxArrayOfBytesSize = 2_147_483_591;

        private ProtocolCommandId _commandId;
        private RentedMemory _methodMemory;
        private RentedMemory _headerMemory;
        private RentedMemory _bodyMemory;
        private int _remainingBodyByteCount;
        private int _offset;
        private AssemblyState _state;

        private readonly uint _maxBodyLength;

        public CommandAssembler(uint maxBodyLength)
        {
            _maxBodyLength = maxBodyLength;
            Reset();
        }

        private void Reset()
        {
            _commandId = default;
            _methodMemory = default;
            _headerMemory = default;
            _bodyMemory = default;
            _remainingBodyByteCount = 0;
            _offset = 0;
            _state = AssemblyState.ExpectingMethod;
        }

        public void HandleFrame(InboundFrame frame, out IncomingCommand command)
        {
            switch (_state)
            {
                case AssemblyState.ExpectingMethod:
                    ParseMethodFrame(frame);
                    break;
                case AssemblyState.ExpectingContentHeader:
                    ParseHeaderFrame(frame);
                    break;
                case AssemblyState.ExpectingContentBody:
                    ParseBodyFrame(frame);
                    break;
            }

            if (_state != AssemblyState.Complete)
            {
                command = IncomingCommand.Empty;
                return;
            }

            RabbitMqClientEventSource.Log.CommandReceived();
            command = new IncomingCommand(_commandId, _methodMemory, _headerMemory, _bodyMemory);
            Reset();
        }

        private void ParseMethodFrame(InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameMethod)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            _commandId = (ProtocolCommandId)NetworkOrderDeserializer.ReadUInt32(frame.Payload.Span);
            _methodMemory = frame.TakeoverPayload(Framing.Method.ArgumentsOffset);

            switch (_commandId)
            {
                // Commands with payload
                case ProtocolCommandId.BasicGetOk:
                case ProtocolCommandId.BasicDeliver:
                case ProtocolCommandId.BasicReturn:
                    _state = AssemblyState.ExpectingContentHeader;
                    break;
                default:
                    _state = AssemblyState.Complete;
                    break;
            }
        }

        private void ParseHeaderFrame(InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameHeader)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            ReadOnlySpan<byte> span = frame.Payload.Span;
            ushort classId = NetworkOrderDeserializer.ReadUInt16(span);
            if (classId != ClassConstants.Basic)
            {
                throw new UnknownClassOrMethodException(classId, 0);
            }

            ulong totalBodyBytes = NetworkOrderDeserializer.ReadUInt64(span.Slice(Framing.Header.BodyLengthOffset));
            if (totalBodyBytes > MaxArrayOfBytesSize)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            if (totalBodyBytes > _maxBodyLength)
            {
                string msg = $"Frame body size '{totalBodyBytes}' exceeds maximum of '{_maxBodyLength}' bytes";
                throw new MalformedFrameException(message: msg, canShutdownCleanly: false);
            }

            // There are always at least 2 bytes, even for empty ones
            if (frame.Payload.Length <= Framing.Header.HeaderArgumentOffset + 2)
            {
                frame.TryReturnPayload();
            }
            else
            {
                _headerMemory = frame.TakeoverPayload(Framing.Header.HeaderArgumentOffset);
            }

            _remainingBodyByteCount = (int)totalBodyBytes;
            UpdateContentBodyState();
        }

        private void ParseBodyFrame(InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameBody)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            int payloadLength = frame.Payload.Length;
            if (payloadLength > _remainingBodyByteCount)
            {
                throw new MalformedFrameException($"Overlong content body received - {_remainingBodyByteCount} bytes remaining, {payloadLength} bytes received");
            }

            if (_bodyMemory.RentedArray is null)
            {
                // check for single frame payload for an early exit
                if (payloadLength == _remainingBodyByteCount)
                {
                    _bodyMemory = frame.TakeoverPayload(0);
                    _state = AssemblyState.Complete;
                    return;
                }

                // Is returned by IncomingCommand.ReturnPayload in Session.HandleFrame
                var rentedBodyArray = ArrayPool<byte>.Shared.Rent(_remainingBodyByteCount);
                _bodyMemory = new RentedMemory(new ReadOnlyMemory<byte>(rentedBodyArray, 0, _remainingBodyByteCount), rentedBodyArray);
            }

            frame.Payload.Span.CopyTo(_bodyMemory.RentedArray.AsSpan(_offset));
            frame.TryReturnPayload();
            _remainingBodyByteCount -= payloadLength;
            _offset += payloadLength;
            UpdateContentBodyState();
        }

        private void UpdateContentBodyState()
        {
            _state = _remainingBodyByteCount > 0 ? AssemblyState.ExpectingContentBody : AssemblyState.Complete;
        }

        private enum AssemblyState
        {
            ExpectingMethod,
            ExpectingContentHeader,
            ExpectingContentBody,
            Complete
        }
    }
}

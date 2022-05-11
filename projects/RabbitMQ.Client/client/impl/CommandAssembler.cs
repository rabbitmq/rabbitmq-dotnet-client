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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal sealed class CommandAssembler
    {
        private const int MaxArrayOfBytesSize = 2_147_483_591;

        private readonly ProtocolBase _protocol;

        private MethodBase _method;
        private ContentHeaderBase _header;
        private byte[] _bodyBytes;
        private Memory<byte> _body;
        private int _remainingBodyBytes;
        private int _offset;
        private AssemblyState _state;

        public CommandAssembler(ProtocolBase protocol)
        {
            _protocol = protocol;
            Reset();
        }

        private void Reset()
        {
            _method = null;
            _header = null;
            _bodyBytes = null;
            _body = Memory<byte>.Empty;
            _remainingBodyBytes = 0;
            _offset = 0;
            _state = AssemblyState.ExpectingMethod;
        }

        public IncomingCommand HandleFrame(in InboundFrame frame)
        {
            switch (_state)
            {
                case AssemblyState.ExpectingMethod:
                    ParseMethodFrame(in frame);
                    break;
                case AssemblyState.ExpectingContentHeader:
                    ParseHeaderFrame(in frame);
                    break;
                case AssemblyState.ExpectingContentBody:
                    ParseBodyFrame(in frame);
                    break;
            }

            if (_state != AssemblyState.Complete)
            {
                return IncomingCommand.Empty;
            }

            var result = new IncomingCommand(_method, _header, _body, _bodyBytes, _protocol.MemoryPool);
            Reset();
            return result;
        }

        private void ParseMethodFrame(in InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameMethod)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            _method = _protocol.DecodeMethodFrom(frame.Payload.Span);
            _state = _method.HasContent ? AssemblyState.ExpectingContentHeader : AssemblyState.Complete;
        }

        private void ParseHeaderFrame(in InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameHeader)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            ReadOnlySpan<byte> span = frame.Payload.Span;
            _header = _protocol.DecodeContentHeaderFrom(NetworkOrderDeserializer.ReadUInt16(span));
            _header.ReadFrom(span.Slice(12));
            ulong totalBodyBytes = NetworkOrderDeserializer.ReadUInt64(span.Slice(4));
            if (totalBodyBytes > MaxArrayOfBytesSize)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            _remainingBodyBytes = (int) totalBodyBytes;

            // Is returned by IncomingCommand.Dispose in Session.HandleFrame
            _bodyBytes = _protocol.MemoryPool.Rent(_remainingBodyBytes);
            _body = new Memory<byte>(_bodyBytes, 0, _remainingBodyBytes);
            UpdateContentBodyState();
        }

        private void ParseBodyFrame(in InboundFrame frame)
        {
            if (frame.Type != FrameType.FrameBody)
            {
                throw new UnexpectedFrameException(frame.Type);
            }

            int payloadLength = frame.Payload.Length;
            if (payloadLength > _remainingBodyBytes)
            {
                throw new MalformedFrameException($"Overlong content body received - {_remainingBodyBytes} bytes remaining, {payloadLength} bytes received");
            }

            frame.Payload.CopyTo(_body.Slice(_offset));
            _remainingBodyBytes -= payloadLength;
            _offset += payloadLength;
            UpdateContentBodyState();
        }

        private void UpdateContentBodyState()
        {
            _state = _remainingBodyBytes > 0 ? AssemblyState.ExpectingContentBody : AssemblyState.Complete;
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

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
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Logging;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl;

#nullable enable
internal sealed class CommandAssembler
{
    private const int MaxArrayOfBytesSize = 2_147_483_591;

    private ProtocolCommandId _commandId;
    private ReadOnlyMemory<byte> _methodBytes;
    private byte[]? _rentedMethodArray;
    private ReadOnlyMemory<byte> _headerBytes;
    private byte[]? _rentedHeaderArray;
    private ReadOnlyMemory<byte> _bodyBytes;
    private byte[]? _rentedBodyArray;
    private int _remainingBodyBytes;
    private int _offset;
    private AssemblyState _state;

    public CommandAssembler()
    {
        Reset();
    }

    private void Reset()
    {
        _commandId = default;
        _methodBytes = ReadOnlyMemory<byte>.Empty;
        _rentedMethodArray = null;
        _headerBytes = ReadOnlyMemory<byte>.Empty;
        _rentedHeaderArray = null;
        _bodyBytes = ReadOnlyMemory<byte>.Empty;
        _rentedBodyArray = null;
        _remainingBodyBytes = 0;
        _offset = 0;
        _state = AssemblyState.ExpectingMethod;
    }

    public bool HandleFrame(in InboundFrame frame, out IncomingCommand command)
    {
        bool shallReturn = true;
        switch (_state)
        {
            case AssemblyState.ExpectingMethod:
                ParseMethodFrame(in frame);
                shallReturn = false;
                break;
            case AssemblyState.ExpectingContentHeader:
                shallReturn = ParseHeaderFrame(in frame);
                break;
            case AssemblyState.ExpectingContentBody:
                shallReturn = ParseBodyFrame(in frame);
                break;
        }

        if (_state != AssemblyState.Complete)
        {
            command = IncomingCommand.Empty;
            return shallReturn;
        }

        RabbitMqClientEventSource.Log.CommandReceived();
        command = new IncomingCommand(_commandId, _methodBytes, _rentedMethodArray, _headerBytes, _rentedHeaderArray, _bodyBytes, _rentedBodyArray);
        Reset();
        return shallReturn;
    }

    private void ParseMethodFrame(in InboundFrame frame)
    {
        if (frame.Type != FrameType.FrameMethod)
        {
            throw new UnexpectedFrameException(frame.Type);
        }

        _rentedMethodArray = frame.TakeoverPayload();
        _commandId = (ProtocolCommandId)NetworkOrderDeserializer.ReadUInt32(frame.Payload.Span);
        _methodBytes = frame.Payload.Slice(4);

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

    private bool ParseHeaderFrame(in InboundFrame frame)
    {
        if (frame.Type != FrameType.FrameHeader)
        {
            throw new UnexpectedFrameException(frame.Type);
        }

        ReadOnlySpan<byte> span = frame.Payload.Span;
        var classId = NetworkOrderDeserializer.ReadUInt16(span);
        if (classId != ClassConstants.Basic)
        {
            throw new UnknownClassOrMethodException(classId, 0);
        }

        ulong totalBodyBytes = NetworkOrderDeserializer.ReadUInt64(span.Slice(4));
        if (totalBodyBytes > MaxArrayOfBytesSize)
        {
            throw new UnexpectedFrameException(frame.Type);
        }
        _rentedHeaderArray = totalBodyBytes != 0 ? frame.TakeoverPayload() : Array.Empty<byte>();

        _headerBytes = frame.Payload;

        _remainingBodyBytes = (int)totalBodyBytes;
        UpdateContentBodyState();
        return _rentedHeaderArray.Length == 0;
    }

    private bool ParseBodyFrame(in InboundFrame frame)
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

        if (_rentedBodyArray is null)
        {
            // check for single frame payload for an early exit
            if (payloadLength == _remainingBodyBytes)
            {
                _rentedBodyArray = frame.TakeoverPayload();
                _bodyBytes = frame.Payload;
                _state = AssemblyState.Complete;
                return false;
            }

            // Is returned by IncomingCommand.ReturnPayload in Session.HandleFrame
            _rentedBodyArray = ArrayPool<byte>.Shared.Rent(_remainingBodyBytes);
            _bodyBytes = new ReadOnlyMemory<byte>(_rentedBodyArray, 0, _remainingBodyBytes);
        }

        frame.Payload.Span.CopyTo(_rentedBodyArray.AsSpan(_offset));
        _remainingBodyBytes -= payloadLength;
        _offset += payloadLength;
        UpdateContentBodyState();
        return true;
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

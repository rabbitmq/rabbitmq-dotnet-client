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

using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal readonly struct OutgoingFrame
    {
        internal FrameType FrameType { get; }
        internal ushort Channel { get; }
        internal MethodBase Method { get; }
        internal ContentHeaderBase Header { get; }
        internal ReadOnlyMemory<byte> Body { get; }
        internal int MaxBodyPayloadBytes { get; }

        private OutgoingFrame(FrameType frameType, ushort channel, MethodBase methodBase, ContentHeaderBase contentHeaderBase, ReadOnlyMemory<byte> body, int maxBodyPayloadBytes)
        {
            FrameType = frameType;
            Channel = channel;
            Method = methodBase;
            Header = contentHeaderBase;
            Body = body;
            MaxBodyPayloadBytes = maxBodyPayloadBytes;
        }

        internal static OutgoingFrame CreateHeartbeat()
        {
            return new OutgoingFrame(FrameType.FrameHeartbeat, 0, null, null, ReadOnlyMemory<byte>.Empty, 0);
        }

        internal static OutgoingFrame CreateMethod(ushort channel, MethodBase methodBase, ContentHeaderBase contentHeaderBase, ReadOnlyMemory<byte> body, int maxBodyPayloadSize)
        {
            return new OutgoingFrame(FrameType.FrameMethod, channel, methodBase, contentHeaderBase, body, maxBodyPayloadSize);
        }

        internal int GetMaxSize(int maxPayloadBytes)
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
    }

    internal readonly struct OutgoingCommand
    {
        // EmptyFrameSize, 8 = 1 + 2 + 4 + 1
        // - 1 byte of frame type
        // - 2 bytes of channel number
        // - 4 bytes of frame payload length
        // - 1 byte of payload trailer FrameEnd byte
        private const int EmptyFrameSize = 8;

        public readonly MethodBase Method;
        private readonly ContentHeaderBase _header;
        private readonly ReadOnlyMemory<byte> _body;

        public OutgoingCommand(MethodBase method)
            : this(method, null, ReadOnlyMemory<byte>.Empty)
        {
        }

        public OutgoingCommand(MethodBase method, ContentHeaderBase header, ReadOnlyMemory<byte> body)
        {
            Method = method;
            _header = header;
            _body = body;
        }

        internal void Transmit(ushort channelNumber, Connection connection)
        {
            int maxBodyPayloadBytes = (int)(connection.FrameMax == 0 ? int.MaxValue : connection.FrameMax - EmptyFrameSize);
            connection.Write(OutgoingFrame.CreateMethod(channelNumber, Method, _header, _body, maxBodyPayloadBytes));
        }

        private int GetMaxSize(int maxPayloadBytes)
        {
            if (!Method.HasContent)
            {
                return Framing.Method.FrameSize + Method.GetRequiredBufferSize();
            }

            return Framing.Method.FrameSize + Method.GetRequiredBufferSize() +
                   Framing.Header.FrameSize + _header.GetRequiredPayloadBufferSize() +
                   Framing.BodySegment.FrameSize * GetBodyFrameCount(maxPayloadBytes) + _body.Length;
        }

        private int GetBodyFrameCount(int maxPayloadBytes)
        {
            if (maxPayloadBytes == int.MaxValue)
            {
                return 1;
            }

            return (_body.Length + maxPayloadBytes - 1) / maxPayloadBytes;
        }
    }
}

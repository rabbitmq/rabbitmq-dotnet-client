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

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    internal static class Framing
    {
        /* +------------+---------+----------------+---------+------------------+
         * | Frame Type | Channel | Payload length | Payload | Frame End Marker |
         * +------------+---------+----------------+---------+------------------+
         * | 1 byte     | 2 bytes | 4 bytes        | x bytes | 1 byte           |
         * +------------+---------+----------------+---------+------------------+ */
        internal const int BaseFrameSize = 1 + 2 + 4 + 1;
        internal const int StartPayload = 7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteBaseFrame(Span<byte> span, FrameType type, ushort channel, int payloadLength)
        {
            const int StartFrameType = 0;
            const int StartChannel = 1;
            const int StartPayloadSize = 3;

            span[StartFrameType] = (byte)type;
            NetworkOrderSerializer.WriteUInt16(span.Slice(StartChannel), channel);
            NetworkOrderSerializer.WriteUInt32(span.Slice(StartPayloadSize), (uint)payloadLength);
            span[StartPayload + payloadLength] = Constants.FrameEnd;
            return StartPayload + 1 + payloadLength;
        }

        internal static class Heartbeat
        {
            public const int FrameSize = BaseFrameSize;

            public static int WriteTo(Span<byte> span)
            {
                return WriteBaseFrame(span, FrameType.FrameHeartbeat, 0, 0);
            }
        }

        internal static class Method
        {
            /* +----------+-----------+-----------+
             * | CommandId (combined) | Arguments |    
             * | Class Id | Method Id |           |
             * +----------+-----------+-----------+
             * | 4 bytes (combined)   | x bytes   |
             * | 2 bytes  | 2 bytes   |           |
             * +----------+-----------+-----------+ */
            public const int FrameSize = BaseFrameSize + 2 + 2;

            public static int WriteTo(Span<byte> span, ushort channel, MethodBase method)
            {
                const int StartClassId = StartPayload;
                const int StartMethodArguments = StartPayload + 4;

                NetworkOrderSerializer.WriteUInt32(span.Slice(StartClassId), (uint)method.ProtocolCommandId);
                int offset = method.WriteArgumentsTo(span.Slice(StartMethodArguments));
                return WriteBaseFrame(span, FrameType.FrameMethod, channel, StartMethodArguments - StartPayload + offset);
            }
        }

        internal static class Header
        {
            /* +----------+----------+-------------------+-----------+
             * | Class Id | (unused) | Total body length | Arguments |
             * +----------+----------+-------------------+-----------+
             * | 2 bytes  | 2 bytes  | 8 bytes           | x bytes   |
             * +----------+----------+-------------------+-----------+ */
            public const int FrameSize = BaseFrameSize + 2 + 2 + 8;

            public static int WriteTo(Span<byte> span, ushort channel, ContentHeaderBase header, int bodyLength)
            {
                const int StartClassId = StartPayload;
                const int StartWeight = StartPayload + 2;
                const int StartBodyLength = StartPayload + 4;
                const int StartHeaderArguments = StartPayload + 12;

                NetworkOrderSerializer.WriteUInt16(span.Slice(StartClassId), header.ProtocolClassId);
                NetworkOrderSerializer.WriteUInt16(span.Slice(StartWeight), 0); // Weight - not used
                NetworkOrderSerializer.WriteUInt64(span.Slice(StartBodyLength), (ulong)bodyLength);
                int offset = header.WritePropertiesTo(span.Slice(StartHeaderArguments));
                return WriteBaseFrame(span, FrameType.FrameHeader, channel, StartHeaderArguments - StartPayload + offset);
            }
        }

        internal static class BodySegment
        {
            /* +--------------+
             * | Body segment |
             * +--------------+
             * | x bytes      |
             * +--------------+ */
            public const int FrameSize = BaseFrameSize;

            public static int WriteTo(Span<byte> span, ushort channel, ReadOnlySpan<byte> body)
            {
                const int StartBodyArgument = StartPayload;

                body.CopyTo(span.Slice(StartBodyArgument));
                return WriteBaseFrame(span, FrameType.FrameBody, channel, StartBodyArgument - StartPayload + body.Length);
            }
        }
    }

    internal readonly struct InboundFrame
    {
        public readonly FrameType Type;
        public readonly int Channel;
        public readonly ReadOnlyMemory<byte> Payload;
        private readonly byte[] _rentedArray;

        private InboundFrame(FrameType type, int channel, ReadOnlyMemory<byte> payload, byte[] rentedArray)
        {
            Type = type;
            Channel = channel;
            Payload = payload;
            _rentedArray = rentedArray;
        }

        internal static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out InboundFrame frame)
        {
            // We'll always need to read at least 8 bytes (the Framing.BaseFrameSize) or 8 bytes of protocol error (see ProcessProtocolHeader).
            if (buffer.Length < Framing.BaseFrameSize)
            {
                frame = default;
                return false;
            }

            // Let's first check if we have a protocol header.
            if (buffer.First.Span[0] == 'A')
            {
                ProcessProtocolHeader(buffer);
            }

            int type = buffer.First.Span[0];
            ParseFrameHeader(buffer, out int channel, out int payloadSize);

            // We'll need to read the payloadSize + FrameEndMarker (1 byte)
            int readSize = payloadSize + 1;

            // Do we have enough bytes to read the rest of the frame
            if (buffer.Length < (Framing.StartPayload + readSize))
            {
                frame = default;
                return false;
            }

            // The rented array is returned by InboundFrame.ReturnPayload in Connection.MainLoopIteration
            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(readSize);
            Memory<byte> payloadMemory = payloadBytes.AsMemory(0, readSize);
            ReadOnlySequence<byte> payloadSlice = buffer.Slice(7, readSize);
            payloadSlice.CopyTo(payloadMemory.Span);

            // Let's validate that the frame contains a valid Frame End Marker
            if (payloadBytes[payloadSize] != Constants.FrameEnd)
            {
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Bad frame end marker: {payloadBytes[payloadSize]}");
            }

            // We have a frame!
            frame = new InboundFrame((FrameType)type, channel, payloadMemory.Slice(0, payloadSize), payloadBytes);

            // Update the buffer
            buffer = buffer.Slice(payloadSlice.End);
            return true;
        }

        private static void ProcessProtocolHeader(ReadOnlySequence<byte> buffer)
        {
            if (buffer.First.Length >= 8)
            {
                EvaluateProtocolError(buffer.First.Span.Slice(1, 7));
            }
            else
            {
                Span<byte> tempBuffer = stackalloc byte[7];
                buffer.Slice(1, 7).CopyTo(tempBuffer);
                EvaluateProtocolError(tempBuffer);
            }

            void EvaluateProtocolError(ReadOnlySpan<byte> protocolError)
            {
                byte b1 = protocolError[0];
                byte b2 = protocolError[1];
                byte b3 = protocolError[2];
                if (b1 != 'M' || b2 != 'Q' || b3 != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                int transportHigh = protocolError[3];
                int transportLow = protocolError[4];
                int serverMajor = protocolError[5];
                int serverMinor = protocolError[6];
                throw new PacketNotRecognizedException(transportHigh, transportLow, serverMajor, serverMinor);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private static void ParseFrameHeader(ReadOnlySequence<byte> buffer, out int channel, out int payloadSize)
        {
            if (buffer.First.Length >= 7)
            {
                channel = NetworkOrderDeserializer.ReadUInt16(buffer.First.Span.Slice(1, 2));
                payloadSize = NetworkOrderDeserializer.ReadInt32(buffer.First.Span.Slice(3, 4)); // FIXME - throw exn on unreasonable value
            }
            else
            {
                Span<byte> headerBytes = stackalloc byte[6];
                buffer.Slice(1, 6).CopyTo(headerBytes);
                channel = NetworkOrderDeserializer.ReadUInt16(headerBytes.Slice(0, 2));
                payloadSize = NetworkOrderDeserializer.ReadInt32(headerBytes.Slice(2, 4)); // FIXME - throw exn on unreasonable value
            }
        }

        public byte[] TakeoverPayload()
        {
            return _rentedArray;
        }

        public void ReturnPayload()
        {
            ArrayPool<byte>.Shared.Return(_rentedArray);
        }

        public override string ToString()
        {
            return $"(type={Type}, channel={Channel}, {Payload.Length} bytes of payload)";
        }
    }

    internal enum FrameType : int
    {
        FrameMethod = Constants.FrameMethod,
        FrameHeader = Constants.FrameHeader,
        FrameBody = Constants.FrameBody,
        FrameHeartbeat = Constants.FrameHeartbeat
    }
}

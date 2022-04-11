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
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Logging;
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
        private const int StartPayload = 7;

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteTo<T>(Span<byte> span, ushort channel, ref T method) where T : struct, IOutgoingAmqpMethod
            {
                const int StartClassId = StartPayload;
                const int StartMethodArguments = StartClassId + 4;

                int payloadLength = method.WriteTo(span.Slice(StartMethodArguments)) + 4;
                NetworkOrderSerializer.WriteUInt64(ref span.GetStart(), ((ulong)Constants.FrameMethod << 56) | ((ulong)channel << 40) | ((ulong)payloadLength << 8));
                NetworkOrderSerializer.WriteUInt32(ref span.GetOffset(StartClassId), (uint)method.ProtocolCommandId);
                span[payloadLength + StartPayload] = Constants.FrameEnd;
                return payloadLength + BaseFrameSize;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteTo<T>(Span<byte> span, ushort channel, ref T header, int bodyLength) where T : IAmqpHeader
            {
                const int StartClassId = StartPayload;
                const int StartBodyLength = StartPayload + 4;
                const int StartHeaderArguments = StartPayload + 12;

                int payloadLength = 12 + header.WriteTo(span.Slice(StartHeaderArguments));
                NetworkOrderSerializer.WriteUInt64(ref span.GetStart(), ((ulong)Constants.FrameHeader << 56) | ((ulong)channel << 40) | ((ulong)payloadLength << 8));
                NetworkOrderSerializer.WriteUInt32(ref span.GetOffset(StartClassId), (uint)header.ProtocolClassId << 16); // The last 16 bytes (Weight) aren't used
                NetworkOrderSerializer.WriteUInt64(ref span.GetOffset(StartBodyLength), (ulong)bodyLength);
                span[payloadLength + StartPayload] = Constants.FrameEnd;
                return payloadLength + BaseFrameSize;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteTo(Span<byte> span, ushort channel, ReadOnlySpan<byte> body)
            {
                const int StartBodyArgument = StartPayload;
                NetworkOrderSerializer.WriteUInt64(ref span.GetStart(), ((ulong)Constants.FrameBody << 56) | ((ulong)channel << 40) | ((ulong)body.Length << 8));
                body.CopyTo(span.Slice(StartBodyArgument));
                span[StartPayload + body.Length] = Constants.FrameEnd;
                return body.Length + BaseFrameSize;
            }
        }

        internal static class Heartbeat
        {
            /* Empty frame */
            public const int FrameSize = BaseFrameSize;

            /// <summary>
            /// Compiler trick to directly refer to static data in the assembly, see here: https://github.com/dotnet/roslyn/pull/24621
            /// </summary>
            internal static ReadOnlySpan<byte> Payload => new byte[]
            {
                Constants.FrameHeartbeat,
                0, 0, // channel
                0, 0, 0, 0, // payload length
                Constants.FrameEnd
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeToFrames<TMethod, TBufferWriter>(ref TMethod method, TBufferWriter pipeWriter, ushort channelNumber) where TMethod : struct, IOutgoingAmqpMethod where TBufferWriter : IBufferWriter<byte>
        {
            int size = Method.FrameSize + method.GetRequiredBufferSize();
            Span<byte> outputBuffer = pipeWriter.GetSpan(size);
            int offset = Method.WriteTo(outputBuffer, channelNumber, ref method);
            System.Diagnostics.Debug.Assert(offset == size, "Serialized to wrong size", "Serialized to wrong size, expect {0:N0}, offset {1:N0}", size, offset);
            pipeWriter.Advance(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeToFrames<TMethod, THeader, TBufferWriter>(ref TMethod method, ref THeader header, ReadOnlyMemory<byte> body, TBufferWriter pipeWriter, ushort channelNumber, int maxBodyPayloadBytes)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
            where TBufferWriter : IBufferWriter<byte>
        {
            int remainingBodyBytes = body.Length;
            int size = Method.FrameSize + Header.FrameSize +
                       method.GetRequiredBufferSize() + header.GetRequiredBufferSize() +
                       BodySegment.FrameSize * GetBodyFrameCount(maxBodyPayloadBytes, remainingBodyBytes) + remainingBodyBytes;

            // Will be returned by SocketFrameWriter.WriteLoop
            Span<byte> outputBuffer = pipeWriter.GetSpan(size);

            int offset = Method.WriteTo(outputBuffer, channelNumber, ref method);
            offset += Header.WriteTo(outputBuffer.Slice(offset), channelNumber, ref header, remainingBodyBytes);
            var bodySpan = body.Span;
            while (remainingBodyBytes > 0)
            {
                int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                offset += BodySegment.WriteTo(outputBuffer.Slice(offset), channelNumber, bodySpan.Slice(bodySpan.Length - remainingBodyBytes, frameSize));
                remainingBodyBytes -= frameSize;
            }

            System.Diagnostics.Debug.Assert(offset == size, "Serialized to wrong size", "Serialized to wrong size, expect {0:N0}, offset {1:N0}", size, offset);
            pipeWriter.Advance(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBodyFrameCount(int maxPayloadBytes, int length)
        {
            if (maxPayloadBytes == int.MaxValue)
            {
                return 1;
            }

            return (length + maxPayloadBytes - 1) / maxPayloadBytes;
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

        internal static void ProcessProtocolHeader(ReadOnlySequence<byte> buffer)
        {
            Span<byte> protocolSpan = stackalloc byte[7];
            buffer.Slice(1, 7).CopyTo(protocolSpan);
            if (protocolSpan[0] != 'M' || protocolSpan[1] != 'Q' || protocolSpan[2] != 'P')
            {
                throw new MalformedFrameException("Invalid AMQP protocol header from server");
            }

            throw new PacketNotRecognizedException(protocolSpan[3], protocolSpan[4], protocolSpan[5], protocolSpan[6]);
        }

        public static bool TryParseInboundFrame(ref ReadOnlySequence<byte> buffer, out InboundFrame frame)
        {
            int payloadSize = NetworkOrderDeserializer.ReadInt32(buffer.Slice(3, 4)); // FIXME - throw exn on unreasonable value
            int frameSize = payloadSize + 8;
            if (buffer.Length < frameSize)
            {
                frame = default;
                return false;
            }

            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(frameSize);
            buffer.Slice(0, frameSize).CopyTo(payloadBytes);
            if (payloadBytes[frameSize - 1] != Constants.FrameEnd)
            {
                ArrayPool<byte>.Shared.Return(payloadBytes);
                frame = default;
                return ThrowMalformedFrameException(payloadBytes[frameSize - 1]);
            }

            buffer = buffer.Slice(frameSize);
            RabbitMqClientEventSource.Log.DataReceived(frameSize);
            FrameType frameType = (FrameType)payloadBytes[0];
            ushort channel = NetworkOrderDeserializer.ReadUInt16(ref Unsafe.AsRef(payloadBytes[1]));
            frame = new InboundFrame(frameType, channel, payloadBytes.AsMemory(7, payloadSize), payloadBytes);
            return true;
        }

        private static bool ThrowMalformedFrameException(byte value)
        {
            throw new MalformedFrameException($"Bad frame end marker: {value}");
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

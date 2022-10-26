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
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

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
            /// A heartbeat frame has the following layout:
            /// +--------------------+------------------+-----------------+--------------------------+
            /// | Frame Type(1 byte) | Channel(2 bytes) | Length(4 bytes) | End Frame Marker(1 byte) |
            /// +--------------------+------------------+-----------------+--------------------------+
            /// | 0x08               | 0x0000           | 0x00000000      | 0xCE                     |
            /// +--------------------+------------------+-----------------+--------------------------+
            ///</summary>
            private static ReadOnlySpan<byte> Payload => new byte[] { Constants.FrameHeartbeat, 0, 0, 0, 0, 0, 0, Constants.FrameEnd };

            public static Memory<byte> GetHeartbeatFrame()
            {
                // Is returned by SocketFrameHandler.WriteLoop
                byte[] buffer = ArrayPool<byte>.Shared.Rent(FrameSize);
                Payload.CopyTo(buffer);
                return new Memory<byte>(buffer, 0, FrameSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<byte> SerializeToFrames<T>(ref T method, ushort channelNumber)
            where T : struct, IOutgoingAmqpMethod
        {
            int size = Method.FrameSize + method.GetRequiredBufferSize();

            // Will be returned by SocketFrameWriter.WriteLoop
            var array = ArrayPool<byte>.Shared.Rent(size);
            int offset = Method.WriteTo(array, channelNumber, ref method);

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return new ReadOnlyMemory<byte>(array, 0, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<byte> SerializeToFrames<TMethod, THeader>(ref TMethod method, ref THeader header, ReadOnlyMemory<byte> body, ushort channelNumber, int maxBodyPayloadBytes)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            int remainingBodyBytes = body.Length;
            int size = Method.FrameSize + Header.FrameSize +
                       method.GetRequiredBufferSize() + header.GetRequiredBufferSize() +
                       BodySegment.FrameSize * GetBodyFrameCount(maxBodyPayloadBytes, remainingBodyBytes) + remainingBodyBytes;

            // Will be returned by SocketFrameWriter.WriteLoop
            var array = ArrayPool<byte>.Shared.Rent(size);

            int offset = Method.WriteTo(array, channelNumber, ref method);
            offset += Header.WriteTo(array.AsSpan(offset), channelNumber, ref header, remainingBodyBytes);
            var bodySpan = body.Span;
            while (remainingBodyBytes > 0)
            {
                int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                offset += BodySegment.WriteTo(array.AsSpan(offset), channelNumber, bodySpan.Slice(bodySpan.Length - remainingBodyBytes, frameSize));
                remainingBodyBytes -= frameSize;
            }

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return new ReadOnlyMemory<byte>(array, 0, size);
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

        private static void ProcessProtocolHeader(ReadOnlySequence<byte> buffer)
        {
            try
            {
                if (buffer.Length < 8)
                {
                    throw new EndOfStreamException();
                }

                Span<byte> tempSpan = stackalloc byte[8];
                buffer.Slice(0, 8).CopyTo(tempSpan);

                if (tempSpan[1] != 'M' || tempSpan[2] != 'Q' || tempSpan[3] != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                throw new PacketNotRecognizedException(tempSpan[4], tempSpan[5], tempSpan[6], tempSpan[7]);
            }
            catch (EndOfStreamException)
            {
                // Ideally we'd wrap the EndOfStreamException in the
                // MalformedFrameException, but unfortunately the
                // design of MalformedFrameException's superclass,
                // ProtocolViolationException, doesn't permit
                // this. Fortunately, the call stack in the
                // EndOfStreamException is largely irrelevant at this
                // point, so can safely be ignored.
                throw new MalformedFrameException("Invalid AMQP protocol header from server");
            }
        }

        internal static async ValueTask<InboundFrame> ReadFromPipeAsync(PipeReader reader, uint maxMessageSize)
        {
            ReadResult result = await reader.ReadAsync().ConfigureAwait(false);

            ReadOnlySequence<byte> buffer = result.Buffer;
            if (result.IsCompleted || buffer.Length == 0)
            {
                throw new EndOfStreamException("Pipe is completed.");
            }

            InboundFrame frame;
            // Loop until we have enough data to read an entire frame, or until the pipe is completed.
            while (!TryReadFrame(ref buffer, maxMessageSize, out frame))
            {
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Not enough 
                result = await reader.ReadAsync().ConfigureAwait(false);

                if (result.IsCompleted || buffer.Length == 0)
                {
                    throw new EndOfStreamException("Pipe is completed.");
                }

                buffer = result.Buffer;
            }

            reader.AdvanceTo(buffer.Start);
            return frame;
        }

        internal static bool TryReadFrameFromPipe(PipeReader reader, uint maxMessageSize, out InboundFrame frame)
        {
            if (reader.TryRead(out ReadResult result))
            {
                ReadOnlySequence<byte> buffer = result.Buffer;
                if (result.IsCompleted || buffer.Length == 0)
                {
                    throw new EndOfStreamException("Pipe is completed.");
                }

                if (TryReadFrame(ref buffer, maxMessageSize, out frame))
                {
                    reader.AdvanceTo(buffer.Start);
                    return true;
                }

                // We didn't read enough, so let's signal how much of the buffer we examined.
                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            // Failed to synchronously read sufficient data from the pipe. We'll need to go async.
            frame = default;
            return false;
        }

        internal static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, uint maxMessageSize, out InboundFrame frame)
        {
            if (buffer.Length < 7)
            {
                frame = default;
                return false;
            }

            byte firstByte = buffer.First.Span[0];
            if (firstByte == 'A')
            {
                ProcessProtocolHeader(buffer);
            }

            FrameType type = (FrameType)firstByte;
            int channel = NetworkOrderDeserializer.ReadUInt16(buffer.Slice(1));
            int payloadSize = NetworkOrderDeserializer.ReadInt32(buffer.Slice(3));
            if ((maxMessageSize > 0) && (payloadSize > maxMessageSize))
            {
                string msg = $"Frame payload size '{payloadSize}' exceeds maximum of '{maxMessageSize}' bytes";
                throw new MalformedFrameException(message: msg, canShutdownCleanly: false);
            }

            const int EndMarkerLength = 1;
            // Is returned by InboundFrame.ReturnPayload in Connection.MainLoopIteration
            int readSize = payloadSize + EndMarkerLength;

            if ((buffer.Length - 7) < readSize)
            {
                frame = default;
                return false;
            }
            else
            {
                byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(readSize);
                ReadOnlySequence<byte> framePayload = buffer.Slice(7, readSize);
                framePayload.CopyTo(payloadBytes);

                if (payloadBytes[payloadSize] != Constants.FrameEnd)
                {
                    ArrayPool<byte>.Shared.Return(payloadBytes);
                    throw new MalformedFrameException($"Bad frame end marker: {payloadBytes[payloadSize]}");
                }

                RabbitMqClientEventSource.Log.DataReceived(payloadSize + Framing.BaseFrameSize);
                frame = new InboundFrame(type, channel, new Memory<byte>(payloadBytes, 0, payloadSize), payloadBytes);
                // Advance the buffer
                buffer = buffer.Slice(7 + readSize);
                return true;
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

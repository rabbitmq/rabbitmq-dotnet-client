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
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.Logging;
using RabbitMQ.Client.Util;

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
            public const int ArgumentsOffset = 4;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteTo<T>(Span<byte> span, ushort channel, ref T method) where T : struct, IOutgoingAmqpMethod
            {
                const int StartClassId = StartPayload;
                const int StartMethodArguments = StartPayload + ArgumentsOffset;

                int payloadLength = ArgumentsOffset + method.WriteTo(span.Slice(StartMethodArguments));
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
            public const int BodyLengthOffset = 4;
            public const int HeaderArgumentOffset = 12;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteTo<T>(Span<byte> span, ushort channel, ref T header, int bodyLength) where T : IAmqpHeader
            {
                const int StartClassId = StartPayload;
                const int StartBodyLength = StartPayload + BodyLengthOffset;
                const int StartHeaderArguments = StartPayload + HeaderArgumentOffset;

                int payloadLength = HeaderArgumentOffset + header.WriteTo(span.Slice(StartHeaderArguments));
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

            public static RentedMemory GetHeartbeatFrame()
            {
                // Is returned by SocketFrameHandler.WriteLoop
                byte[] buffer = ArrayPool<byte>.Shared.Rent(FrameSize);
                Payload.CopyTo(buffer);
                var mem = new ReadOnlyMemory<byte>(buffer, 0, FrameSize);
                return new RentedMemory(mem, buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RentedMemory SerializeToFrames<T>(ref T method, ushort channelNumber)
            where T : struct, IOutgoingAmqpMethod
        {
            int size = Method.FrameSize + method.GetRequiredBufferSize();

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] array = ArrayPool<byte>.Shared.Rent(size);
            int offset = Method.WriteTo(array, channelNumber, ref method);

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            var mem = new ReadOnlyMemory<byte>(array, 0, size);
            return new RentedMemory(mem, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RentedMemory SerializeToFrames<TMethod, THeader>(ref TMethod method, ref THeader header, ReadOnlyMemory<byte> body, ushort channelNumber, int maxBodyPayloadBytes)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            int remainingBodyBytes = body.Length;
            int size = Method.FrameSize + Header.FrameSize +
                       method.GetRequiredBufferSize() + header.GetRequiredBufferSize() +
                       BodySegment.FrameSize * GetBodyFrameCount(maxBodyPayloadBytes, remainingBodyBytes) + remainingBodyBytes;

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] array = ArrayPool<byte>.Shared.Rent(size);

            int offset = Method.WriteTo(array, channelNumber, ref method);
            offset += Header.WriteTo(array.AsSpan(offset), channelNumber, ref header, remainingBodyBytes);
            ReadOnlySpan<byte> bodySpan = body.Span;
            while (remainingBodyBytes > 0)
            {
                int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                offset += BodySegment.WriteTo(array.AsSpan(offset), channelNumber, bodySpan.Slice(bodySpan.Length - remainingBodyBytes, frameSize));
                remainingBodyBytes -= frameSize;
            }

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            var mem = new ReadOnlyMemory<byte>(array, 0, size);
            return new RentedMemory(mem, array);
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

    internal sealed class InboundFrame
    {
        public FrameType Type { get; private set; }
        public int Channel { get; private set; }
        public ReadOnlyMemory<byte> Payload { get; private set; }
        private byte[]? _rentedArray;

        private static void ProcessProtocolHeader(ReadOnlySequence<byte> buffer)
        {
            // Probably an AMQP.... header indicating a version mismatch.
            // Otherwise meaningless, so try to read the version and throw an exception, whether we
            // read the version correctly or not.
            try
            {
                if (buffer.Length < 8)
                {
                    throw new EndOfStreamException();
                }

                ReadOnlySpan<byte> bufferSpan = buffer.First.Span;

                if (bufferSpan[1] != 'M' || bufferSpan[2] != 'Q' || bufferSpan[3] != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                throw new PacketNotRecognizedException(bufferSpan[4], bufferSpan[5], bufferSpan[6], bufferSpan[7]);
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

        internal static async ValueTask ReadFromPipeAsync(PipeReader reader,
            uint maxInboundMessageBodySize, InboundFrame frame, CancellationToken mainLoopCancellationToken)
        {
            ReadResult result = await reader.ReadAsync(mainLoopCancellationToken).ConfigureAwait(false);

            ReadOnlySequence<byte> buffer = result.Buffer;

            MaybeThrowEndOfStream(result, buffer);

            // Loop until we have enough data to read an entire frame, or until the pipe is completed.
            while (!TryReadFrame(ref buffer, maxInboundMessageBodySize, frame))
            {
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Not enough data, read a bit more
                result = await reader.ReadAsync(mainLoopCancellationToken)
                    .ConfigureAwait(false);

                MaybeThrowEndOfStream(result, buffer);

                buffer = result.Buffer;
            }

            reader.AdvanceTo(buffer.Start);
        }

        internal static bool TryReadFrameFromPipe(PipeReader reader,
            uint maxInboundMessageBodySize, InboundFrame frame)
        {
            if (reader.TryRead(out ReadResult result))
            {
                ReadOnlySequence<byte> buffer = result.Buffer;

                MaybeThrowEndOfStream(result, buffer);

                if (TryReadFrame(ref buffer, maxInboundMessageBodySize, frame))
                {
                    reader.AdvanceTo(buffer.Start);
                    return true;
                }

                // We didn't read enough, so let's signal how much of the buffer we examined.
                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            // Failed to synchronously read sufficient data from the pipe. We'll need to go async.
            return false;
        }

        internal static bool TryReadFrame(ref ReadOnlySequence<byte> buffer,
            uint maxInboundMessageBodySize, InboundFrame frame)
        {
            if (buffer.Length < 7)
            {
                return false;
            }

            /*
             * Note:
             * The use of buffer.Slice seems to take all segments into account, thus there appears to be no need to check IsSingleSegment
             * Debug.Assert(buffer.IsSingleSegment);
             * In addition, the TestBasicRoundtripConcurrentManyMessages asserts that the consumed message bodies are equivalent to
             * the published bodies, and if there were an issue parsing frames, it would show up there for sure.
             * https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1516#issuecomment-1991943017
             */

            byte firstByte = buffer.First.Span[0];
            if (firstByte == 'A')
            {
                ProcessProtocolHeader(buffer);
            }

            frame.Type = (FrameType)firstByte;
            frame.Channel = NetworkOrderDeserializer.ReadUInt16(buffer.Slice(1));
            int payloadSize = NetworkOrderDeserializer.ReadInt32(buffer.Slice(3));
            if ((maxInboundMessageBodySize > 0) && (payloadSize > maxInboundMessageBodySize))
            {
                string msg = $"Frame payload size '{payloadSize}' exceeds maximum of '{maxInboundMessageBodySize}' bytes";
                throw new MalformedFrameException(message: msg, canShutdownCleanly: false);
            }

            const int EndMarkerLength = 1;
            // Is returned by InboundFrame.ReturnPayload in Connection.MainLoopIteration
            int readSize = payloadSize + EndMarkerLength;

            if (buffer.Length - 7 < readSize)
            {
                return false;
            }

            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(readSize);
            ReadOnlySequence<byte> framePayload = buffer.Slice(7, readSize);
            framePayload.CopyTo(payloadBytes);

            if (payloadBytes[payloadSize] != Constants.FrameEnd)
            {
                byte frameEndMarker = payloadBytes[payloadSize];
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Bad frame end marker: {frameEndMarker}");
            }

            RabbitMqClientEventSource.Log.DataReceived(payloadSize + Framing.BaseFrameSize);
            frame._rentedArray = payloadBytes;
            frame.Payload = new ReadOnlyMemory<byte>(payloadBytes, 0, payloadSize);

            // Advance the buffer
            buffer = buffer.Slice(7 + readSize);
            return true;
        }

        public RentedMemory TakeoverPayload(int sliceOffset)
        {
            byte[]? array = _rentedArray ?? throw new InvalidOperationException("Payload was already taken over or returned.");
            ReadOnlyMemory<byte> payload = Payload.Slice(sliceOffset);
            Payload = ReadOnlyMemory<byte>.Empty;
            _rentedArray = null;
            return new RentedMemory(payload, array);
        }

        public void TryReturnPayload()
        {
            byte[]? array = _rentedArray;
            if (array is null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(array);
            Payload = ReadOnlyMemory<byte>.Empty;
            _rentedArray = null;
        }

        public override string ToString()
        {
            return $"(type={Type}, channel={Channel}, {Payload.Length} bytes of payload)";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MaybeThrowEndOfStream(ReadResult result, ReadOnlySequence<byte> buffer)
        {
            // https://blog.marcgravell.com/2018/07/pipe-dreams-part-1.html
            // Uses &&
            if (result.IsCompleted && buffer.IsEmpty)
            {
                throw new EndOfStreamException("Pipe is completed.");
            }
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

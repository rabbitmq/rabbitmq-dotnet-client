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
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
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
            public static int WriteTo(ref SequenceBuilder<byte> data, Memory<byte> buffer, ushort channel, ReadOnlySequence<byte> body, bool copyBody)
            {
                const int StartBodyArgument = StartPayload;
                NetworkOrderSerializer.WriteUInt64(ref buffer.Span.GetStart(), ((ulong)Constants.FrameBody << 56) | ((ulong)channel << 40) | ((ulong)body.Length << 8));

                if (copyBody)
                {
                    int length = (int)body.Length;
                    Span<byte> span = buffer.Span;

                    body.CopyTo(span.Slice(StartBodyArgument));
                    span[StartPayload + length] = Constants.FrameEnd;

                    data.Append(buffer.Slice(0, length + BaseFrameSize));

                    return length + BaseFrameSize;
                }

                data.Append(buffer.Slice(0, StartBodyArgument));
                data.Append(body);
                buffer.Span[StartBodyArgument] = Constants.FrameEnd;
                data.Append(buffer.Slice(StartBodyArgument, 1));
                return BaseFrameSize;
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

            public static RentedOutgoingMemory GetHeartbeatFrame()
            {
                // Is returned by SocketFrameHandler.WriteLoop
                byte[] buffer = ClientArrayPool.Rent(FrameSize);
                Payload.CopyTo(buffer);
                var mem = new ReadOnlyMemory<byte>(buffer, 0, FrameSize);
                return RentedOutgoingMemory.GetAndInitialize(mem, buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RentedOutgoingMemory SerializeToFrames<T>(ref T method, ushort channelNumber)
            where T : struct, IOutgoingAmqpMethod
        {
            int size = Method.FrameSize + method.GetRequiredBufferSize();

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] array = ClientArrayPool.Rent(size);
            int offset = Method.WriteTo(array, channelNumber, ref method);

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            var mem = new ReadOnlyMemory<byte>(array, 0, size);
            return RentedOutgoingMemory.GetAndInitialize(mem, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RentedOutgoingMemory SerializeToFrames<TMethod, THeader>(ref TMethod method, ref THeader header, ReadOnlySequence<byte> body, ushort channelNumber, int maxBodyPayloadBytes, bool copyBody = true)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            int remainingBodyBytes = (int)body.Length;
            int size = Method.FrameSize + Header.FrameSize +
                       method.GetRequiredBufferSize() + header.GetRequiredBufferSize() +
                       BodySegment.FrameSize * GetBodyFrameCount(maxBodyPayloadBytes, remainingBodyBytes);

            if (copyBody)
            {
                size += remainingBodyBytes;
            }

            // Will be returned by SocketFrameWriter.WriteLoop
            byte[] array = ClientArrayPool.Rent(size);

            SequenceBuilder<byte> sequenceBuilder = new SequenceBuilder<byte>();
            Memory<byte> buffer = array.AsMemory();

            int offset = Method.WriteTo(array, channelNumber, ref method);
            offset += Header.WriteTo(array.AsSpan(offset), channelNumber, ref header, remainingBodyBytes);

            sequenceBuilder.Append(buffer.Slice(0, offset));
            buffer = buffer.Slice(offset);

            ReadOnlySequence<byte> remainingBody = body;
            while (remainingBodyBytes > 0)
            {
                int frameSize = remainingBodyBytes > maxBodyPayloadBytes ? maxBodyPayloadBytes : remainingBodyBytes;
                int segmentSize = BodySegment.WriteTo(ref sequenceBuilder, buffer, channelNumber, remainingBody.Slice(remainingBody.Length - remainingBodyBytes, frameSize), copyBody);

                buffer = buffer.Slice(segmentSize);
                offset += segmentSize;
                remainingBodyBytes -= frameSize;
            }

            System.Diagnostics.Debug.Assert(offset == size, $"Serialized to wrong size, expect {size}, offset {offset}");
            return RentedOutgoingMemory.GetAndInitialize(sequenceBuilder.Build(), array, waitSend: !copyBody);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RentedOutgoingMemory SerializeToFrames<TMethod, THeader>(ref TMethod method, ref THeader header, ReadOnlyMemory<byte> body, ushort channelNumber, int maxBodyPayloadBytes)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            return SerializeToFrames(ref method, ref header, new ReadOnlySequence<byte>(body), channelNumber, maxBodyPayloadBytes);
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

        internal static async ValueTask<InboundFrame> ReadFromPipeAsync(PipeReader reader, uint maxMessageSize)
        {
            ReadResult result = await reader.ReadAsync()
               .ConfigureAwait(false);

            ReadOnlySequence<byte> buffer = result.Buffer;

            MaybeThrowEndOfStream(result, buffer);

            InboundFrame frame;
            // Loop until we have enough data to read an entire frame, or until the pipe is completed.
            while (!TryReadFrame(ref buffer, maxMessageSize, out frame))
            {
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Not enough data, read a bit more
                result = await reader.ReadAsync()
                   .ConfigureAwait(false);

                MaybeThrowEndOfStream(result, buffer);

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

                MaybeThrowEndOfStream(result, buffer);

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

            // TODO check this?
            // buffer.IsSingleSegment;

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
                byte[] payloadBytes = ClientArrayPool.Rent(readSize);
                ReadOnlySequence<byte> framePayload = buffer.Slice(7, readSize);
                framePayload.CopyTo(payloadBytes);

                if (payloadBytes[payloadSize] != Constants.FrameEnd)
                {
                    byte frameEndMarker = payloadBytes[payloadSize];
                    ClientArrayPool.Return(payloadBytes);
                    throw new MalformedFrameException($"Bad frame end marker: {frameEndMarker}");
                }

                RabbitMqClientEventSource.Log.DataReceived(payloadSize + Framing.BaseFrameSize);
                frame = new InboundFrame(type, channel, new ReadOnlyMemory<byte>(payloadBytes, 0, payloadSize), payloadBytes);
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
            ClientArrayPool.Return(_rentedArray);
        }

        public override string ToString()
        {
            return $"(type={Type}, channel={Channel}, {Payload.Length} bytes of payload)";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MaybeThrowEndOfStream(ReadResult result, ReadOnlySequence<byte> buffer)
        {
            // TODO
            // https://blog.marcgravell.com/2018/07/pipe-dreams-part-1.html
            // Uses &&
            // if (result.IsCompleted && buffer.IsEmpty)
            if (result.IsCompleted || buffer.IsEmpty)
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

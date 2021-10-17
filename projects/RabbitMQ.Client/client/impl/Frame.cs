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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

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

                int payloadLength = method.WriteArgumentsTo(span.Slice(StartMethodArguments)) + 4;
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
            public static int WriteTo<T>(Span<byte> span, ushort channel, T header, int bodyLength) where T : ContentHeaderBase
            {
                const int StartClassId = StartPayload;
                const int StartBodyLength = StartPayload + 4;
                const int StartHeaderArguments = StartPayload + 12;

                int payloadLength = 12 + header.WritePropertiesTo(span.Slice(StartHeaderArguments));
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
            private static ReadOnlySpan<byte> Payload => new byte[]
            {
                Constants.FrameHeartbeat,
                0, 0, // channel
                0, 0, 0, 0, // payload length
                Constants.FrameEnd
            };

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
        public static ReadOnlyMemory<byte> SerializeToFrames<T>(ref T method, ContentHeaderBase header, ReadOnlyMemory<byte> body, ushort channelNumber, int maxBodyPayloadBytes)
            where T : struct, IOutgoingAmqpMethod
        {
            int remainingBodyBytes = body.Length;
            int size = Method.FrameSize + Header.FrameSize +
                       method.GetRequiredBufferSize() + header.GetRequiredPayloadBufferSize() +
                       BodySegment.FrameSize * GetBodyFrameCount(maxBodyPayloadBytes, remainingBodyBytes) + remainingBodyBytes;

            // Will be returned by SocketFrameWriter.WriteLoop
            var array = ArrayPool<byte>.Shared.Rent(size);

            int offset = Method.WriteTo(array, channelNumber, ref method);
            offset += Header.WriteTo(array.AsSpan(offset), channelNumber, header, remainingBodyBytes);
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

    internal readonly ref struct InboundFrame
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

        private static void ProcessProtocolHeader(Stream reader, ReadOnlySpan<byte> frameHeader)
        {
            try
            {
                if (frameHeader[0] != 'M' || frameHeader[1] != 'Q' || frameHeader[2] != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                int serverMinor = reader.ReadByte();
                if (serverMinor == -1)
                {
                    throw new EndOfStreamException();
                }

                throw new PacketNotRecognizedException(frameHeader[3], frameHeader[4], frameHeader[5], serverMinor);
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

        internal static InboundFrame ReadFrom(Stream reader, byte[] frameHeaderBuffer)
        {
            try
            {
                ReadFromStream(reader, frameHeaderBuffer, frameHeaderBuffer.Length);
            }
            catch (IOException ioe)
            {
                // If it's a WSAETIMEDOUT SocketException, unwrap it.
                // This might happen when the limit of half-open connections is
                // reached.
                if (ioe?.InnerException is SocketException exception && exception.SocketErrorCode == SocketError.TimedOut)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }
                else
                {
                    throw;
                }
            }

            byte firstByte = frameHeaderBuffer[0];
            if (firstByte == 'A')
            {
                // Probably an AMQP protocol header, otherwise meaningless
                ProcessProtocolHeader(reader, frameHeaderBuffer.AsSpan(1, 6));
            }

            FrameType type = (FrameType)firstByte;
            var frameHeaderSpan = new ReadOnlySpan<byte>(frameHeaderBuffer, 1, 6);
            int channel = NetworkOrderDeserializer.ReadUInt16(frameHeaderSpan);
            int payloadSize = NetworkOrderDeserializer.ReadInt32(frameHeaderSpan.Slice(2, 4)); // FIXME - throw exn on unreasonable value

            const int EndMarkerLength = 1;
            // Is returned by InboundFrame.ReturnPayload in Connection.MainLoopIteration
            int readSize = payloadSize + EndMarkerLength;
            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(readSize);
            try
            {
                ReadFromStream(reader, payloadBytes, readSize);
            }
            catch (Exception)
            {
                // Early EOF.
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Short frame - expected to read {readSize} bytes");
            }

            if (payloadBytes[payloadSize] != Constants.FrameEnd)
            {
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Bad frame end marker: {payloadBytes[payloadSize]}");
            }

            RabbitMqClientEventSource.Log.DataReceived(payloadSize + Framing.BaseFrameSize);
            return new InboundFrame(type, channel, new Memory<byte>(payloadBytes, 0, payloadSize), payloadBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadFromStream(Stream reader, byte[] buffer, int toRead)
        {
            int bytesRead = 0;
            do
            {
                int read = reader.Read(buffer, bytesRead, toRead - bytesRead);
                if (read == 0)
                {
                    ThrowEndOfStream();
                }

                bytesRead += read;
            } while (bytesRead != toRead);

            static void ThrowEndOfStream()
            {
                throw new EndOfStreamException("Reached the end of the stream. Possible authentication failure.");
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

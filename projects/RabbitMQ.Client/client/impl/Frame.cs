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
using System.Runtime.InteropServices;
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
        private const int BaseFrameSize = 1 + 2 + 4 + 1;
        private const int StartPayload = 7;

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

        internal static class Method
        {
            /* +----------+-----------+-----------+
             * | Class Id | Method Id | Arguments |
             * +----------+-----------+-----------+
             * | 2 bytes  | 2 bytes   | x bytes   |
             * +----------+-----------+-----------+ */
            public const int FrameSize = BaseFrameSize + 2 + 2;

            public static int WriteTo(Span<byte> span, ushort channel, MethodBase method)
            {
                const int StartClassId = StartPayload;
                const int StartMethodId = StartPayload + 2;
                const int StartMethodArguments = StartPayload + 4;

                NetworkOrderSerializer.WriteUInt16(span.Slice(StartClassId), method.ProtocolClassId);
                NetworkOrderSerializer.WriteUInt16(span.Slice(StartMethodId), method.ProtocolMethodId);
                var argWriter = new MethodArgumentWriter(span.Slice(StartMethodArguments));
                method.WriteArgumentsTo(ref argWriter);
                return WriteBaseFrame(span, FrameType.FrameMethod, channel, StartMethodArguments - StartPayload + argWriter.Offset);
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
                var headerWriter = new ContentHeaderPropertyWriter(span.Slice(StartHeaderArguments));
                header.WritePropertiesTo(ref headerWriter);
                return WriteBaseFrame(span, FrameType.FrameHeader, channel, StartHeaderArguments - StartPayload + headerWriter.Offset);
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
                var buffer = ArrayPool<byte>.Shared.Rent(FrameSize);
                Payload.CopyTo(buffer);
                return new Memory<byte>(buffer, 0, FrameSize);
            }
        }
    }

    internal readonly struct InboundFrame : IDisposable
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

        private static void ProcessProtocolHeader(Stream reader)
        {
            try
            {
                byte b1 = (byte)reader.ReadByte();
                byte b2 = (byte)reader.ReadByte();
                byte b3 = (byte)reader.ReadByte();
                if (b1 != 'M' || b2 != 'Q' || b3 != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                int transportHigh = reader.ReadByte();
                int transportLow = reader.ReadByte();
                int serverMajor = reader.ReadByte();
                int serverMinor = reader.ReadByte();
                throw new PacketNotRecognizedException(transportHigh, transportLow, serverMajor, serverMinor);
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
            int type = default;
            try
            {
                type = reader.ReadByte();
            }
            catch (IOException ioe)
            {
                // If it's a WSAETIMEDOUT SocketException, unwrap it.
                // This might happen when the limit of half-open connections is
                // reached.
                if (ioe.InnerException == null ||
                    !(ioe.InnerException is SocketException) ||
                    ((SocketException)ioe.InnerException).SocketErrorCode != SocketError.TimedOut)
                {
                    throw;
                }

                ExceptionDispatchInfo.Capture(ioe.InnerException).Throw();
            }

            switch (type)
            {
                case -1:
                    throw new EndOfStreamException("Reached the end of the stream. Possible authentication failure.");
                case 'A':
                    // Probably an AMQP protocol header, otherwise meaningless
                    ProcessProtocolHeader(reader);
                    break;
            }

            reader.Read(frameHeaderBuffer, 0, frameHeaderBuffer.Length);
            int channel = NetworkOrderDeserializer.ReadUInt16(new ReadOnlySpan<byte>(frameHeaderBuffer));
            int payloadSize = NetworkOrderDeserializer.ReadInt32(new ReadOnlySpan<byte>(frameHeaderBuffer, 2, 4)); // FIXME - throw exn on unreasonable value

            const int EndMarkerLength = 1;
            // Is returned by InboundFrame.Dispose in Connection.MainLoopIteration
            var readSize = payloadSize + EndMarkerLength;
            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(readSize);
            int bytesRead = 0;
            try
            {
                while (bytesRead < readSize)
                {
                    bytesRead += reader.Read(payloadBytes, bytesRead, readSize - bytesRead);
                }
            }
            catch (Exception)
            {
                // Early EOF.
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Short frame - expected to read {readSize} bytes, only got {bytesRead} bytes");
            }

            if (payloadBytes[payloadSize] != Constants.FrameEnd)
            {
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Bad frame end marker: {payloadBytes[payloadSize]}");
            }

            return new InboundFrame((FrameType)type, channel, new Memory<byte>(payloadBytes, 0, payloadSize), payloadBytes);
        }

        public void Dispose()
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

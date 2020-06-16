// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    readonly ref struct HeaderOutboundFrame
    {
        private readonly ushort _channel;
        private readonly ContentHeaderBase _header;
        private readonly int _bodyLength;

        internal HeaderOutboundFrame(ushort channel, ContentHeaderBase header, int bodyLength)
        {
            _channel = channel;
            _header = header;
            _bodyLength = bodyLength;
        }

        public ushort GetChannel() => _channel;

        public FrameType GetFrameType() => FrameType.FrameHeader;

        public int GetMinimumPayloadBufferSize()
        {
            // ProtocolClassId (2) + header (X bytes)
            return 2 + _header.GetRequiredBufferSize();
        }

        public int WritePayload(Memory<byte> memory)
        {
            // write protocol class id (2 bytes)
            NetworkOrderSerializer.WriteUInt16(memory.Span, _header.ProtocolClassId);
            // write header (X bytes)
            int bytesWritten = _header.WriteTo(memory.Slice(2), (ulong)_bodyLength);
            return 2 + bytesWritten;
        }
    }

    readonly ref struct BodySegmentOutboundFrame
    {
        private readonly ReadOnlyMemory<byte> _body;
        private readonly ushort _channel;

        internal BodySegmentOutboundFrame(ushort channel, ReadOnlyMemory<byte> bodySegment)
        {
            _channel = channel;
            _body = bodySegment;
        }

        public ushort GetChannel() => _channel;

        public FrameType GetFrameType() => FrameType.FrameBody;

        public int GetMinimumPayloadBufferSize()
        {
            return _body.Length;
        }

        public int WritePayload(Memory<byte> memory)
        {
            _body.CopyTo(memory);
            return _body.Length;
        }
    }

    readonly ref struct MethodOutboundFrame
    {
        private readonly MethodBase _method;
        private readonly ushort _channel;

        internal MethodOutboundFrame(ushort channel, MethodBase method)
        {
            _channel = channel;
            _method = method;
        }

        public ushort GetChannel() => _channel;

        public FrameType GetFrameType() => FrameType.FrameMethod;

        public int GetMinimumPayloadBufferSize()
        {
            // class id (2 bytes) + method id (2 bytes) + arguments (X bytes)
            return 4 + _method.GetRequiredBufferSize();
        }

        public int WritePayload(Memory<byte> memory)
        {
            NetworkOrderSerializer.WriteUInt16(memory.Span, _method.ProtocolClassId);
            NetworkOrderSerializer.WriteUInt16(memory.Slice(2).Span, _method.ProtocolMethodId);
            var argWriter = new MethodArgumentWriter(memory.Slice(4));
            _method.WriteArgumentsTo(ref argWriter);
            argWriter.Flush();
            return 4 + argWriter.Offset;
        }
    }

    readonly ref struct EmptyOutboundFrame
    {
        public ushort GetChannel() => 0;
        public FrameType GetFrameType() => FrameType.FrameHeartbeat;
        public int GetMinimumPayloadBufferSize() => 0;
        public int WritePayload(Memory<byte> memory) => 0;
    }

    interface IOutboundFrame
    {
        FrameType GetFrameType();
        ushort GetChannel();
        int WritePayload(Memory<byte> memory);
        int GetMinimumPayloadBufferSize();
    }

    internal static class OutboundFrameExtensions
    {
        internal static int GetMinimumBufferSize(this in EmptyOutboundFrame outboundFrame)
        {
            return 8 + outboundFrame.GetMinimumPayloadBufferSize();
        }

        internal static int GetMinimumBufferSize(this in MethodOutboundFrame outboundFrame)
        {
            return 8 + outboundFrame.GetMinimumPayloadBufferSize();
        }

        internal static int GetMinimumBufferSize(this in HeaderOutboundFrame outboundFrame)
        {
            return 8 + outboundFrame.GetMinimumPayloadBufferSize();
        }

        internal static int GetMinimumBufferSize(this in BodySegmentOutboundFrame outboundFrame)
        {
            return 8 + outboundFrame.GetMinimumPayloadBufferSize();
        }

        internal static void WriteTo(this in EmptyOutboundFrame frame, Memory<byte> memory)
        {
            memory.Span[0] = (byte)frame.GetFrameType();
            NetworkOrderSerializer.WriteUInt16(memory.Slice(1).Span, frame.GetChannel());
            int bytesWritten = frame.WritePayload(memory.Slice(7));
            NetworkOrderSerializer.WriteUInt32(memory.Slice(3).Span, (uint)bytesWritten);
            memory.Span[bytesWritten + 7] = Constants.FrameEnd;
        }

        internal static void WriteTo(this in MethodOutboundFrame frame, Memory<byte> memory)
        {
            memory.Span[0] = (byte)frame.GetFrameType();
            NetworkOrderSerializer.WriteUInt16(memory.Slice(1).Span, frame.GetChannel());
            int bytesWritten = frame.WritePayload(memory.Slice(7));
            NetworkOrderSerializer.WriteUInt32(memory.Slice(3).Span, (uint)bytesWritten);
            memory.Span[bytesWritten + 7] = Constants.FrameEnd;
        }

        internal static void WriteTo(this in HeaderOutboundFrame frame, Memory<byte> memory)
        {
            memory.Span[0] = (byte)frame.GetFrameType();
            NetworkOrderSerializer.WriteUInt16(memory.Slice(1).Span, frame.GetChannel());
            int bytesWritten = frame.WritePayload(memory.Slice(7));
            NetworkOrderSerializer.WriteUInt32(memory.Slice(3).Span, (uint)bytesWritten);
            memory.Span[bytesWritten + 7] = Constants.FrameEnd;
        }

        internal static void WriteTo(this in BodySegmentOutboundFrame frame, Memory<byte> memory)
        {
            memory.Span[0] = (byte)frame.GetFrameType();
            NetworkOrderSerializer.WriteUInt16(memory.Slice(1).Span, frame.GetChannel());
            int bytesWritten = frame.WritePayload(memory.Slice(7));
            NetworkOrderSerializer.WriteUInt32(memory.Slice(3).Span, (uint)bytesWritten);
            memory.Span[bytesWritten + 7] = Constants.FrameEnd;
        }
    }

    internal readonly struct InboundFrame : IDisposable
    {
        public readonly ReadOnlyMemory<byte> Payload;
        public readonly int Channel;
        public readonly FrameType Type;

        private InboundFrame(FrameType type, int channel, ReadOnlyMemory<byte> payload)
        {
            Payload = payload;
            Type = type;
            Channel = channel;
        }

        public bool IsMethod()
        {
            return Type == FrameType.FrameMethod;
        }
        public bool IsHeader()
        {
            return Type == FrameType.FrameHeader;
        }
        public bool IsBody()
        {
            return Type == FrameType.FrameBody;
        }
        public bool IsHeartbeat()
        {
            return Type == FrameType.FrameHeartbeat;
        }

        private static void ProcessProtocolHeader(ReadOnlySequence<byte> buffer, Span<byte> headerBytes)
        {
            try
            {
                // We have already read 7 bytes
                byte b1 = headerBytes[1];
                byte b2 = headerBytes[2];
                byte b3 = headerBytes[3];
                if (b1 != 'M' || b2 != 'Q' || b3 != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                int transportHigh = headerBytes[4];
                int transportLow = headerBytes[5];
                int serverMajor = headerBytes[6];
                buffer.Slice(7, 1).CopyTo(headerBytes.Slice(7));
                int serverMinor = headerBytes[7];
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

        internal static bool TryReadFrom(ReadOnlySequence<byte> buffer, out InboundFrame frame)
        {
            frame = default;
            if (buffer.Length > 7)
            {
                Span<byte> headerBytes = stackalloc byte[8];
                buffer.Slice(0, 7).CopyTo(headerBytes.Slice(0, 7));
                int type = default;
                try
                {
                    type = headerBytes[0];
                    if (type == -1)
                    {
                        throw new EndOfStreamException("Reached the end of the stream. Possible authentication failure.");
                    }
                }
                catch (IOException ioe)
                {
                    // If it's a WSAETIMEDOUT SocketException, unwrap it.
                    // This might happen when the limit of half-open connections is
                    // reached.
                    if (ioe?.InnerException is SocketException exception && exception.SocketErrorCode == SocketError.TimedOut)
                    {
                        ExceptionDispatchInfo.Capture(ioe.InnerException).Throw();
                    }

                    throw;
                }

                if (type == 'A')
                {
                    // Probably an AMQP protocol header, otherwise meaningless
                    ProcessProtocolHeader(buffer, headerBytes);
                }

                int channel = NetworkOrderDeserializer.ReadUInt16(headerBytes.Slice(1));
                int payloadSize = NetworkOrderDeserializer.ReadInt32(headerBytes.Slice(3)); // FIXME - throw exn on unreasonable value
                var payloadBuffer = buffer.Slice(7);
                if (payloadBuffer.Length >= payloadSize + 1)
                {
                    byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(payloadSize + 1);
                    Memory<byte> payloadWithEndMarker = payloadBytes.AsMemory(0, payloadSize + 1);
                    payloadBuffer.Slice(0, payloadSize + 1).CopyTo(payloadWithEndMarker.Span);

                    if (payloadWithEndMarker.Span[payloadWithEndMarker.Length - 1] != Constants.FrameEnd)
                    {
                        throw new MalformedFrameException("Bad frame end marker: " + payloadWithEndMarker.Span[payloadWithEndMarker.Length - 1]);
                    }

                    frame = new InboundFrame((FrameType)type, channel, payloadWithEndMarker.Slice(0, payloadSize));
                    return true;
                }

                return false;
            }

            return false;
        }

        public void Dispose()
        {
            if (MemoryMarshal.TryGetArray(Payload, out ArraySegment<byte> segment))
            {
                ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }
        
        public override string ToString()
        {
            return $"(type={Type}, channel={Channel}, {Payload.Length} bytes of payload)";
        }
    }

    class Frame
    {
        public Frame(FrameType type, int channel)
        {
            Type = type;
            Channel = channel;
            Payload = null;
        }

        public Frame(FrameType type, int channel, ReadOnlyMemory<byte> payload)
        {
            Type = type;
            Channel = channel;
            Payload = payload;
        }

        public int Channel { get; private set; }

        public ReadOnlyMemory<byte> Payload { get; private set; }

        public FrameType Type { get; private set; }

        public override string ToString()
        {
            return string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                Type,
                Channel,
                Payload.Length.ToString());
        }

        
    }

    enum FrameType : int
    {
        FrameMethod = 1,
        FrameHeader = 2,
        FrameBody = 3,
        FrameHeartbeat = 8,
        FrameEnd = 206,
        FrameMinSize = 4096
    }
}

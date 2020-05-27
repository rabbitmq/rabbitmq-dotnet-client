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
    class HeaderOutboundFrame : OutboundFrame
    {
        private readonly ContentHeaderBase _header;
        private readonly int _bodyLength;

        internal HeaderOutboundFrame(int channel, ContentHeaderBase header, int bodyLength) : base(FrameType.FrameHeader, channel)
        {
            _header = header;
            _bodyLength = bodyLength;
        }

        internal override int GetMinimumPayloadBufferSize()
        {
            // ProtocolClassId (2) + header (X bytes)
            return 2 + _header.GetRequiredBufferSize();
        }

        internal override int WritePayload(Span<byte> span)
        {
            // write protocol class id (2 bytes)
            NetworkOrderSerializer.WriteUInt16(span, _header.ProtocolClassId);
            // write header (X bytes)
            int bytesWritten = _header.WriteTo(span.Slice(2), (ulong)_bodyLength);
            return bytesWritten + 2;
        }
    }

    class BodySegmentOutboundFrame : OutboundFrame
    {
        private readonly ReadOnlyMemory<byte> _body;

        internal BodySegmentOutboundFrame(int channel, ReadOnlyMemory<byte> bodySegment) : base(FrameType.FrameBody, channel)
        {
            _body = bodySegment;
        }

        internal override int GetMinimumPayloadBufferSize()
        {
            return _body.Length;
        }

        internal override int WritePayload(Span<byte> span)
        {
            _body.Span.CopyTo(span);
            return _body.Length;
        }
    }

    class MethodOutboundFrame : OutboundFrame
    {
        private readonly MethodBase _method;

        internal MethodOutboundFrame(int channel, MethodBase method) : base(FrameType.FrameMethod, channel)
        {
            _method = method;
        }

        internal override int GetMinimumPayloadBufferSize()
        {
            // class id (2 bytes) + method id (2 bytes) + arguments (X bytes)
            return 4 + _method.GetRequiredBufferSize();
        }

        internal override int WritePayload(Span<byte> span)
        {
            NetworkOrderSerializer.WriteUInt16(span, _method.ProtocolClassId);
            NetworkOrderSerializer.WriteUInt16(span.Slice(2), _method.ProtocolMethodId);
            var argWriter = new MethodArgumentWriter(span.Slice(4));
            _method.WriteArgumentsTo(ref argWriter);
            return 4 + argWriter.Offset;
        }
    }

    class EmptyOutboundFrame : OutboundFrame
    {
        public EmptyOutboundFrame() : base(FrameType.FrameHeartbeat, 0)
        {
        }

        internal override int GetMinimumPayloadBufferSize()
        {
            return 0;
        }

        internal override int WritePayload(Span<byte> span)
        {
            return 0;
        }
    }

    internal abstract class OutboundFrame
    {
        public int Channel { get; }
        public FrameType Type { get; }

        protected OutboundFrame(FrameType type, int channel)
        {
            Type = type;
            Channel = channel;
        }

        internal void WriteTo(Span<byte> span)
        {
            span[0] = (byte)Type;
            NetworkOrderSerializer.WriteUInt16(span.Slice(1), (ushort)Channel);
            int bytesWritten = WritePayload(span.Slice(7));
            NetworkOrderSerializer.WriteUInt32(span.Slice(3), (uint)bytesWritten);
            span[bytesWritten + 7] = Constants.FrameEnd;
        }

        internal abstract int WritePayload(Span<byte> span);
        internal abstract int GetMinimumPayloadBufferSize();
        internal int GetMinimumBufferSize()
        {
            return 8 + GetMinimumPayloadBufferSize();
        }

        public override string ToString()
        {
            return $"(type={Type}, channel={Channel})";
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

        internal static InboundFrame ReadFrom(Stream reader)
        {
            int type = default;

            try
            {
                type = reader.ReadByte();
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
                if (ioe.InnerException == null ||
                    !(ioe.InnerException is SocketException) ||
                    ((SocketException)ioe.InnerException).SocketErrorCode != SocketError.TimedOut)
                {
                    throw;
                }

                ExceptionDispatchInfo.Capture(ioe.InnerException).Throw();
            }

            if (type == 'A')
            {
                // Probably an AMQP protocol header, otherwise meaningless
                ProcessProtocolHeader(reader);
            }

            Span<byte> headerBytes = stackalloc byte[6];
            reader.Read(headerBytes);
            int channel = NetworkOrderDeserializer.ReadUInt16(headerBytes);
            int payloadSize = NetworkOrderDeserializer.ReadInt32(headerBytes.Slice(2)); // FIXME - throw exn on unreasonable value
            byte[] payloadBytes = ArrayPool<byte>.Shared.Rent(payloadSize);
            Memory<byte> payload = new Memory<byte>(payloadBytes, 0, payloadSize);
            int bytesRead = 0;
            try
            {
                while (bytesRead < payloadSize)
                {
                    bytesRead += reader.Read(payload.Slice(bytesRead, payloadSize - bytesRead));
                }
            }
            catch (Exception)
            {
                // Early EOF.
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Short frame - expected to read {payloadSize} bytes, only got {bytesRead} bytes");
            }

            int frameEndMarker = reader.ReadByte();
            if (frameEndMarker != Constants.FrameEnd)
            {
                ArrayPool<byte>.Shared.Return(payloadBytes);
                throw new MalformedFrameException($"Bad frame end marker: {frameEndMarker}");
            }

            return new InboundFrame((FrameType)type, channel, payload);
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

    internal enum FrameType : int
    {
        FrameMethod = Constants.FrameMethod,
        FrameHeader = Constants.FrameHeader,
        FrameBody = Constants.FrameBody,
        FrameHeartbeat = Constants.FrameHeartbeat
    }
}

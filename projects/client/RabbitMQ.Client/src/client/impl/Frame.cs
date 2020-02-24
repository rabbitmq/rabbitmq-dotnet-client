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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class HeaderOutboundFrame : OutboundFrame
    {
        private readonly ContentHeaderBase header;
        private readonly int bodyLength;

        public HeaderOutboundFrame(int channel, ContentHeaderBase header, int bodyLength) : base(FrameType.FrameHeader, channel)
        {
            this.header = header;
            this.bodyLength = bodyLength;
        }

        public override void WritePayload(PipelineBinaryWriter writer)
        {
            using (var nw = new BinaryBufferWriter())
            {
                nw.Write(header.ProtocolClassId);
                header.WriteTo(nw, (ulong)bodyLength);

                var bufferSegment = nw.Buffer;
                writer.Write((uint)bufferSegment.Length);
                writer.Write(bufferSegment);
            }
        }
    }

    public class BodySegmentOutboundFrame : OutboundFrame
    {
        private readonly ReadOnlyMemory<byte> _body;

        public BodySegmentOutboundFrame(int channel, byte[] body, int offset, int count) : this(channel, new ReadOnlyMemory<byte>(body, offset, count))
        {
        }

        public BodySegmentOutboundFrame(int channel, ReadOnlyMemory<byte> body) : base(FrameType.FrameBody, channel)
        {
            _body = body;
        }

        public override void WritePayload(PipelineBinaryWriter writer)
        {
            writer.Write((uint)_body.Length);
            writer.Write(_body.Span);
        }
    }

    public class MethodOutboundFrame : OutboundFrame
    {
        private readonly MethodBase method;

        public MethodOutboundFrame(int channel, MethodBase method) : base(FrameType.FrameMethod, channel)
        {
            this.method = method;
        }

        public override void WritePayload(PipelineBinaryWriter writer)
        {
            using (var nw = new BinaryBufferWriter())
            {
                nw.Write(method.ProtocolClassId);
                nw.Write(method.ProtocolMethodId);

                var argWriter = new MethodArgumentWriter(nw);
                method.WriteArgumentsTo(ref argWriter);
                argWriter.Flush();

                var bufferSegment = nw.Buffer;
                writer.Write((uint)bufferSegment.Length);
                writer.Write(bufferSegment);
            }
        }
    }

    public class EmptyOutboundFrame : OutboundFrame
    {
        public EmptyOutboundFrame() : base(FrameType.FrameHeartbeat, 0)
        {
        }

        public override void WritePayload(PipelineBinaryWriter writer)
        {
            writer.Write((uint)0);
        }
    }

    public abstract class OutboundFrame : Frame
    {
        public OutboundFrame(FrameType type, int channel) : base(type, channel)
        {
        }

        public void WriteTo(PipelineBinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write((ushort)Channel);
            WritePayload(writer);
            writer.Write((byte)Constants.FrameEnd);
        }

        public abstract void WritePayload(PipelineBinaryWriter writer);
    }

    public class InboundFrame : Frame
    {
        private static ObjectPool<FrameContentReader> _readerPool = ObjectPool.Create<FrameContentReader>();
        private InboundFrame(FrameType type, int channel, byte[] payload, int payloadSize) : base(type, channel, payload, payloadSize)
        {
        }

        public static async ValueTask<InboundFrame> ReadFromAsync(PipelineBinaryReader reader, CancellationToken cancellationToken = default)
        {
            FrameContentReader contentReader = _readerPool.Get();
            try
            {
                await reader.ReadAsync(contentReader, cancellationToken).ConfigureAwait(false);
                reader.Advance();
                return new InboundFrame((FrameType)contentReader.Type, contentReader.Channel, contentReader.Payload, contentReader.PayloadSize);
            }
            finally
            {
                contentReader.Reset();
                _readerPool.Return(contentReader);
            }
        }

        public BinaryBufferReader GetReader()
        {
            return new BinaryBufferReader(new ReadOnlyMemory<byte>(Payload, 0, PayloadSize));
        }
    }

    public class Frame : IDisposable
    {
        public Frame(FrameType type, int channel)
        {
            Type = type;
            Channel = channel;
            Payload = null;
        }

        public Frame(FrameType type, int channel, byte[] payload, int payloadSize)
        {
            Type = type;
            if (!IsMethod() && !IsHeader() && !IsBody() && !IsHeartbeat())
            {
                throw new InvalidOperationException("Unknown frame type");
            }
            Channel = channel;
            Payload = payload;
            PayloadSize = payloadSize;
        }

        public int Channel { get; private set; }

        public byte[] Payload { get; private set; }
        public int PayloadSize { get; }
        public FrameType Type { get; private set; }

        public Span<byte> PayloadSpan => new Span<byte>(Payload, 0, PayloadSize);

        public override string ToString()
        {
            return string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                Type,
                Channel,
                Payload == null
                    ? "(null)"
                    : PayloadSize.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMethod()
        {
            return Type == FrameType.FrameMethod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHeader()
        {
            return Type == FrameType.FrameHeader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBody()
        {
            return Type == FrameType.FrameBody;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHeartbeat()
        {
            return Type == FrameType.FrameHeartbeat;
        }

        public void Dispose()
        {
            if (Payload != null && Payload.Length != 0)
            {
                ArrayPool<byte>.Shared.Return(Payload);
            }
        }
    }

    public enum FrameType : int
    {
        FrameMethod = 1,
        FrameHeader = 2,
        FrameBody = 3,
        FrameHeartbeat = 8,
        FrameEnd = 206,
        FrameMinSize = 4096
    }

}

// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;
using RabbitMQ.Util;
using System;
using System.IO;

#if NETFX_CORE
using Windows.Networking.Sockets;
#else

using System.Net.Sockets;

#endif

namespace RabbitMQ.Client.Impl
{
    public class HeaderWriteFrame : WriteFrame
    {
        public HeaderWriteFrame(int channel, ContentHeaderBase header, int bodyLength) : base(FrameType.FrameHeader, channel)
        {
            NetworkBinaryWriter writer = base.GetWriter();

            writer.Write((ushort)header.ProtocolClassId);
            header.WriteTo(writer, (ulong)bodyLength);
        }
    }

    public class BodySegmentWriteFrame : WriteFrame
    {
        public BodySegmentWriteFrame(int channel, byte[] body, int offset, int count) : base(FrameType.FrameBody, channel)
        {
            NetworkBinaryWriter writer = base.GetWriter();

            writer.Write(body, offset, count);
        }
    }

    public class MethodWriteFrame : WriteFrame
    {
        public MethodWriteFrame(int channel, MethodBase method) : base(FrameType.FrameMethod, channel)
        {
            NetworkBinaryWriter writer = base.GetWriter();

            writer.Write((ushort)method.ProtocolClassId);
            writer.Write((ushort)method.ProtocolMethodId);

            var argWriter = new MethodArgumentWriter(writer);

            method.WriteArgumentsTo(argWriter);

            argWriter.Flush();
        }
    }

    public class EmptyWriteFrame : WriteFrame
    {
        private static readonly byte[] m_emptyByteArray = new byte[0];

        public EmptyWriteFrame() : base(FrameType.FrameHeartbeat, 0)
        {
            base.GetWriter().Write(m_emptyByteArray);
        }

        public override string ToString()
        {
            return base.ToString() + string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                Type,
                Channel,
                Payload == null
                    ? "(null)"
                    : Payload.Length.ToString());
        }
    }

    public class WriteFrame : Frame
    {
        private readonly MemoryStream m_accumulator;
        private readonly NetworkBinaryWriter writer;

        public WriteFrame(FrameType type, int channel) : base(type, channel)
        {
            m_accumulator = new MemoryStream();
            writer = new NetworkBinaryWriter(m_accumulator);
        }

        public NetworkBinaryWriter GetWriter()
        {
            return writer;
        }

        public override string ToString()
        {
            return base.ToString() + string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                Type,
                Channel,
                Payload == null
                    ? "(null)"
                    : Payload.Length.ToString());
        }

        public void WriteTo(NetworkBinaryWriter writer)
        {
            var payload = m_accumulator.ToArray();

            writer.Write((byte)Type);
            writer.Write((ushort)Channel);
            writer.Write((uint)payload.Length);
            writer.Write(payload);
            writer.Write((byte)Constants.FrameEnd);
        }
    }

    public class ReadFrame : Frame
    {
        private ReadFrame(FrameType type, int channel, byte[] payload) : base(type, channel, payload)
        {
        }

        private static void ProcessProtocolHeader(NetworkBinaryReader reader)
        {
            try
            {
                byte b1 = reader.ReadByte();
                byte b2 = reader.ReadByte();
                byte b3 = reader.ReadByte();
                if (b1 != 'M' || b2 != 'Q' || b3 != 'P')
                {
                    throw new MalformedFrameException("Invalid AMQP protocol header from server");
                }

                int transportHigh = reader.ReadByte();
                int transportLow = reader.ReadByte();
                int serverMajor = reader.ReadByte();
                int serverMinor = reader.ReadByte();
                throw new PacketNotRecognizedException(transportHigh,
                    transportLow,
                    serverMajor,
                    serverMinor);
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

        public static ReadFrame ReadFrom(NetworkBinaryReader reader)
        {
            int type;

            try
            {
                type = reader.ReadByte();
            }
            catch (IOException ioe)
            {
#if NETFX_CORE
                if (ioe.InnerException != null
                    && SocketError.GetStatus(ioe.InnerException.HResult) == SocketErrorStatus.ConnectionTimedOut)
                {
                    throw ioe.InnerException;
                }

                throw;
#else
                // If it's a WSAETIMEDOUT SocketException, unwrap it.
                // This might happen when the limit of half-open connections is
                // reached.
                if (ioe.InnerException == null ||
                    !(ioe.InnerException is SocketException) ||
                    ((SocketException)ioe.InnerException).SocketErrorCode != SocketError.TimedOut)
                {
                    throw ioe;
                }
                throw ioe.InnerException;
#endif
            }

            if (type == 'A')
            {
                // Probably an AMQP protocol header, otherwise meaningless
                ProcessProtocolHeader(reader);
            }

            int channel = reader.ReadUInt16();
            int payloadSize = reader.ReadInt32(); // FIXME - throw exn on unreasonable value
            byte[] payload = reader.ReadBytes(payloadSize);
            if (payload.Length != payloadSize)
            {
                // Early EOF.
                throw new MalformedFrameException("Short frame - expected " +
                                                  payloadSize + " bytes, got " +
                                                  payload.Length + " bytes");
            }

            int frameEndMarker = reader.ReadByte();
            if (frameEndMarker != Constants.FrameEnd)
            {
                throw new MalformedFrameException("Bad frame end marker: " + frameEndMarker);
            }

            return new ReadFrame((FrameType)type, channel, payload);
        }

        public NetworkBinaryReader GetReader()
        {
            return new NetworkBinaryReader(new MemoryStream(base.Payload));
        }

        public override string ToString()
        {
            return base.ToString() + string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                base.Type,
                base.Channel,
                base.Payload == null
                    ? "(null)"
                    : base.Payload.Length.ToString());
        }
    }

    public class Frame
    {
        public Frame(FrameType type, int channel)
        {
            Type = type;
            Channel = channel;
            Payload = null;
        }

        public Frame(FrameType type, int channel, byte[] payload)
        {
            Type = type;
            Channel = channel;
            Payload = payload;
        }

        public int Channel { get; private set; }

        public byte[] Payload { get; private set; }

        public FrameType Type { get; private set; }

        public override string ToString()
        {
            return base.ToString() + string.Format("(type={0}, channel={1}, {2} bytes of payload)",
                Type,
                Channel,
                Payload == null
                    ? "(null)"
                    : Payload.Length.ToString());
        }

        public bool IsMethod()
        {
            return this.Type == FrameType.FrameMethod;
        }
        public bool IsHeader()
        {
            return this.Type == FrameType.FrameHeader;
        }
        public bool IsBody()
        {
            return this.Type == FrameType.FrameBody;
        }
        public bool IsHeartbeat()
        {
            return this.Type == FrameType.FrameHeartbeat;
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

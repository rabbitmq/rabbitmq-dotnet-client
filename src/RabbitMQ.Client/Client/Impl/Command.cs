// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Framing;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    public class Command
    {
        // EmptyFrameSize, 8 = 1 + 2 + 4 + 1
        // - 1 byte of frame type
        // - 2 bytes of channel number
        // - 4 bytes of frame payload length
        // - 1 byte of payload trailer FrameEnd byte
        public const int EmptyFrameSize = 8;

        public byte[] m_body0;
        public IList<byte[]> m_bodyN;
        private static readonly byte[] m_emptyByteArray = new byte[0];

        static Command()
        {
            CheckEmptyFrameSize();
        }

        public Command() : this(null, null, null)
        {
        }

        public Command(MethodBase method) : this(method, null, null)
        {
        }

        public Command(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            Method = method;
            Header = header;
            m_body0 = body;
            m_bodyN = null;
        }

        public byte[] Body
        {
            get { return ConsolidateBody(); }
        }

        public ContentHeaderBase Header { get; set; }

        public MethodBase Method { get; set; }

        public static void CheckEmptyFrameSize()
        {
            var f = new Frame(Constants.FrameBody, 0, m_emptyByteArray);
            var stream = new MemoryStream();
            var writer = new NetworkBinaryWriter(stream);
            f.WriteTo(writer);
            long actualLength = stream.Length;

            if (EmptyFrameSize != actualLength)
            {
                string message =
                    string.Format("EmptyFrameSize is incorrect - defined as {0} where the computed value is in fact {1}.",
                        EmptyFrameSize,
                        actualLength);
                throw new ProtocolViolationException(message);
            }
        }

        public void AppendBodyFragment(byte[] fragment)
        {
            if (m_body0 == null)
            {
                m_body0 = fragment;
            }
            else
            {
                if (m_bodyN == null)
                {
                    m_bodyN = new List<byte[]>();
                }
                m_bodyN.Add(fragment);
            }
        }

        public byte[] ConsolidateBody()
        {
            if (m_bodyN == null)
            {
                return m_body0 ?? m_emptyByteArray;
            }
            else
            {
                int totalSize = m_body0.Length;
                foreach (byte[] fragment in m_bodyN)
                {
                    totalSize += fragment.Length;
                }
                var result = new byte[totalSize];
                Array.Copy(m_body0, 0, result, 0, m_body0.Length);
                int offset = m_body0.Length;
                foreach (byte[] fragment in m_bodyN)
                {
                    Array.Copy(fragment, 0, result, offset, fragment.Length);
                    offset += fragment.Length;
                }
                m_body0 = result;
                m_bodyN = null;
                return m_body0;
            }
        }

        public void Transmit(int channelNumber, Connection connection)
        {
            if(Method.HasContent)
            {
                TransmitAsFrameSet(channelNumber, connection);
            }
            else
            {
                TransmitAsSingleFrame(channelNumber, connection);
            }
        }

        public void TransmitAsSingleFrame(int channelNumber, Connection connection)
        {
            var frame = new Frame(Constants.FrameMethod, channelNumber);
            NetworkBinaryWriter writer = frame.GetWriter();
            writer.Write((ushort)Method.ProtocolClassId);
            writer.Write((ushort)Method.ProtocolMethodId);
            var argWriter = new MethodArgumentWriter(writer);
            Method.WriteArgumentsTo(argWriter);
            argWriter.Flush();
            connection.WriteFrame(frame);
        }

        public void TransmitAsFrameSet(int channelNumber, Connection connection)
        {
            var frame = new Frame(Constants.FrameMethod, channelNumber);
            NetworkBinaryWriter writer = frame.GetWriter();
            writer.Write((ushort)Method.ProtocolClassId);
            writer.Write((ushort)Method.ProtocolMethodId);
            var argWriter = new MethodArgumentWriter(writer);
            Method.WriteArgumentsTo(argWriter);
            argWriter.Flush();

            var frames = new List<Frame>();
            frames.Add(frame);

            if (Method.HasContent)
            {
                byte[] body = Body;

                frame = new Frame(Constants.FrameHeader, channelNumber);
                writer = frame.GetWriter();
                writer.Write((ushort)Header.ProtocolClassId);
                Header.WriteTo(writer, (ulong)body.Length);
                frames.Add(frame);

                var frameMax = (int)Math.Min(int.MaxValue, connection.FrameMax);
                int bodyPayloadMax = (frameMax == 0)
                    ? body.Length
                    : frameMax - EmptyFrameSize;
                for (int offset = 0; offset < body.Length; offset += bodyPayloadMax)
                {
                    int remaining = body.Length - offset;

                    frame = new Frame(Constants.FrameBody, channelNumber);
                    writer = frame.GetWriter();
                    writer.Write(body, offset,
                        (remaining < bodyPayloadMax) ? remaining : bodyPayloadMax);
                    frames.Add(frame);
                }
            }

            connection.WriteFrameSet(frames);
        }
    }
}

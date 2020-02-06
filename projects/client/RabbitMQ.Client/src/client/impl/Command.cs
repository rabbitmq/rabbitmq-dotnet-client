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
using System.Collections.Generic;
using System.IO.Pipelines;

using RabbitMQ.Client.Framing.Impl;
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
        private const int EmptyFrameSize = 8;

        static Command()
        {
            CheckEmptyFrameSize();
        }

        public Command(MethodBase method) : this(method, null, null)
        {
        }

        public Command(MethodBase method, ContentHeaderBase header, byte[] body)
        {
            Method = method;
            Header = header;
            Body = body ?? Array.Empty<byte>();
        }

        public byte[] Body { get; private set; }

        public ContentHeaderBase Header { get; private set; }

        public MethodBase Method { get; private set; }

        public static void CheckEmptyFrameSize()
        {
            var f = new EmptyOutboundFrame();
            using (var stream = PooledMemoryStream.GetMemoryStream())
            {
                var pipeWriter = PipeWriter.Create(stream);
                var writer = new PipelineBinaryWriter(pipeWriter);
                f.WriteTo(writer);
                writer.Flush();

                long actualLength = stream.Position;

                if (EmptyFrameSize != actualLength)
                {
                    string message =
                        string.Format("EmptyFrameSize is incorrect - defined as {0} where the computed value is in fact {1}.",
                            EmptyFrameSize,
                            actualLength);
                    throw new ProtocolViolationException(message);
                }
            }
        }

        public void Transmit(int channelNumber, Connection connection)
        {
            if (Method.HasContent)
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
            connection.WriteFrame(new MethodOutboundFrame(channelNumber, Method));
        }

        public void TransmitAsFrameSet(int channelNumber, Connection connection)
        {
            var frames = new List<OutboundFrame>();
            frames.Add(new MethodOutboundFrame(channelNumber, Method));
            if (Method.HasContent)
            {
                var body = Body;

                frames.Add(new HeaderOutboundFrame(channelNumber, Header, body.Length));
                var frameMax = (int)Math.Min(int.MaxValue, connection.FrameMax);
                var bodyPayloadMax = (frameMax == 0) ? body.Length : frameMax - EmptyFrameSize;
                for (int offset = 0; offset < body.Length; offset += bodyPayloadMax)
                {
                    var remaining = body.Length - offset;
                    var count = (remaining < bodyPayloadMax) ? remaining : bodyPayloadMax;
                    frames.Add(new BodySegmentOutboundFrame(channelNumber, body, offset, count));
                }
            }

            connection.WriteFrameSet(frames);
        }


        public static List<OutboundFrame> CalculateFrames(int channelNumber, Connection connection, IList<Command> commands)
        {
            var frames = new List<OutboundFrame>();

            foreach (var cmd in commands)
            {
                frames.Add(new MethodOutboundFrame(channelNumber, cmd.Method));
                if (cmd.Method.HasContent)
                {
                    var body = cmd.Body;

                    frames.Add(new HeaderOutboundFrame(channelNumber, cmd.Header, body.Length));
                    var frameMax = (int)Math.Min(int.MaxValue, connection.FrameMax);
                    var bodyPayloadMax = (frameMax == 0) ? body.Length : frameMax - EmptyFrameSize;
                    for (int offset = 0; offset < body.Length; offset += bodyPayloadMax)
                    {
                        var remaining = body.Length - offset;
                        var count = (remaining < bodyPayloadMax) ? remaining : bodyPayloadMax;
                        frames.Add(new BodySegmentOutboundFrame(channelNumber, body, offset, count));
                    }
                }
            }

            return frames;
        }
    }
}

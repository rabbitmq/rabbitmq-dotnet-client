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
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Impl
{
    enum AssemblyState
    {
        ExpectingMethod,
        ExpectingContentHeader,
        ExpectingContentBody,
        Complete
    }

    class CommandAssembler
    {
        private const int MaxArrayOfBytesSize = 2_147_483_591;

        public MethodBase m_method;
        public ContentHeaderBase m_header;
        public Memory<byte> m_body;
        public ProtocolBase m_protocol;
        public int m_remainingBodyBytes;
        private int _offset;
        public AssemblyState m_state;

        public CommandAssembler(ProtocolBase protocol)
        {
            m_protocol = protocol;
            Reset();
        }

        public Command HandleFrame(in InboundFrame f)
        {
            switch (m_state)
            {
                case AssemblyState.ExpectingMethod:
                    if (!f.IsMethod())
                    {
                        throw new UnexpectedFrameException(f.Type);
                    }
                    m_method = m_protocol.DecodeMethodFrom(f.Payload);
                    m_state = m_method.HasContent ? AssemblyState.ExpectingContentHeader : AssemblyState.Complete;
                    return CompletedCommand();
                case AssemblyState.ExpectingContentHeader:
                    if (!f.IsHeader())
                    {
                        throw new UnexpectedFrameException(f.Type);
                    }
                    m_header = m_protocol.DecodeContentHeaderFrom(NetworkOrderDeserializer.ReadUInt16(f.Payload.Span));
                    ulong totalBodyBytes = m_header.ReadFrom(f.Payload.Slice(2));
                    if (totalBodyBytes > MaxArrayOfBytesSize)
                    {
                        throw new UnexpectedFrameException(f.Type);
                    }

                    m_remainingBodyBytes = (int)totalBodyBytes;
                    byte[] bodyBytes = ArrayPool<byte>.Shared.Rent(m_remainingBodyBytes);
                    m_body = new Memory<byte>(bodyBytes, 0, m_remainingBodyBytes);
                    UpdateContentBodyState();
                    return CompletedCommand();
                case AssemblyState.ExpectingContentBody:
                    if (!f.IsBody())
                    {
                        throw new UnexpectedFrameException(f.Type);
                    }

                    if (f.Payload.Length > m_remainingBodyBytes)
                    {
                        throw new MalformedFrameException($"Overlong content body received - {m_remainingBodyBytes} bytes remaining, {f.Payload.Length} bytes received");
                    }

                    f.Payload.CopyTo(m_body.Slice(_offset));
                    m_remainingBodyBytes -= f.Payload.Length;
                    _offset += f.Payload.Length;
                    UpdateContentBodyState();
                    return CompletedCommand();
                case AssemblyState.Complete:
                default:
                    return null;
            }
        }

        private Command CompletedCommand()
        {
            if (m_state == AssemblyState.Complete)
            {
                Command result = new Command(m_method, m_header, m_body, true);
                Reset();
                return result;
            }
            else
            {
                return null;
            }
        }

        private void Reset()
        {
            m_state = AssemblyState.ExpectingMethod;
            m_method = null;
            m_header = null;
            m_body = null;
            _offset = 0;
            m_remainingBodyBytes = 0;
        }

        private void UpdateContentBodyState()
        {
            m_state = (m_remainingBodyBytes > 0)
                ? AssemblyState.ExpectingContentBody
                : AssemblyState.Complete;
        }
    }
}

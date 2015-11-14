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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used during channel quiescing.</summary>
    public class QuiescingSession : SessionBase
    {
        public ShutdownEventArgs m_reason;

        public QuiescingSession(Connection connection,
            int channelNumber,
            ShutdownEventArgs reason)
            : base(connection, channelNumber)
        {
            m_reason = reason;
        }

        public override void HandleFrame(Frame frame)
        {
            if (frame.Type == Constants.FrameMethod)
            {
                MethodBase method = Connection.Protocol.DecodeMethodFrom(frame.GetReader());
                if ((method.ProtocolClassId == ChannelCloseOk.ClassId)
                    && (method.ProtocolMethodId == ChannelCloseOk.MethodId))
                {
                    // This is the reply we were looking for. Release
                    // the channel with the reason we were passed in
                    // our constructor.
                    Close(m_reason);
                }
                else if ((method.ProtocolClassId == ChannelClose.ClassId)
                         && (method.ProtocolMethodId == ChannelClose.MethodId))
                {
                    // We're already shutting down the channel, so
                    // just send back an ok.
                    Transmit(CreateChannelCloseOk());
                }
            }

            // Either a non-method frame, or not what we were looking
            // for. Ignore it - we're quiescing.
        }

        protected Command CreateChannelCloseOk()
        {
            return new Command(new ConnectionCloseOk());
        }
    }
}

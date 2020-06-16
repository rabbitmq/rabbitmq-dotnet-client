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

using System.Threading.Tasks;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    ///<summary>Small ISession implementation used during channel quiescing.</summary>
    class QuiescingSession : SessionBase
    {
        public ShutdownEventArgs m_reason;

        public QuiescingSession(Connection connection,
            int channelNumber,
            ShutdownEventArgs reason)
            : base(connection, channelNumber)
        {
            m_reason = reason;
        }

        public override ValueTask HandleFrame(in InboundFrame frame)
        {
            if (frame.IsMethod())
            {
                MethodBase method = Connection.Protocol.DecodeMethodFrom(frame.Payload);
                if ((method.ProtocolClassId == ClassConstants.Channel)
                    && (method.ProtocolMethodId == ChannelMethodConstants.CloseOk))
                {
                    // This is the reply we were looking for. Release
                    // the channel with the reason we were passed in
                    // our constructor.
                    return Close(m_reason);
                }
                else if ((method.ProtocolClassId == ClassConstants.Channel)
                         && (method.ProtocolMethodId == ChannelMethodConstants.Close))
                {
                    // We're already shutting down the channel, so
                    // just send back an ok.
                    Transmit(CreateChannelCloseOk());
                }
            }

            return default;

            // Either a non-method frame, or not what we were looking
            // for. Ignore it - we're quiescing.
        }

        protected Command CreateChannelCloseOk()
        {
            return new Command(new ConnectionCloseOk());
        }
    }
}

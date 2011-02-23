// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2011 VMware, Inc.
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
//  The Initial Developer of the Original Code is VMware, Inc.
//  Copyright (c) 2007-2011 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

using System.Collections;

namespace RabbitMQ.Client.Framing.Impl.v0_9_1 {
    public abstract class ProtocolBase: AbstractProtocolBase {

        public ProtocolBase() {
            Capabilities["publisher_confirms"] = true;
            Capabilities["exchange_exchange_bindings"] = true;
            Capabilities["basic.nack"] = true;
            Capabilities["consumer_cancel_notify"] = true;
        }

        public override IFrameHandler CreateFrameHandler(AmqpTcpEndpoint endpoint) {
            return new SocketFrameHandler_0_9(endpoint);
        }

        public override IModel CreateModel(ISession session) {
            return new Model(session);
        }

        public override IConnection CreateConnection(ConnectionFactory factory,
                                                     bool insist,
                                                     IFrameHandler frameHandler)
        {
            return new Connection(factory, insist, frameHandler);
        }

        public override void CreateConnectionClose(ushort reasonCode,
                                                   string reasonText,
                                                   out Command request,
                                                   out int replyClassId,
                                                   out int replyMethodId)
        {
            request = new Command(new RabbitMQ.Client.Framing.Impl.v0_9_1.ConnectionClose(reasonCode,
                                                                                          reasonText,
                                                                                          0, 0));
            replyClassId = RabbitMQ.Client.Framing.Impl.v0_9_1.ConnectionCloseOk.ClassId;
            replyMethodId = RabbitMQ.Client.Framing.Impl.v0_9_1.ConnectionCloseOk.MethodId;
        }

        public override void CreateChannelClose(ushort reasonCode,
                                                string reasonText,
                                                out Command request,
                                                out int replyClassId,
                                                out int replyMethodId)
        {
            request = new Command(new RabbitMQ.Client.Framing.Impl.v0_9_1.ChannelClose(reasonCode,
                                                                                       reasonText,
                                                                                       0, 0));
            replyClassId = RabbitMQ.Client.Framing.Impl.v0_9_1.ChannelCloseOk.ClassId;
            replyMethodId = RabbitMQ.Client.Framing.Impl.v0_9_1.ChannelCloseOk.MethodId;
        }

        public override bool CanSendWhileClosed(Command cmd)
        {
            return cmd.m_method is RabbitMQ.Client.Framing.Impl.v0_9_1.ChannelCloseOk;
        }
    }
}

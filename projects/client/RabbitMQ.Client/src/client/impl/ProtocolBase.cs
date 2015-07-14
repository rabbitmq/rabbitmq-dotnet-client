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

#if !NETFX_CORE
using System.Net.Sockets;
#else
using Windows.Networking.Sockets;
#endif

using RabbitMQ.Client.Impl;
using RabbitMQ.Client;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Framing.Impl
{
    public abstract class ProtocolBase : IProtocol
    {
        public IDictionary<string, bool> Capabilities = new Dictionary<string, bool>();

        public ProtocolBase()
        {
            Capabilities["publisher_confirms"] = true;
            Capabilities["exchange_exchange_bindings"] = true;
            Capabilities["basic.nack"] = true;
            Capabilities["consumer_cancel_notify"] = true;
            Capabilities["connection.blocked"] = true;
            Capabilities["authentication_failure_close"] = true;
        }

        public abstract string ApiName { get; }
        public abstract int DefaultPort { get; }

        public abstract int MajorVersion { get; }
        public abstract int MinorVersion { get; }
        public abstract int Revision { get; }

        public AmqpVersion Version
        {
            get { return new AmqpVersion(MajorVersion, MinorVersion); }
        }

        public bool CanSendWhileClosed(Command cmd)
        {
            return cmd.Method is Impl.ChannelCloseOk;
        }

        public void CreateChannelClose(ushort reasonCode,
            string reasonText,
            out Command request,
            out int replyClassId,
            out int replyMethodId)
        {
            request = new Command(new Impl.ChannelClose(reasonCode,
                reasonText,
                0, 0));
            replyClassId = Impl.ChannelCloseOk.ClassId;
            replyMethodId = Impl.ChannelCloseOk.MethodId;
        }

        public void CreateConnectionClose(ushort reasonCode,
            string reasonText,
            out Command request,
            out int replyClassId,
            out int replyMethodId)
        {
            request = new Command(new Impl.ConnectionClose(reasonCode,
                reasonText,
                0, 0));
            replyClassId = Impl.ConnectionCloseOk.ClassId;
            replyMethodId = Impl.ConnectionCloseOk.MethodId;
        }

        public abstract ContentHeaderBase DecodeContentHeaderFrom(NetworkBinaryReader reader);
        public abstract MethodBase DecodeMethodFrom(NetworkBinaryReader reader);

        public override bool Equals(object obj)
        {
            return (GetType() == obj.GetType());
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        public override string ToString()
        {
            return Version.ToString();
        }

        public IConnection CreateConnection(IConnectionFactory factory,
            bool insist,
            IFrameHandler frameHandler)
        {
            return new Connection(factory, insist, frameHandler);
        }

        public IConnection CreateConnection(ConnectionFactory factory,
            IFrameHandler frameHandler,
            bool automaticRecoveryEnabled)
        {
            var ac = new AutorecoveringConnection(factory);
            ac.Init();
            return ac;
        }

        public IFrameHandler CreateFrameHandler(
            AmqpTcpEndpoint endpoint,
#if !NETFX_CORE
            Func<AddressFamily, ITcpClient> socketFactory, 
#else
            Func<StreamSocket> socketFactory,
#endif
            int timeout)
        {
            return new SocketFrameHandler(endpoint, socketFactory, timeout);
        }

        public IModel CreateModel(ISession session)
        {
            return new Model(session);
        }

        public IModel CreateModel(ISession session, ConsumerWorkService workService)
        {
            return new Model(session, workService);
        }
    }
}

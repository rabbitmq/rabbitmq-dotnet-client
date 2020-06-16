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

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    abstract class ProtocolBase : IProtocol
    {
        public IDictionary<string, bool> Capabilities;

        public ProtocolBase()
        {
            Capabilities = new Dictionary<string, bool>
            {
                ["publisher_confirms"] = true,
                ["exchange_exchange_bindings"] = true,
                ["basic.nack"] = true,
                ["consumer_cancel_notify"] = true,
                ["connection.blocked"] = true,
                ["authentication_failure_close"] = true
            };
        }

        public abstract string ApiName { get; }
        public abstract int DefaultPort { get; }

        public abstract int MajorVersion { get; }
        public abstract int MinorVersion { get; }
        public abstract int Revision { get; }

        public AmqpVersion Version => new AmqpVersion(MajorVersion, MinorVersion);

        public bool CanSendWhileClosed(Command cmd)
        {
            switch (cmd.Method)
            {
                case Impl.ChannelClose _:
                case Impl.ChannelCloseOk _:
                    return true;
                default:
                    return false;
            }
        }

        public void CreateChannelClose(ushort reasonCode,
            string reasonText,
            out Command request,
            out ushort replyClassId,
            out ushort replyMethodId)
        {
            request = new Command(new Impl.ChannelClose(reasonCode,
                reasonText,
                0, 0));
            replyClassId = ClassConstants.Channel;
            replyMethodId = ChannelMethodConstants.CloseOk;
        }

        public void CreateConnectionClose(ushort reasonCode,
            string reasonText,
            out Command request,
            out ushort replyClassId,
            out ushort replyMethodId)
        {
            request = new Command(new Impl.ConnectionClose(reasonCode,
                reasonText,
                0, 0));
            replyClassId = ClassConstants.Connection;
            replyMethodId = ConnectionMethodConstants.CloseOk;
        }

        internal abstract ContentHeaderBase DecodeContentHeaderFrom(ushort classId);
        internal abstract MethodBase DecodeMethodFrom(ReadOnlyMemory<byte> reader);

        public override bool Equals(object obj)
        {
            return GetType() == obj.GetType();
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        public override string ToString()
        {
            return Version.ToString();
        }

        public IConnection CreateConnection(IConnectionFactory factory, IFrameHandler frameHandler)
        {
            return new Connection(factory, frameHandler, null);
        }


        public IConnection CreateConnection(IConnectionFactory factory, IFrameHandler frameHandler, string clientProvidedName)
        {
            return new Connection(factory, frameHandler, clientProvidedName);
        }

        public IConnection CreateConnection(ConnectionFactory factory, IFrameHandler frameHandler, bool automaticRecoveryEnabled)
        {
            var ac = new AutorecoveringConnection(factory, null);
            ac.Init();
            return ac;
        }

        public IConnection CreateConnection(ConnectionFactory factory, IFrameHandler frameHandler, bool automaticRecoveryEnabled, string clientProvidedName)
        {
            var ac = new AutorecoveringConnection(factory, clientProvidedName);
            ac.Init();
            return ac;
        }


        public IModel CreateModel(ISession session)
        {
            return new Model(session);
        }
    }
}

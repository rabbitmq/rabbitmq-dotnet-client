// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
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
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal abstract class ProtocolBase : IProtocol
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

        public AmqpVersion Version
        {
            get { return new AmqpVersion(MajorVersion, MinorVersion); }
        }

        public bool CanSendWhileClosed(MethodBase method)
        {
            return method is Impl.ChannelCloseOk;
        }

        public void CreateChannelClose(ushort reasonCode,
            string reasonText,
            out OutgoingCommand request)
        {
            request = new OutgoingCommand(new Impl.ChannelClose(reasonCode, reasonText, 0, 0));
        }

        public void CreateConnectionClose(ushort reasonCode, string reasonText, out OutgoingCommand request, out ProtocolCommandId replyProtocolCommandId)
        {
            request = new OutgoingCommand(new Impl.ConnectionClose(reasonCode, reasonText, 0, 0));
            replyProtocolCommandId = ProtocolCommandId.ConnectionCloseOk;
        }

        internal abstract ContentHeaderBase DecodeContentHeaderFrom(ushort classId, ReadOnlyMemory<byte> rentedMemory);
        internal abstract MethodBase DecodeMethodFrom(ReadOnlySpan<byte> reader);

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

        public IConnection CreateConnection(IConnectionFactory factory,
            bool insist,
            IFrameHandler frameHandler)
        {
            return new Connection(factory, insist, frameHandler, null);
        }


        public IConnection CreateConnection(IConnectionFactory factory,
            bool insist,
            IFrameHandler frameHandler,
            string clientProvidedName)
        {
            return new Connection(factory, insist, frameHandler, clientProvidedName);
        }

        public IConnection CreateConnection(ConnectionFactory factory,
            IFrameHandler frameHandler,
            bool automaticRecoveryEnabled)
        {
            var ac = new AutorecoveringConnection(factory, null);
            ac.Init();
            return ac;
        }

        public IConnection CreateConnection(ConnectionFactory factory,
            IFrameHandler frameHandler,
            bool automaticRecoveryEnabled,
            string clientProvidedName)
        {
            var ac = new AutorecoveringConnection(factory, clientProvidedName);
            ac.Init();
            return ac;
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

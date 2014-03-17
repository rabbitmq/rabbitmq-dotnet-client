// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

using System.Collections.Generic;

namespace RabbitMQ.Client.Impl {
    public abstract class AbstractProtocolBase: IProtocol {
        public abstract int MajorVersion { get; }
        public abstract int MinorVersion { get; }
        public abstract int Revision { get; }
        public abstract string ApiName { get; }
        public abstract int DefaultPort { get; }

        public IDictionary<string, bool> Capabilities = new Dictionary<string, bool>();

        public abstract IFrameHandler CreateFrameHandler(AmqpTcpEndpoint endpoint,
                                                         ConnectionFactory.ObtainSocket socketFactory,
                                                         int timeout);
        public abstract IConnection CreateConnection(ConnectionFactory factory,
                                                     bool insist,
                                                     IFrameHandler frameHandler);
        public abstract IModel CreateModel(ISession session);

        public abstract MethodBase DecodeMethodFrom(NetworkBinaryReader reader);
        public abstract ContentHeaderBase DecodeContentHeaderFrom(NetworkBinaryReader reader);

        ///<summary>Used when a connection is being quiesced because
        ///of a HardProtocolException or Application initiated shutdown</summary>
        public abstract void CreateConnectionClose(ushort reasonCode,
                                                   string reasonText,
                                                   out Command request,
                                                   out int replyClassId,
                                                   out int replyMethodId);

        ///<summary>Used when a channel is being quiesced because of a
        ///SoftProtocolException.</summary>
        public abstract void CreateChannelClose(ushort reasonCode,
                                                   string reasonText,
                                                   out Command request,
                                                   out int replyClassId,
                                                   out int replyMethodId);

        ///<summary>Used in the quiescing session to determine if the command
        ///is allowed to be sent.</summary>
        public abstract bool CanSendWhileClosed(Command cmd);

        public AmqpVersion Version {
            get {
                return new AmqpVersion(MajorVersion, MinorVersion);
            }
        }

        public override string ToString() {
            return Version.ToString();
        }

        public override bool Equals(object obj) {
            return (GetType() == obj.GetType());
        }

        public override int GetHashCode() {
            return GetType().GetHashCode();
        }
    }
}

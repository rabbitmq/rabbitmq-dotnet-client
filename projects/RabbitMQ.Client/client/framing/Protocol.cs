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
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Framing
{
    internal sealed class Protocol : ProtocolBase
    {
        ///<summary>Protocol major version (= 0)</summary>
        public override int MajorVersion => 0;

        ///<summary>Protocol minor version (= 9)</summary>
        public override int MinorVersion => 9;

        ///<summary>Protocol revision (= 1)</summary>
        public override int Revision => 1;

        ///<summary>Protocol API name (= :AMQP_0_9_1)</summary>
        public override string ApiName => ":AMQP_0_9_1";

        ///<summary>Default TCP port (= 5672)</summary>
        public override int DefaultPort => 5672;

        internal override Client.Impl.MethodBase DecodeMethodFrom(ReadOnlySpan<byte> span)
        {
            var commandId = (ProtocolCommandId)Util.NetworkOrderDeserializer.ReadUInt32(span);
            Client.Impl.MethodBase result = DecodeMethodFrom(commandId);
            Client.Impl.MethodArgumentReader reader = new Client.Impl.MethodArgumentReader(span.Slice(4));
            result.ReadArgumentsFrom(ref reader);
            return result;
        }

        internal Client.Impl.MethodBase DecodeMethodFrom(ProtocolCommandId commandId)
        {
            switch (commandId)
            {
                case ProtocolCommandId.ConnectionStart: return new ConnectionStart();
                case ProtocolCommandId.ConnectionStartOk: return new ConnectionStartOk();
                case ProtocolCommandId.ConnectionSecure: return new ConnectionSecure();
                case ProtocolCommandId.ConnectionSecureOk: return new ConnectionSecureOk();
                case ProtocolCommandId.ConnectionTune: return new ConnectionTune();
                case ProtocolCommandId.ConnectionTuneOk: return new ConnectionTuneOk();
                case ProtocolCommandId.ConnectionOpen: return new ConnectionOpen();
                case ProtocolCommandId.ConnectionOpenOk: return new ConnectionOpenOk();
                case ProtocolCommandId.ConnectionClose: return new ConnectionClose();
                case ProtocolCommandId.ConnectionCloseOk: return new ConnectionCloseOk();
                case ProtocolCommandId.ConnectionBlocked: return new ConnectionBlocked();
                case ProtocolCommandId.ConnectionUnblocked: return new ConnectionUnblocked();
                case ProtocolCommandId.ConnectionUpdateSecret: return new ConnectionUpdateSecret();
                case ProtocolCommandId.ConnectionUpdateSecretOk: return new ConnectionUpdateSecretOk();
                case ProtocolCommandId.ChannelOpen: return new ChannelOpen();
                case ProtocolCommandId.ChannelOpenOk: return new ChannelOpenOk();
                case ProtocolCommandId.ChannelFlow: return new ChannelFlow();
                case ProtocolCommandId.ChannelFlowOk: return new ChannelFlowOk();
                case ProtocolCommandId.ChannelClose: return new ChannelClose();
                case ProtocolCommandId.ChannelCloseOk: return new ChannelCloseOk();
                case ProtocolCommandId.ExchangeDeclare: return new ExchangeDeclare();
                case ProtocolCommandId.ExchangeDeclareOk: return new ExchangeDeclareOk();
                case ProtocolCommandId.ExchangeDelete: return new ExchangeDelete();
                case ProtocolCommandId.ExchangeDeleteOk: return new ExchangeDeleteOk();
                case ProtocolCommandId.ExchangeBind: return new ExchangeBind();
                case ProtocolCommandId.ExchangeBindOk: return new ExchangeBindOk();
                case ProtocolCommandId.ExchangeUnbind: return new ExchangeUnbind();
                case ProtocolCommandId.ExchangeUnbindOk: return new ExchangeUnbindOk();
                case ProtocolCommandId.QueueDeclare: return new QueueDeclare();
                case ProtocolCommandId.QueueDeclareOk: return new Impl.QueueDeclareOk();
                case ProtocolCommandId.QueueBind: return new QueueBind();
                case ProtocolCommandId.QueueBindOk: return new QueueBindOk();
                case ProtocolCommandId.QueueUnbind: return new QueueUnbind();
                case ProtocolCommandId.QueueUnbindOk: return new QueueUnbindOk();
                case ProtocolCommandId.QueuePurge: return new QueuePurge();
                case ProtocolCommandId.QueuePurgeOk: return new QueuePurgeOk();
                case ProtocolCommandId.QueueDelete: return new QueueDelete();
                case ProtocolCommandId.QueueDeleteOk: return new QueueDeleteOk();
                case ProtocolCommandId.BasicQos: return new BasicQos();
                case ProtocolCommandId.BasicQosOk: return new BasicQosOk();
                case ProtocolCommandId.BasicConsume: return new BasicConsume();
                case ProtocolCommandId.BasicConsumeOk: return new BasicConsumeOk();
                case ProtocolCommandId.BasicCancel: return new BasicCancel();
                case ProtocolCommandId.BasicCancelOk: return new BasicCancelOk();
                case ProtocolCommandId.BasicPublish: return new BasicPublish();
                case ProtocolCommandId.BasicReturn: return new BasicReturn();
                case ProtocolCommandId.BasicDeliver: return new BasicDeliver();
                case ProtocolCommandId.BasicGet: return new BasicGet();
                case ProtocolCommandId.BasicGetOk: return new BasicGetOk();
                case ProtocolCommandId.BasicGetEmpty: return new BasicGetEmpty();
                case ProtocolCommandId.BasicAck: return new BasicAck();
                case ProtocolCommandId.BasicReject: return new BasicReject();
                case ProtocolCommandId.BasicRecoverAsync: return new BasicRecoverAsync();
                case ProtocolCommandId.BasicRecover: return new BasicRecover();
                case ProtocolCommandId.BasicRecoverOk: return new BasicRecoverOk();
                case ProtocolCommandId.BasicNack: return new BasicNack();
                case ProtocolCommandId.TxSelect: return new TxSelect();
                case ProtocolCommandId.TxSelectOk: return new TxSelectOk();
                case ProtocolCommandId.TxCommit: return new TxCommit();
                case ProtocolCommandId.TxCommitOk: return new TxCommitOk();
                case ProtocolCommandId.TxRollback: return new TxRollback();
                case ProtocolCommandId.TxRollbackOk: return new TxRollbackOk();
                case ProtocolCommandId.ConfirmSelect: return new ConfirmSelect();
                case ProtocolCommandId.ConfirmSelectOk: return new ConfirmSelectOk();
                default:
                    //TODO Check if valid
                    throw new Exceptions.UnknownClassOrMethodException((ushort)((uint)commandId >> 16), (ushort)((uint)commandId & 0xFFFF));
            }
        }


        internal override Client.Impl.ContentHeaderBase DecodeContentHeaderFrom(ushort classId)
        {
            switch (classId)
            {
                case 60: return new BasicProperties();
                default: throw new Exceptions.UnknownClassOrMethodException(classId, 0);
            }
        }
    }
}

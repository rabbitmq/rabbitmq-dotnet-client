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
using System.Runtime.CompilerServices;

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
            ProtocolCommandId commandId = ReadCommandId(span);
            return DecodeMethodFrom(commandId, span.Slice(4));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProtocolCommandId ReadCommandId(ReadOnlySpan<byte> span) => (ProtocolCommandId)Util.NetworkOrderDeserializer.ReadUInt32(span);

        private static Client.Impl.MethodBase DecodeMethodFrom(ProtocolCommandId commandId, ReadOnlySpan<byte> span)
        {
            switch (commandId)
            {
                case ProtocolCommandId.ConnectionStart: return new ConnectionStart(span);
                case ProtocolCommandId.ConnectionStartOk: return new ConnectionStartOk(span);
                case ProtocolCommandId.ConnectionSecure: return new ConnectionSecure(span);
                case ProtocolCommandId.ConnectionSecureOk: return new ConnectionSecureOk(span);
                case ProtocolCommandId.ConnectionTune: return new ConnectionTune(span);
                case ProtocolCommandId.ConnectionTuneOk: return new ConnectionTuneOk(span);
                case ProtocolCommandId.ConnectionOpen: return new ConnectionOpen(span);
                case ProtocolCommandId.ConnectionOpenOk: return new ConnectionOpenOk();
                case ProtocolCommandId.ConnectionClose: return new ConnectionClose(span);
                case ProtocolCommandId.ConnectionCloseOk: return new ConnectionCloseOk();
                case ProtocolCommandId.ConnectionBlocked: return new ConnectionBlocked(span);
                case ProtocolCommandId.ConnectionUnblocked: return new ConnectionUnblocked();
                case ProtocolCommandId.ConnectionUpdateSecret: return new ConnectionUpdateSecret(span);
                case ProtocolCommandId.ConnectionUpdateSecretOk: return new ConnectionUpdateSecretOk();
                case ProtocolCommandId.ChannelOpen: return new ChannelOpen();
                case ProtocolCommandId.ChannelOpenOk: return new ChannelOpenOk();
                case ProtocolCommandId.ChannelFlow: return new ChannelFlow(span);
                case ProtocolCommandId.ChannelFlowOk: return new ChannelFlowOk(span);
                case ProtocolCommandId.ChannelClose: return new ChannelClose(span);
                case ProtocolCommandId.ChannelCloseOk: return new ChannelCloseOk();
                case ProtocolCommandId.ExchangeDeclare: return new ExchangeDeclare(span);
                case ProtocolCommandId.ExchangeDeclareOk: return new ExchangeDeclareOk();
                case ProtocolCommandId.ExchangeDelete: return new ExchangeDelete(span);
                case ProtocolCommandId.ExchangeDeleteOk: return new ExchangeDeleteOk();
                case ProtocolCommandId.ExchangeBind: return new ExchangeBind(span);
                case ProtocolCommandId.ExchangeBindOk: return new ExchangeBindOk();
                case ProtocolCommandId.ExchangeUnbind: return new ExchangeUnbind(span);
                case ProtocolCommandId.ExchangeUnbindOk: return new ExchangeUnbindOk();
                case ProtocolCommandId.QueueDeclare: return new QueueDeclare(span);
                case ProtocolCommandId.QueueDeclareOk: return new Impl.QueueDeclareOk(span);
                case ProtocolCommandId.QueueBind: return new QueueBind(span);
                case ProtocolCommandId.QueueBindOk: return new QueueBindOk();
                case ProtocolCommandId.QueueUnbind: return new QueueUnbind(span);
                case ProtocolCommandId.QueueUnbindOk: return new QueueUnbindOk();
                case ProtocolCommandId.QueuePurge: return new QueuePurge(span);
                case ProtocolCommandId.QueuePurgeOk: return new QueuePurgeOk(span);
                case ProtocolCommandId.QueueDelete: return new QueueDelete(span);
                case ProtocolCommandId.QueueDeleteOk: return new QueueDeleteOk(span);
                case ProtocolCommandId.BasicQos: return new BasicQos(span);
                case ProtocolCommandId.BasicQosOk: return new BasicQosOk();
                case ProtocolCommandId.BasicConsume: return new BasicConsume(span);
                case ProtocolCommandId.BasicConsumeOk: return new BasicConsumeOk(span);
                case ProtocolCommandId.BasicCancel: return new BasicCancel(span);
                case ProtocolCommandId.BasicCancelOk: return new BasicCancelOk(span);
                case ProtocolCommandId.BasicPublish: return new BasicPublish(span);
                case ProtocolCommandId.BasicReturn: return new BasicReturn(span);
                case ProtocolCommandId.BasicDeliver: return new BasicDeliver(span);
                case ProtocolCommandId.BasicGet: return new BasicGet(span);
                case ProtocolCommandId.BasicGetOk: return new BasicGetOk(span);
                case ProtocolCommandId.BasicGetEmpty: return new BasicGetEmpty();
                case ProtocolCommandId.BasicAck: return new BasicAck(span);
                case ProtocolCommandId.BasicReject: return new BasicReject(span);
                case ProtocolCommandId.BasicRecoverAsync: return new BasicRecoverAsync(span);
                case ProtocolCommandId.BasicRecover: return new BasicRecover(span);
                case ProtocolCommandId.BasicRecoverOk: return new BasicRecoverOk();
                case ProtocolCommandId.BasicNack: return new BasicNack(span);
                case ProtocolCommandId.TxSelect: return new TxSelect();
                case ProtocolCommandId.TxSelectOk: return new TxSelectOk();
                case ProtocolCommandId.TxCommit: return new TxCommit();
                case ProtocolCommandId.TxCommitOk: return new TxCommitOk();
                case ProtocolCommandId.TxRollback: return new TxRollback();
                case ProtocolCommandId.TxRollbackOk: return new TxRollbackOk();
                case ProtocolCommandId.ConfirmSelect: return new ConfirmSelect();
                case ProtocolCommandId.ConfirmSelectOk: return new ConfirmSelectOk();
                default:
                    throw new Exceptions.UnknownClassOrMethodException((ushort)((uint)commandId >> 16), (ushort)((uint)commandId & 0xFFFF));
            }
        }

        internal override Client.Impl.ContentHeaderBase DecodeContentHeaderFrom(ushort classId, ReadOnlySpan<byte> span)
        {
            switch (classId)
            {
                case 60: return new BasicProperties(span);
                default: throw new Exceptions.UnknownClassOrMethodException(classId, 0);
            }
        }
    }
}

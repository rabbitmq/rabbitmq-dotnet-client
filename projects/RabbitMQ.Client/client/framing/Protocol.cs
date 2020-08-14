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
            ushort classId = Util.NetworkOrderDeserializer.ReadUInt16(span);
            ushort methodId = Util.NetworkOrderDeserializer.ReadUInt16(span.Slice(2));
            Client.Impl.MethodBase result = DecodeMethodFrom(classId, methodId);
            if(result != null)
            {
                Client.Impl.MethodArgumentReader reader = new Client.Impl.MethodArgumentReader(span.Slice(4));
                result.ReadArgumentsFrom(ref reader);
                return result;
            }

            throw new Exceptions.UnknownClassOrMethodException(classId, methodId);
        }

        internal Client.Impl.MethodBase DecodeMethodFrom(ushort classId, ushort methodId)
        {
            switch ((classId << 16) | methodId)
            {
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Start: return new ConnectionStart();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.StartOk: return new ConnectionStartOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Secure: return new ConnectionSecure();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.SecureOk: return new ConnectionSecureOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Tune: return new ConnectionTune();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.TuneOk: return new ConnectionTuneOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Open: return new ConnectionOpen();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.OpenOk: return new ConnectionOpenOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Close: return new ConnectionClose();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.CloseOk: return new ConnectionCloseOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Blocked: return new ConnectionBlocked();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Unblocked: return new ConnectionUnblocked();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecret: return new ConnectionUpdateSecret();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecretOk: return new ConnectionUpdateSecretOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Open: return new ChannelOpen();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.OpenOk: return new ChannelOpenOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Flow: return new ChannelFlow();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.FlowOk: return new ChannelFlowOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Close: return new ChannelClose();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.CloseOk: return new ChannelCloseOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Declare: return new ExchangeDeclare();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeclareOk: return new ExchangeDeclareOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Delete: return new ExchangeDelete();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeleteOk: return new ExchangeDeleteOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Bind: return new ExchangeBind();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.BindOk: return new ExchangeBindOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Unbind: return new ExchangeUnbind();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.UnbindOk: return new ExchangeUnbindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Declare: return new QueueDeclare();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.DeclareOk: return new Impl.QueueDeclareOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Bind: return new QueueBind();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.BindOk: return new QueueBindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Unbind: return new QueueUnbind();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.UnbindOk: return new QueueUnbindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Purge: return new QueuePurge();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.PurgeOk: return new QueuePurgeOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Delete: return new QueueDelete();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.DeleteOk: return new QueueDeleteOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Qos: return new BasicQos();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.QosOk: return new BasicQosOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Consume: return new BasicConsume();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.ConsumeOk: return new BasicConsumeOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Cancel: return new BasicCancel();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.CancelOk: return new BasicCancelOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Publish: return new BasicPublish();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Return: return new BasicReturn();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Deliver: return new BasicDeliver();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Get: return new BasicGet();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetOk: return new BasicGetOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetEmpty: return new BasicGetEmpty();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Ack: return new BasicAck();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Reject: return new BasicReject();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverAsync: return new BasicRecoverAsync();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Recover: return new BasicRecover();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverOk: return new BasicRecoverOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Nack: return new BasicNack();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Select: return new TxSelect();
                case (ClassConstants.Tx << 16) | TxMethodConstants.SelectOk: return new TxSelectOk();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Commit: return new TxCommit();
                case (ClassConstants.Tx << 16) | TxMethodConstants.CommitOk: return new TxCommitOk();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Rollback: return new TxRollback();
                case (ClassConstants.Tx << 16) | TxMethodConstants.RollbackOk: return new TxRollbackOk();
                case (ClassConstants.Confirm << 16) | ConfirmMethodConstants.Select: return new ConfirmSelect();
                case (ClassConstants.Confirm << 16) | ConfirmMethodConstants.SelectOk: return new ConfirmSelectOk();
                default: return null;
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

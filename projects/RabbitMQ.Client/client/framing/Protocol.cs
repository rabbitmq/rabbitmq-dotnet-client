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
    internal sealed class Protocol : RabbitMQ.Client.Framing.Impl.ProtocolBase
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

            throw new Client.Exceptions.UnknownClassOrMethodException(classId, methodId);
        }

        internal Client.Impl.MethodBase DecodeMethodFrom(ushort classId, ushort methodId)
        {
            switch ((classId << 16) | methodId)
            {
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Start: return new Impl.ConnectionStart();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.StartOk: return new Impl.ConnectionStartOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Secure: return new Impl.ConnectionSecure();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.SecureOk: return new Impl.ConnectionSecureOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Tune: return new Impl.ConnectionTune();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.TuneOk: return new Impl.ConnectionTuneOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Open: return new Impl.ConnectionOpen();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.OpenOk: return new Impl.ConnectionOpenOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Close: return new Impl.ConnectionClose();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.CloseOk: return new Impl.ConnectionCloseOk();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Blocked: return new Impl.ConnectionBlocked();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Unblocked: return new Impl.ConnectionUnblocked();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecret: return new Impl.ConnectionUpdateSecret();
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecretOk: return new Impl.ConnectionUpdateSecretOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Open: return new Impl.ChannelOpen();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.OpenOk: return new Impl.ChannelOpenOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Flow: return new Impl.ChannelFlow();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.FlowOk: return new Impl.ChannelFlowOk();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Close: return new Impl.ChannelClose();
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.CloseOk: return new Impl.ChannelCloseOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Declare: return new Impl.ExchangeDeclare();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeclareOk: return new Impl.ExchangeDeclareOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Delete: return new Impl.ExchangeDelete();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeleteOk: return new Impl.ExchangeDeleteOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Bind: return new Impl.ExchangeBind();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.BindOk: return new Impl.ExchangeBindOk();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Unbind: return new Impl.ExchangeUnbind();
                case (ClassConstants.Exchange << 16) | ExchangeMethodConstants.UnbindOk: return new Impl.ExchangeUnbindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Declare: return new Impl.QueueDeclare();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.DeclareOk: return new Impl.QueueDeclareOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Bind: return new Impl.QueueBind();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.BindOk: return new Impl.QueueBindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Unbind: return new Impl.QueueUnbind();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.UnbindOk: return new Impl.QueueUnbindOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Purge: return new Impl.QueuePurge();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.PurgeOk: return new Impl.QueuePurgeOk();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.Delete: return new Impl.QueueDelete();
                case (ClassConstants.Queue << 16) | QueueMethodConstants.DeleteOk: return new Impl.QueueDeleteOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Qos: return new Impl.BasicQos();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.QosOk: return new Impl.BasicQosOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Consume: return new Impl.BasicConsume();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.ConsumeOk: return new Impl.BasicConsumeOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Cancel: return new Impl.BasicCancel();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.CancelOk: return new Impl.BasicCancelOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Publish: return new Impl.BasicPublish();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Return: return new Impl.BasicReturn();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Deliver: return new Impl.BasicDeliver();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Get: return new Impl.BasicGet();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetOk: return new Impl.BasicGetOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetEmpty: return new Impl.BasicGetEmpty();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Ack: return new Impl.BasicAck();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Reject: return new Impl.BasicReject();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverAsync: return new Impl.BasicRecoverAsync();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Recover: return new Impl.BasicRecover();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverOk: return new Impl.BasicRecoverOk();
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Nack: return new Impl.BasicNack();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Select: return new Impl.TxSelect();
                case (ClassConstants.Tx << 16) | TxMethodConstants.SelectOk: return new Impl.TxSelectOk();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Commit: return new Impl.TxCommit();
                case (ClassConstants.Tx << 16) | TxMethodConstants.CommitOk: return new Impl.TxCommitOk();
                case (ClassConstants.Tx << 16) | TxMethodConstants.Rollback: return new Impl.TxRollback();
                case (ClassConstants.Tx << 16) | TxMethodConstants.RollbackOk: return new Impl.TxRollbackOk();
                case (ClassConstants.Confirm << 16) | ConfirmMethodConstants.Select: return new Impl.ConfirmSelect();
                case (ClassConstants.Confirm << 16) | ConfirmMethodConstants.SelectOk: return new Impl.ConfirmSelectOk();
                default: return null;
            }
        }


        internal override Client.Impl.ContentHeaderBase DecodeContentHeaderFrom(ushort classId)
        {
            switch (classId)
            {
                case 60: return new BasicProperties();
                default: throw new Client.Exceptions.UnknownClassOrMethodException(classId, 0);
            }
        }
    }
}

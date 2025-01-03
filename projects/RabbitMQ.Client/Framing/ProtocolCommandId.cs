// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

namespace RabbitMQ.Client.Framing
{
    internal enum ProtocolCommandId : uint
    {
        BasicQos = (ClassConstants.Basic << 16) | BasicMethodConstants.Qos,
        BasicQosOk = (ClassConstants.Basic << 16) | BasicMethodConstants.QosOk,
        BasicConsume = (ClassConstants.Basic << 16) | BasicMethodConstants.Consume,
        BasicConsumeOk = (ClassConstants.Basic << 16) | BasicMethodConstants.ConsumeOk,
        BasicCancel = (ClassConstants.Basic << 16) | BasicMethodConstants.Cancel,
        BasicCancelOk = (ClassConstants.Basic << 16) | BasicMethodConstants.CancelOk,
        BasicPublish = (ClassConstants.Basic << 16) | BasicMethodConstants.Publish,
        BasicReturn = (ClassConstants.Basic << 16) | BasicMethodConstants.Return,
        BasicDeliver = (ClassConstants.Basic << 16) | BasicMethodConstants.Deliver,
        BasicGet = (ClassConstants.Basic << 16) | BasicMethodConstants.Get,
        BasicGetOk = (ClassConstants.Basic << 16) | BasicMethodConstants.GetOk,
        BasicGetEmpty = (ClassConstants.Basic << 16) | BasicMethodConstants.GetEmpty,
        BasicAck = (ClassConstants.Basic << 16) | BasicMethodConstants.Ack,
        BasicReject = (ClassConstants.Basic << 16) | BasicMethodConstants.Reject,
        BasicRecoverAsync = (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverAsync,
        BasicRecover = (ClassConstants.Basic << 16) | BasicMethodConstants.Recover,
        BasicRecoverOk = (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverOk,
        BasicNack = (ClassConstants.Basic << 16) | BasicMethodConstants.Nack,
        ChannelOpen = (ClassConstants.Channel << 16) | ChannelMethodConstants.Open,
        ChannelOpenOk = (ClassConstants.Channel << 16) | ChannelMethodConstants.OpenOk,
        ChannelFlow = (ClassConstants.Channel << 16) | ChannelMethodConstants.Flow,
        ChannelFlowOk = (ClassConstants.Channel << 16) | ChannelMethodConstants.FlowOk,
        ChannelClose = (ClassConstants.Channel << 16) | ChannelMethodConstants.Close,
        ChannelCloseOk = (ClassConstants.Channel << 16) | ChannelMethodConstants.CloseOk,
        ConfirmSelect = (ClassConstants.Confirm << 16) | ConfirmMethodConstants.Select,
        ConfirmSelectOk = (ClassConstants.Confirm << 16) | ConfirmMethodConstants.SelectOk,
        ConnectionStart = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Start,
        ConnectionStartOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.StartOk,
        ConnectionSecure = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Secure,
        ConnectionSecureOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.SecureOk,
        ConnectionTune = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Tune,
        ConnectionTuneOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.TuneOk,
        ConnectionOpen = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Open,
        ConnectionOpenOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.OpenOk,
        ConnectionClose = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Close,
        ConnectionCloseOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.CloseOk,
        ConnectionBlocked = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Blocked,
        ConnectionUnblocked = (ClassConstants.Connection << 16) | ConnectionMethodConstants.Unblocked,
        ConnectionUpdateSecret = (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecret,
        ConnectionUpdateSecretOk = (ClassConstants.Connection << 16) | ConnectionMethodConstants.UpdateSecretOk,
        ExchangeDeclare = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Declare,
        ExchangeDeclareOk = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeclareOk,
        ExchangeDelete = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Delete,
        ExchangeDeleteOk = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.DeleteOk,
        ExchangeBind = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Bind,
        ExchangeBindOk = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.BindOk,
        ExchangeUnbind = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.Unbind,
        ExchangeUnbindOk = (ClassConstants.Exchange << 16) | ExchangeMethodConstants.UnbindOk,
        QueueDeclare = (ClassConstants.Queue << 16) | QueueMethodConstants.Declare,
        QueueDeclareOk = (ClassConstants.Queue << 16) | QueueMethodConstants.DeclareOk,
        QueueBind = (ClassConstants.Queue << 16) | QueueMethodConstants.Bind,
        QueueBindOk = (ClassConstants.Queue << 16) | QueueMethodConstants.BindOk,
        QueueUnbind = (ClassConstants.Queue << 16) | QueueMethodConstants.Unbind,
        QueueUnbindOk = (ClassConstants.Queue << 16) | QueueMethodConstants.UnbindOk,
        QueuePurge = (ClassConstants.Queue << 16) | QueueMethodConstants.Purge,
        QueuePurgeOk = (ClassConstants.Queue << 16) | QueueMethodConstants.PurgeOk,
        QueueDelete = (ClassConstants.Queue << 16) | QueueMethodConstants.Delete,
        QueueDeleteOk = (ClassConstants.Queue << 16) | QueueMethodConstants.DeleteOk,
        TxSelect = (ClassConstants.Tx << 16) | TxMethodConstants.Select,
        TxSelectOk = (ClassConstants.Tx << 16) | TxMethodConstants.SelectOk,
        TxCommit = (ClassConstants.Tx << 16) | TxMethodConstants.Commit,
        TxCommitOk = (ClassConstants.Tx << 16) | TxMethodConstants.CommitOk,
        TxRollback = (ClassConstants.Tx << 16) | TxMethodConstants.Rollback,
        TxRollbackOk = (ClassConstants.Tx << 16) | TxMethodConstants.RollbackOk
    }
}

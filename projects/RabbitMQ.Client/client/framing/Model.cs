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

using System.Collections.Generic;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.client.impl;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl;

internal class Model : ModelBase
{
    public Model(ConnectionConfig config, ISession session) : base(config, session)
    {
    }

    public override void ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
    {
        var cmd = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
        ModelSend(ref cmd);
    }

    public override void _Private_BasicCancel(string consumerTag, bool nowait)
    {
        var cmd = new BasicCancel(consumerTag, nowait);
        ModelSend(ref cmd);
    }

    public override void _Private_BasicConsume(string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments)
    {
        var cmd = new BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, nowait, arguments);
        ModelSend(ref cmd);
    }

    public override void _Private_BasicGet(string queue, bool autoAck)
    {
        var cmd = new BasicGet(queue, autoAck);
        ModelSend(ref cmd);
    }

    public override void _Private_BasicRecover(bool requeue)
    {
        var cmd = new BasicRecover(requeue);
        ModelSend(ref cmd);
    }

    public override void _Private_ChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
    {
        var cmd = new ChannelClose(replyCode, replyText, classId, methodId);
        ModelSend(ref cmd);
    }

    public override void _Private_ChannelCloseOk()
    {
        var cmd = new ChannelCloseOk();
        ModelSend(ref cmd);
    }

    public override void _Private_ChannelFlowOk(bool active)
    {
        var cmd = new ChannelFlowOk(active);
        ModelSend(ref cmd);
    }

    public override void _Private_ChannelOpen()
    {
        var cmd = new ChannelOpen();
        ModelRpc(ref cmd, ProtocolCommandId.ChannelOpenOk);
    }

    public override void _Private_ConfirmSelect(bool nowait)
    {
        var method = new ConfirmSelect(nowait);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.ConfirmSelectOk);
        }
    }

    public override void _Private_ConnectionCloseOk()
    {
        var cmd = new ConnectionCloseOk();
        ModelSend(ref cmd);
    }

    public override void _Private_ConnectionOpen(string virtualHost)
    {
        var cmd = new ConnectionOpen(virtualHost);
        ModelSend(ref cmd);
    }

    public override void _Private_ConnectionSecureOk(byte[] response)
    {
        var cmd = new ConnectionSecureOk(response);
        ModelSend(ref cmd);
    }

    public override void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale)
    {
        var cmd = new ConnectionStartOk(clientProperties, mechanism, response, locale);
        ModelSend(ref cmd);
    }

    public override void _Private_UpdateSecret(byte[] newSecret, string reason)
    {
        var cmd = new ConnectionUpdateSecret(newSecret, reason);
        ModelRpc(ref cmd, ProtocolCommandId.ConnectionUpdateSecretOk);
    }

    public override void _Private_ExchangeBind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
    {
        ExchangeBind method = new ExchangeBind(destination, source, routingKey, nowait, arguments);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.ExchangeBindOk);
        }
    }

    public override void _Private_ExchangeDeclare(string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments)
    {
        ExchangeDeclare method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, @internal, nowait, arguments);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.ExchangeDeclareOk);
        }
    }

    public override void _Private_ExchangeDelete(string exchange, bool ifUnused, bool nowait)
    {
        ExchangeDelete method = new ExchangeDelete(exchange, ifUnused, nowait);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.ExchangeDeleteOk);
        }
    }

    public override void _Private_ExchangeUnbind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
    {
        ExchangeUnbind method = new ExchangeUnbind(destination, source, routingKey, nowait, arguments);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.ExchangeUnbindOk);
        }
    }

    public override void _Private_QueueBind(string queue, string exchange, string routingKey, bool nowait, IDictionary<string, object> arguments)
    {
        QueueBind method = new QueueBind(queue, exchange, routingKey, nowait, arguments);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelRpc(ref method, ProtocolCommandId.QueueBindOk);
        }
    }

    public override void _Private_QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments)
    {
        QueueDeclare method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, nowait, arguments);
        if (nowait)
        {
            ModelSend(ref method);
        }
        else
        {
            ModelSend(ref method);
        }
    }

    public override uint _Private_QueueDelete(string queue, bool ifUnused, bool ifEmpty, bool nowait)
    {
        QueueDelete method = new QueueDelete(queue, ifUnused, ifEmpty, nowait);
        if (nowait)
        {
            ModelSend(ref method);
            return 0xFFFFFFFF;
        }

        return ModelRpc(ref method, ProtocolCommandId.QueueDeleteOk, memory => new QueueDeleteOk(memory.Span)._messageCount);
    }

    public override uint _Private_QueuePurge(string queue, bool nowait)
    {
        QueuePurge method = new QueuePurge(queue, nowait);
        if (nowait)
        {
            ModelSend(ref method);
            return 0xFFFFFFFF;
        }

        return ModelRpc(ref method, ProtocolCommandId.QueuePurgeOk, memory => new QueuePurgeOk(memory.Span)._messageCount);
    }

    public override void BasicAck(ulong deliveryTag, bool multiple)
    {
        var cmd = new BasicAck(deliveryTag, multiple);
        ModelSend(ref cmd);
    }

    public override void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
    {
        var cmd = new BasicNack(deliveryTag, multiple, requeue);
        ModelSend(ref cmd);
    }

    public override void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
    {
        var cmd = new BasicQos(prefetchSize, prefetchCount, global);
        ModelRpc(ref cmd, ProtocolCommandId.BasicQosOk);
    }

    public override void BasicRecoverAsync(bool requeue)
    {
        var cmd = new BasicRecoverAsync(requeue);
        ModelSend(ref cmd);
    }

    public override void BasicReject(ulong deliveryTag, bool requeue)
    {
        var cmd = new BasicReject(deliveryTag, requeue);
        ModelSend(ref cmd);
    }

    public override void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
    {
        var cmd = new QueueUnbind(queue, exchange, routingKey, arguments);
        ModelRpc(ref cmd, ProtocolCommandId.QueueUnbindOk);
    }

    public override void TxCommit()
    {
        var cmd = new TxCommit();
        ModelRpc(ref cmd, ProtocolCommandId.TxCommitOk);
    }

    public override void TxRollback()
    {
        var cmd = new TxRollback();
        ModelRpc(ref cmd, ProtocolCommandId.TxRollbackOk);
    }

    public override void TxSelect()
    {
        var cmd = new TxSelect();
        ModelRpc(ref cmd, ProtocolCommandId.TxSelectOk);
    }

    protected override bool DispatchAsynchronous(in IncomingCommand cmd)
    {
        switch (cmd.CommandId)
        {
            case ProtocolCommandId.BasicDeliver:
                {
                    HandleBasicDeliver(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicAck:
                {
                    HandleBasicAck(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicCancel:
                {
                    HandleBasicCancel(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicCancelOk:
                {
                    HandleBasicCancelOk(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicConsumeOk:
                {
                    HandleBasicConsumeOk(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicGetEmpty:
                {
                    cmd.ReturnMethodBuffer();
                    HandleBasicGetEmpty();
                    return true;
                }
            case ProtocolCommandId.BasicGetOk:
                {
                    HandleBasicGetOk(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicNack:
                {
                    HandleBasicNack(in cmd);
                    return true;
                }
            case ProtocolCommandId.BasicRecoverOk:
                {
                    cmd.ReturnMethodBuffer();
                    HandleBasicRecoverOk();
                    return true;
                }
            case ProtocolCommandId.BasicReturn:
                {
                    HandleBasicReturn(in cmd);
                    return true;
                }
            case ProtocolCommandId.ChannelClose:
                {
                    HandleChannelClose(in cmd);
                    return true;
                }
            case ProtocolCommandId.ChannelCloseOk:
                {
                    cmd.ReturnMethodBuffer();
                    HandleChannelCloseOk();
                    return true;
                }
            case ProtocolCommandId.ChannelFlow:
                {
                    HandleChannelFlow(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionBlocked:
                {
                    HandleConnectionBlocked(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionClose:
                {
                    HandleConnectionClose(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionSecure:
                {
                    HandleConnectionSecure(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionStart:
                {
                    HandleConnectionStart(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionTune:
                {
                    HandleConnectionTune(in cmd);
                    return true;
                }
            case ProtocolCommandId.ConnectionUnblocked:
                {
                    cmd.ReturnMethodBuffer();
                    HandleConnectionUnblocked();
                    return true;
                }
            case ProtocolCommandId.QueueDeclareOk:
                {
                    HandleQueueDeclareOk(in cmd);
                    return true;
                }
            default: return false;
        }
    }
}

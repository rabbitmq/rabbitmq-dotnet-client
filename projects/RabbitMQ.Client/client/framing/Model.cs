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
    internal class Model: ModelBase
    {
        public Model(ISession session)
            : base(session)
        {
        }

        public Model(ISession session, ConsumerWorkService workService)
            : base(session, workService)
        {
        }

        public override void ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
        {
            ModelSend(new ConnectionTuneOk(channelMax, frameMax, heartbeat));
        }

        public override void _Private_BasicCancel(string consumerTag, bool nowait)
        {
            ModelSend(new BasicCancel(consumerTag, nowait));
        }

        public override void _Private_BasicConsume(string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments)
        {
            ModelSend(new BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, nowait, arguments));
        }

        public override void _Private_BasicGet(string queue, bool autoAck)
        {
            ModelSend(new BasicGet(queue, autoAck));
        }

        public override void _Private_BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            ModelSend(new BasicPublish(exchange, routingKey, mandatory, default), (BasicProperties) basicProperties, body);
        }

        public override void _Private_BasicPublishMemory(ReadOnlyMemory<byte> exchange, ReadOnlyMemory<byte> routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            ModelSend(new BasicPublishMemory(exchange, routingKey, mandatory, default), (BasicProperties) basicProperties, body);
        }

        public override void _Private_BasicRecover(bool requeue)
        {
            ModelSend(new BasicRecover(requeue));
        }

        public override void _Private_ChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            ModelSend(new ChannelClose(replyCode, replyText, classId, methodId));
        }

        public override void _Private_ChannelCloseOk()
        {
            ModelSend(new ChannelCloseOk());
        }

        public override void _Private_ChannelFlowOk(bool active)
        {
            ModelSend(new ChannelFlowOk(active));
        }

        public override void _Private_ChannelOpen()
        {
            ModelRpc<ChannelOpenOk>(new ChannelOpen());
        }

        public override void _Private_ConfirmSelect(bool nowait)
        {
            var method = new ConfirmSelect(nowait);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<ConfirmSelectOk>(method);
            }
        }

        public override void _Private_ConnectionClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            ModelRpc<ConnectionCloseOk>(new ConnectionClose(replyCode, replyText, classId, methodId));
        }

        public override void _Private_ConnectionCloseOk()
        {
            ModelSend(new ConnectionCloseOk());
        }

        public override void _Private_ConnectionOpen(string virtualHost)
        {
            ModelSend(new ConnectionOpen(virtualHost));
        }

        public override void _Private_ConnectionSecureOk(byte[] response)
        {
            ModelSend(new ConnectionSecureOk(response));
        }

        public override void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale)
        {
            ModelSend(new ConnectionStartOk(clientProperties, mechanism, response, locale));
        }

        public override void _Private_UpdateSecret(byte[] newSecret, string reason)
        {
            ModelRpc<ConnectionUpdateSecretOk>(new ConnectionUpdateSecret(newSecret, reason));
        }

        public override void _Private_ExchangeBind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeBind method = new ExchangeBind(destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<ExchangeBindOk>(method);
            }
        }

        public override void _Private_ExchangeDeclare(string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeDeclare method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, @internal, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<ExchangeDeclareOk>(method);
            }
        }

        public override void _Private_ExchangeDelete(string exchange, bool ifUnused, bool nowait)
        {
            ExchangeDelete method = new ExchangeDelete(exchange, ifUnused, nowait);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<ExchangeDeleteOk>(method);
            }
        }

        public override void _Private_ExchangeUnbind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeUnbind method = new ExchangeUnbind(destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<ExchangeUnbindOk>(method);
            }
        }

        public override void _Private_QueueBind(string queue, string exchange, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            QueueBind method = new QueueBind(queue, exchange, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelRpc<QueueBindOk>(method);
            }
        }

        public override void _Private_QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments)
        {
            QueueDeclare method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                ModelSend(method);
            }
        }

        public override uint _Private_QueueDelete(string queue, bool ifUnused, bool ifEmpty, bool nowait)
        {
            QueueDelete method = new QueueDelete(queue, ifUnused, ifEmpty, nowait);
            if (nowait)
            {
                ModelSend(method);
                return 0xFFFFFFFF;
            }

            return ModelRpc<QueueDeleteOk>(method)._messageCount;
        }

        public override uint _Private_QueuePurge(string queue, bool nowait)
        {
            QueuePurge method = new QueuePurge(queue, nowait);
            if (nowait)
            {
                ModelSend(method);
                return 0xFFFFFFFF;
            }

            return ModelRpc<QueuePurgeOk>(method)._messageCount;
        }

        public override void BasicAck(ulong deliveryTag, bool multiple)
        {
            ModelSend(new BasicAck(deliveryTag, multiple));
        }

        public override void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            ModelSend(new BasicNack(deliveryTag, multiple, requeue));
        }

        public override void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
        {
            ModelRpc<BasicQosOk>(new BasicQos(prefetchSize, prefetchCount, global));
        }

        public override void BasicRecoverAsync(bool requeue)
        {
            ModelSend(new BasicRecoverAsync(requeue));
        }

        public override void BasicReject(ulong deliveryTag, bool requeue)
        {
            ModelSend(new BasicReject(deliveryTag, requeue));
        }

        public override IBasicProperties CreateBasicProperties()
        {
            return new BasicProperties();
        }

        public override void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ModelRpc<QueueUnbindOk>(new QueueUnbind(queue, exchange, routingKey, arguments));
        }

        public override void TxCommit()
        {
            ModelRpc<TxCommitOk>(new TxCommit());
        }

        public override void TxRollback()
        {
            ModelRpc<TxRollbackOk>(new TxRollback());
        }

        public override void TxSelect()
        {
            ModelRpc<TxSelectOk>(new TxSelect());
        }

        public override bool DispatchAsynchronous(in IncomingCommand cmd)
        {
            switch (cmd.Method.ProtocolCommandId)
            {
                case ProtocolCommandId.BasicDeliver:
                {
                    var __impl = (BasicDeliver)cmd.Method;
                    HandleBasicDeliver(__impl._consumerTag, __impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.BasicAck:
                {
                    var __impl = (BasicAck)cmd.Method;
                    HandleBasicAck(__impl._deliveryTag, __impl._multiple);
                    return true;
                }
                case ProtocolCommandId.BasicCancel:
                {
                    var __impl = (BasicCancel)cmd.Method;
                    HandleBasicCancel(__impl._consumerTag, __impl._nowait);
                    return true;
                }
                case ProtocolCommandId.BasicCancelOk:
                {
                    var __impl = (BasicCancelOk)cmd.Method;
                    HandleBasicCancelOk(__impl._consumerTag);
                    return true;
                }
                case ProtocolCommandId.BasicConsumeOk:
                {
                    var __impl = (BasicConsumeOk)cmd.Method;
                    HandleBasicConsumeOk(__impl._consumerTag);
                    return true;
                }
                case ProtocolCommandId.BasicGetEmpty:
                {
                    HandleBasicGetEmpty();
                    return true;
                }
                case ProtocolCommandId.BasicGetOk:
                {
                    var __impl = (BasicGetOk)cmd.Method;
                    HandleBasicGetOk(__impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, __impl._messageCount, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.BasicNack:
                {
                    var __impl = (BasicNack)cmd.Method;
                    HandleBasicNack(__impl._deliveryTag, __impl._multiple, __impl._requeue);
                    return true;
                }
                case ProtocolCommandId.BasicRecoverOk:
                {
                    HandleBasicRecoverOk();
                    return true;
                }
                case ProtocolCommandId.BasicReturn:
                {
                    var __impl = (BasicReturn)cmd.Method;
                    HandleBasicReturn(__impl._replyCode, __impl._replyText, __impl._exchange, __impl._routingKey, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case ProtocolCommandId.ChannelClose:
                {
                    var __impl = (ChannelClose)cmd.Method;
                    HandleChannelClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case ProtocolCommandId.ChannelCloseOk:
                {
                    HandleChannelCloseOk();
                    return true;
                }
                case ProtocolCommandId.ChannelFlow:
                {
                    var __impl = (ChannelFlow)cmd.Method;
                    HandleChannelFlow(__impl._active);
                    return true;
                }
                case ProtocolCommandId.ConnectionBlocked:
                {
                    var __impl = (ConnectionBlocked)cmd.Method;
                    HandleConnectionBlocked(__impl._reason);
                    return true;
                }
                case ProtocolCommandId.ConnectionClose:
                {
                    var __impl = (ConnectionClose)cmd.Method;
                    HandleConnectionClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case ProtocolCommandId.ConnectionSecure:
                {
                    var __impl = (ConnectionSecure)cmd.Method;
                    HandleConnectionSecure(__impl._challenge);
                    return true;
                }
                case ProtocolCommandId.ConnectionStart:
                {
                    var __impl = (ConnectionStart)cmd.Method;
                    HandleConnectionStart(__impl._versionMajor, __impl._versionMinor, __impl._serverProperties, __impl._mechanisms, __impl._locales);
                    return true;
                }
                case ProtocolCommandId.ConnectionTune:
                {
                    var __impl = (ConnectionTune)cmd.Method;
                    HandleConnectionTune(__impl._channelMax, __impl._frameMax, __impl._heartbeat);
                    return true;
                }
                case ProtocolCommandId.ConnectionUnblocked:
                {
                    HandleConnectionUnblocked();
                    return true;
                }
                case ProtocolCommandId.QueueDeclareOk:
                {
                    var __impl = (QueueDeclareOk)cmd.Method;
                    HandleQueueDeclareOk(__impl._queue, __impl._messageCount, __impl._consumerCount);
                    return true;
                }
                default: return false;
            }
        }
    }
}

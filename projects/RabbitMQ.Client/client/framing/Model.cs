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
using RabbitMQ.Client.Exceptions;
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
            ModelSend(new BasicConsume(default, queue, consumerTag, noLocal, autoAck, exclusive, nowait, arguments));
        }

        public override void _Private_BasicGet(string queue, bool autoAck)
        {
            ModelSend(new BasicGet(default, queue, autoAck));
        }

        public override void _Private_BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            ModelSend(new BasicPublish(default, exchange, routingKey, mandatory, default), (BasicProperties) basicProperties, body);
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

        public override void _Private_ChannelOpen(string outOfBand)
        {
            MethodBase __repBase = ModelRpc(new ChannelOpen(outOfBand));
            if (!(__repBase is ChannelOpenOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
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
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is ConfirmSelectOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_ConnectionClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            MethodBase __repBase = ModelRpc(new ConnectionClose(replyCode, replyText, classId, methodId));
            if (!(__repBase is ConnectionCloseOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override void _Private_ConnectionCloseOk()
        {
            ModelSend(new ConnectionCloseOk());
        }

        public override void _Private_ConnectionOpen(string virtualHost, string capabilities, bool insist)
        {
            ModelSend(new ConnectionOpen(virtualHost, capabilities, insist));
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
            MethodBase __repBase = ModelRpc(new ConnectionUpdateSecret(newSecret, reason));
            if (!(__repBase is ConnectionUpdateSecretOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override void _Private_ExchangeBind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeBind method = new ExchangeBind(default, destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is ExchangeBindOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_ExchangeDeclare(string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeDeclare method = new ExchangeDeclare(default, exchange, type, passive, durable, autoDelete, @internal, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is ExchangeDeclareOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_ExchangeDelete(string exchange, bool ifUnused, bool nowait)
        {
            ExchangeDelete method = new ExchangeDelete(default, exchange, ifUnused, nowait);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is ExchangeDeleteOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_ExchangeUnbind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            ExchangeUnbind method = new ExchangeUnbind(default, destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is ExchangeUnbindOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_QueueBind(string queue, string exchange, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            QueueBind method = new QueueBind(default, queue, exchange, routingKey, nowait, arguments);
            if (nowait)
            {
                ModelSend(method);
            }
            else
            {
                MethodBase __repBase = ModelRpc(method);
                if (!(__repBase is QueueBindOk))
                {
                    throw new UnexpectedMethodException(__repBase);
                }
            }
        }

        public override void _Private_QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments)
        {
            QueueDeclare method = new QueueDeclare(default, queue, passive, durable, exclusive, autoDelete, nowait, arguments);
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
            QueueDelete method = new QueueDelete(default, queue, ifUnused, ifEmpty, nowait);
            if (nowait)
            {
                ModelSend(method);
                return 0xFFFFFFFF;
            }

            MethodBase __repBase = ModelRpc(method);
            if (!(__repBase is QueueDeleteOk __rep))
            {
                throw new UnexpectedMethodException(__repBase);
            }
            return __rep._messageCount;
        }

        public override uint _Private_QueuePurge(string queue, bool nowait)
        {
            QueuePurge method = new QueuePurge(default, queue, nowait);
            if (nowait)
            {
                ModelSend(method);
                return 0xFFFFFFFF;
            }

            MethodBase __repBase = ModelRpc(method);
            if (!(__repBase is QueuePurgeOk __rep))
            {
                throw new UnexpectedMethodException(__repBase);
            }
            return __rep._messageCount;
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
            MethodBase __repBase = ModelRpc(new BasicQos(prefetchSize, prefetchCount, global));
            if (!(__repBase is BasicQosOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
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
            MethodBase __repBase = ModelRpc(new QueueUnbind(default, queue, exchange, routingKey, arguments));
            if (!(__repBase is QueueUnbindOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override void TxCommit()
        {
            MethodBase __repBase = ModelRpc(new TxCommit());
            if (!(__repBase is TxCommitOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override void TxRollback()
        {
            MethodBase __repBase = ModelRpc(new TxRollback());
            if (!(__repBase is TxRollbackOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override void TxSelect()
        {
            MethodBase __repBase = ModelRpc(new TxSelect());
            if (!(__repBase is TxSelectOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }

        public override bool DispatchAsynchronous(in IncomingCommand cmd)
        {
            switch ((cmd.Method.ProtocolClassId << 16) | cmd.Method.ProtocolMethodId)
            {
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Deliver:
                {
                    var __impl = (BasicDeliver)cmd.Method;
                    HandleBasicDeliver(__impl._consumerTag, __impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Ack:
                {
                    var __impl = (BasicAck)cmd.Method;
                    HandleBasicAck(__impl._deliveryTag, __impl._multiple);
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Cancel:
                {
                    var __impl = (BasicCancel)cmd.Method;
                    HandleBasicCancel(__impl._consumerTag, __impl._nowait);
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.CancelOk:
                {
                    var __impl = (BasicCancelOk)cmd.Method;
                    HandleBasicCancelOk(__impl._consumerTag);
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.ConsumeOk:
                {
                    var __impl = (BasicConsumeOk)cmd.Method;
                    HandleBasicConsumeOk(__impl._consumerTag);
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetEmpty:
                {
                    HandleBasicGetEmpty();
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.GetOk:
                {
                    var __impl = (BasicGetOk)cmd.Method;
                    HandleBasicGetOk(__impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, __impl._messageCount, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Nack:
                {
                    var __impl = (BasicNack)cmd.Method;
                    HandleBasicNack(__impl._deliveryTag, __impl._multiple, __impl._requeue);
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.RecoverOk:
                {
                    HandleBasicRecoverOk();
                    return true;
                }
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Return:
                {
                    var __impl = (BasicReturn)cmd.Method;
                    HandleBasicReturn(__impl._replyCode, __impl._replyText, __impl._exchange, __impl._routingKey, (IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
                    return true;
                }
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Close:
                {
                    var __impl = (ChannelClose)cmd.Method;
                    HandleChannelClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.CloseOk:
                {
                    HandleChannelCloseOk();
                    return true;
                }
                case (ClassConstants.Channel << 16) | ChannelMethodConstants.Flow:
                {
                    var __impl = (ChannelFlow)cmd.Method;
                    HandleChannelFlow(__impl._active);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Blocked:
                {
                    var __impl = (ConnectionBlocked)cmd.Method;
                    HandleConnectionBlocked(__impl._reason);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Close:
                {
                    var __impl = (ConnectionClose)cmd.Method;
                    HandleConnectionClose(__impl._replyCode, __impl._replyText, __impl._classId, __impl._methodId);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.OpenOk:
                {
                    var __impl = (ConnectionOpenOk)cmd.Method;
                    HandleConnectionOpenOk(__impl._reserved1);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Secure:
                {
                    var __impl = (ConnectionSecure)cmd.Method;
                    HandleConnectionSecure(__impl._challenge);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Start:
                {
                    var __impl = (ConnectionStart)cmd.Method;
                    HandleConnectionStart(__impl._versionMajor, __impl._versionMinor, __impl._serverProperties, __impl._mechanisms, __impl._locales);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Tune:
                {
                    var __impl = (ConnectionTune)cmd.Method;
                    HandleConnectionTune(__impl._channelMax, __impl._frameMax, __impl._heartbeat);
                    return true;
                }
                case (ClassConstants.Connection << 16) | ConnectionMethodConstants.Unblocked:
                {
                    HandleConnectionUnblocked();
                    return true;
                }
                case (ClassConstants.Queue << 16) | QueueMethodConstants.DeclareOk:
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

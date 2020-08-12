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
    internal class Model: Client.Impl.ModelBase
    {
        public Model(Client.Impl.ISession session): base(session) {}
        public Model(Client.Impl.ISession session, ConsumerWorkService workService): base(session, workService) {}
        public override void ConnectionTuneOk (ushort @channelMax, uint @frameMax, ushort @heartbeat)
        {
            ConnectionTuneOk __req = new ConnectionTuneOk()
            {
                _channelMax = @channelMax,
                _frameMax = @frameMax,
                _heartbeat = @heartbeat,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_BasicCancel (string @consumerTag, bool @nowait)
        {
            BasicCancel __req = new BasicCancel()
            {
                _consumerTag = @consumerTag,
                _nowait = @nowait,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_BasicConsume (string @queue, string @consumerTag, bool @noLocal, bool @autoAck, bool @exclusive, bool @nowait, IDictionary<string, object> @arguments)
        {
            BasicConsume __req = new BasicConsume()
            {
                _queue = @queue,
                _consumerTag = @consumerTag,
                _noLocal = @noLocal,
                _noAck = @autoAck,
                _exclusive = @exclusive,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_BasicGet (string @queue, bool @autoAck)
        {
            BasicGet __req = new BasicGet()
            {
                _queue = @queue,
                _noAck = @autoAck,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_BasicPublish (string @exchange, string @routingKey, bool @mandatory, RabbitMQ.Client.IBasicProperties @basicProperties, ReadOnlyMemory<byte> @body)
        {
            BasicPublish __req = new BasicPublish()
            {
                _exchange = @exchange,
                _routingKey = @routingKey,
                _mandatory = @mandatory,
            };
            ModelSend(__req, (BasicProperties) basicProperties,body);
        }
        public override void _Private_BasicRecover (bool @requeue)
        {
            BasicRecover __req = new BasicRecover()
            {
                _requeue = @requeue,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_ChannelClose (ushort @replyCode, string @replyText, ushort @classId, ushort @methodId)
        {
            ChannelClose __req = new ChannelClose()
            {
                _replyCode = @replyCode,
                _replyText = @replyText,
                _classId = @classId,
                _methodId = @methodId,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_ChannelCloseOk ()
        {
            ChannelCloseOk __req = new ChannelCloseOk();
            ModelSend(__req,null,null);
        }
        public override void _Private_ChannelFlowOk (bool @active)
        {
            ChannelFlowOk __req = new ChannelFlowOk()
            {
                _active = @active,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_ChannelOpen (string @outOfBand)
        {
            ChannelOpen __req = new ChannelOpen()
            {
                _reserved1 = @outOfBand,
            };
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ChannelOpenOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ConfirmSelect (bool @nowait)
        {
            ConfirmSelect __req = new ConfirmSelect()
            {
                _nowait = @nowait,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ConfirmSelectOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ConnectionClose (ushort @replyCode, string @replyText, ushort @classId, ushort @methodId)
        {
            ConnectionClose __req = new ConnectionClose()
            {
                _replyCode = @replyCode,
                _replyText = @replyText,
                _classId = @classId,
                _methodId = @methodId,
            };
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ConnectionCloseOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ConnectionCloseOk ()
        {
            ConnectionCloseOk __req = new ConnectionCloseOk();
            ModelSend(__req,null,null);
        }
        public override void _Private_ConnectionOpen (string @virtualHost, string @capabilities, bool @insist)
        {
            ConnectionOpen __req = new ConnectionOpen()
            {
                _virtualHost = @virtualHost,
                _reserved1 = @capabilities,
                _reserved2 = @insist,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_ConnectionSecureOk (byte[] @response)
        {
            ConnectionSecureOk __req = new ConnectionSecureOk()
            {
                _response = @response,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_ConnectionStartOk (IDictionary<string, object> @clientProperties, string @mechanism, byte[] @response, string @locale)
        {
            ConnectionStartOk __req = new ConnectionStartOk()
            {
                _clientProperties = @clientProperties,
                _mechanism = @mechanism,
                _response = @response,
                _locale = @locale,
            };
            ModelSend(__req,null,null);
        }
        public override void _Private_UpdateSecret (byte[] @newSecret, string @reason)
        {
            ConnectionUpdateSecret __req = new ConnectionUpdateSecret()
            {
                _newSecret = @newSecret,
                _reason = @reason,
            };
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ConnectionUpdateSecretOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ExchangeBind (string @destination, string @source, string @routingKey, bool @nowait, IDictionary<string, object> @arguments)
        {
            ExchangeBind __req = new ExchangeBind()
            {
                _destination = @destination,
                _source = @source,
                _routingKey = @routingKey,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ExchangeBindOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ExchangeDeclare (string @exchange, string @type, bool @passive, bool @durable, bool @autoDelete, bool @internal, bool @nowait, IDictionary<string, object> @arguments)
        {
            ExchangeDeclare __req = new ExchangeDeclare()
            {
                _exchange = @exchange,
                _type = @type,
                _passive = @passive,
                _durable = @durable,
                _autoDelete = @autoDelete,
                _internal = @internal,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ExchangeDeclareOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ExchangeDelete (string @exchange, bool @ifUnused, bool @nowait)
        {
            ExchangeDelete __req = new ExchangeDelete()
            {
                _exchange = @exchange,
                _ifUnused = @ifUnused,
                _nowait = @nowait,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ExchangeDeleteOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_ExchangeUnbind (string @destination, string @source, string @routingKey, bool @nowait, IDictionary<string, object> @arguments)
        {
            ExchangeUnbind __req = new ExchangeUnbind()
            {
                _destination = @destination,
                _source = @source,
                _routingKey = @routingKey,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is ExchangeUnbindOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_QueueBind (string @queue, string @exchange, string @routingKey, bool @nowait, IDictionary<string, object> @arguments)
        {
            QueueBind __req = new QueueBind()
            {
                _queue = @queue,
                _exchange = @exchange,
                _routingKey = @routingKey,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is QueueBindOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void _Private_QueueDeclare (string @queue, bool @passive, bool @durable, bool @exclusive, bool @autoDelete, bool @nowait, IDictionary<string, object> @arguments)
        {
            QueueDeclare __req = new QueueDeclare()
            {
                _queue = @queue,
                _passive = @passive,
                _durable = @durable,
                _exclusive = @exclusive,
                _autoDelete = @autoDelete,
                _nowait = @nowait,
                _arguments = @arguments,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return;
            }
            ModelSend(__req,null,null);
        }
        public override uint _Private_QueueDelete (string @queue, bool @ifUnused, bool @ifEmpty, bool @nowait)
        {
            QueueDelete __req = new QueueDelete()
            {
                _queue = @queue,
                _ifUnused = @ifUnused,
                _ifEmpty = @ifEmpty,
                _nowait = @nowait,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return 0xFFFFFFFF;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is QueueDeleteOk __rep))
            {
                throw new UnexpectedMethodException(__repBase);
            }
            return __rep._messageCount;
        }
        public override uint _Private_QueuePurge (string @queue, bool @nowait)
        {
            QueuePurge __req = new QueuePurge()
            {
                _queue = @queue,
                _nowait = @nowait,
            };
            if (nowait) {
                ModelSend(__req,null,null);
                return 0xFFFFFFFF;
            }
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is QueuePurgeOk __rep))
            {
                throw new UnexpectedMethodException(__repBase);
            }
            return __rep._messageCount;
        }
        public override void BasicAck (ulong @deliveryTag, bool @multiple)
        {
            BasicAck __req = new BasicAck()
            {
                _deliveryTag = @deliveryTag,
                _multiple = @multiple,
            };
            ModelSend(__req,null,null);
        }
        public override void BasicNack (ulong @deliveryTag, bool @multiple, bool @requeue)
        {
            BasicNack __req = new BasicNack()
            {
                _deliveryTag = @deliveryTag,
                _multiple = @multiple,
                _requeue = @requeue,
            };
            ModelSend(__req,null,null);
        }
        public override void BasicQos (uint @prefetchSize, ushort @prefetchCount, bool @global)
        {
            BasicQos __req = new BasicQos()
            {
                _prefetchSize = @prefetchSize,
                _prefetchCount = @prefetchCount,
                _global = @global,
            };
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is BasicQosOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void BasicRecoverAsync (bool @requeue)
        {
            BasicRecoverAsync __req = new BasicRecoverAsync()
            {
                _requeue = @requeue,
            };
            ModelSend(__req,null,null);
        }
        public override void BasicReject (ulong @deliveryTag, bool @requeue)
        {
            BasicReject __req = new BasicReject()
            {
                _deliveryTag = @deliveryTag,
                _requeue = @requeue,
            };
            ModelSend(__req,null,null);
        }
        public override RabbitMQ.Client.IBasicProperties CreateBasicProperties ()
        {
            return new BasicProperties();
        }
        public override void QueueUnbind (string @queue, string @exchange, string @routingKey, IDictionary<string, object> @arguments)
        {
            QueueUnbind __req = new QueueUnbind()
            {
                _queue = @queue,
                _exchange = @exchange,
                _routingKey = @routingKey,
                _arguments = @arguments,
            };
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is QueueUnbindOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void TxCommit ()
        {
            TxCommit __req = new TxCommit();
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is TxCommitOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void TxRollback ()
        {
            TxRollback __req = new TxRollback();
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is TxRollbackOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override void TxSelect ()
        {
            TxSelect __req = new TxSelect();
            Client.Impl.MethodBase __repBase = ModelRpc(__req, null, null);
            if (!(__repBase is TxSelectOk))
            {
                throw new UnexpectedMethodException(__repBase);
            }
        }
        public override bool DispatchAsynchronous(in IncomingCommand cmd) {
            switch ((cmd.Method.ProtocolClassId << 16) | cmd.Method.ProtocolMethodId)
            {
                case (ClassConstants.Basic << 16) | BasicMethodConstants.Deliver:
                {
                    var __impl = (BasicDeliver)cmd.Method;
                    HandleBasicDeliver(__impl._consumerTag, __impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, (RabbitMQ.Client.IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
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
                    HandleBasicGetOk(__impl._deliveryTag, __impl._redelivered, __impl._exchange, __impl._routingKey, __impl._messageCount, (RabbitMQ.Client.IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
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
                    HandleBasicReturn(__impl._replyCode, __impl._replyText, __impl._exchange, __impl._routingKey, (RabbitMQ.Client.IBasicProperties) cmd.Header, cmd.Body, cmd.TakeoverPayload());
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

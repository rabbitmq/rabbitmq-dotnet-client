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
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing.Impl
{
    internal class Channel : ChannelBase
    {
        public Channel(ConnectionConfig config, ISession session) : base(config, session)
        {
        }

        public override void ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
        {
            ChannelSend(new ConnectionTuneOk(channelMax, frameMax, heartbeat));
        }

        public override void _Private_BasicCancel(string consumerTag, bool nowait)
        {
            ChannelSend(new BasicCancel(consumerTag, nowait));
        }

        public override void _Private_BasicConsume(string queue, string consumerTag, bool noLocal, bool autoAck, bool exclusive, bool nowait, IDictionary<string, object> arguments)
        {
            ChannelSend(new BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, nowait, arguments));
        }

        public override void _Private_BasicGet(string queue, bool autoAck)
        {
            ChannelSend(new BasicGet(queue, autoAck));
        }

        public override void _Private_BasicRecover(bool requeue)
        {
            ChannelSend(new BasicRecover(requeue));
        }

        public override void _Private_ChannelClose(ushort replyCode, string replyText, ushort classId, ushort methodId)
        {
            ChannelSend(new ChannelClose(replyCode, replyText, classId, methodId));
        }

        public override void _Private_ChannelCloseOk()
        {
            ChannelSend(new ChannelCloseOk());
        }

        public override void _Private_ChannelFlowOk(bool active)
        {
            ChannelSend(new ChannelFlowOk(active));
        }

        public override void _Private_ChannelOpen()
        {
            ChannelRpc(new ChannelOpen(), ProtocolCommandId.ChannelOpenOk);
        }

        public override void _Private_ConfirmSelect(bool nowait)
        {
            var method = new ConfirmSelect(nowait);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.ConfirmSelectOk);
            }
        }

        public override void _Private_ConnectionCloseOk()
        {
            ChannelSend(new ConnectionCloseOk());
        }

        public override void _Private_ConnectionOpen(string virtualHost)
        {
            ChannelSend(new ConnectionOpen(virtualHost));
        }

        public override ValueTask _Private_ConnectionOpenAsync(string virtualHost)
        {
            return ModelSendAsync(new ConnectionOpen(virtualHost));
        }

        public override void _Private_ConnectionSecureOk(byte[] response)
        {
            ChannelSend(new ConnectionSecureOk(response));
        }

        public override void _Private_ConnectionStartOk(IDictionary<string, object> clientProperties, string mechanism, byte[] response, string locale)
        {
            ChannelSend(new ConnectionStartOk(clientProperties, mechanism, response, locale));
        }

        public override void _Private_UpdateSecret(byte[] newSecret, string reason)
        {
            ChannelRpc(new ConnectionUpdateSecret(newSecret, reason), ProtocolCommandId.ConnectionUpdateSecretOk);
        }

        public override void _Private_ExchangeBind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            var method = new ExchangeBind(destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.ExchangeBindOk);
            }
        }

        public override void _Private_ExchangeDeclare(string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments)
        {
            var method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, @internal, nowait, arguments);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.ExchangeDeclareOk);
            }
        }

        public override void _Private_ExchangeDelete(string exchange, bool ifUnused, bool nowait)
        {
            var method = new ExchangeDelete(exchange, ifUnused, nowait);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.ExchangeDeleteOk);
            }
        }

        public override void _Private_ExchangeUnbind(string destination, string source, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            var method = new ExchangeUnbind(destination, source, routingKey, nowait, arguments);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.ExchangeUnbindOk);
            }
        }

        public override void _Private_QueueBind(string queue, string exchange, string routingKey, bool nowait, IDictionary<string, object> arguments)
        {
            var method = new QueueBind(queue, exchange, routingKey, nowait, arguments);
            if (nowait)
            {
                ChannelSend(method);
            }
            else
            {
                ChannelRpc(method, ProtocolCommandId.QueueBindOk);
            }
        }

        public override void _Private_QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, bool nowait, IDictionary<string, object> arguments)
        {
            /*
             * Note:
             * Even though nowait is a parameter, ChannelSend must be used
             */
            var method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, nowait, arguments);
            ChannelSend(method);
        }

        public override uint _Private_QueueDelete(string queue, bool ifUnused, bool ifEmpty, bool nowait)
        {
            var method = new QueueDelete(queue, ifUnused, ifEmpty, nowait);
            if (nowait)
            {
                ChannelSend(method);
                return 0xFFFFFFFF;
            }

            return ChannelRpc(method, ProtocolCommandId.QueueDeleteOk, memory => new QueueDeleteOk(memory.Span)._messageCount);
        }

        public override uint _Private_QueuePurge(string queue, bool nowait)
        {
            var method = new QueuePurge(queue, nowait);
            if (nowait)
            {
                ChannelSend(method);
                return 0xFFFFFFFF;
            }

            return ChannelRpc(method, ProtocolCommandId.QueuePurgeOk, memory => new QueuePurgeOk(memory.Span)._messageCount);
        }

        public override void BasicAck(ulong deliveryTag, bool multiple)
        {
            ChannelSend(new BasicAck(deliveryTag, multiple));
        }

        public override void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            ChannelSend(new BasicNack(deliveryTag, multiple, requeue));
        }

        public override void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
        {
            ChannelRpc(new BasicQos(prefetchSize, prefetchCount, global), ProtocolCommandId.BasicQosOk);
        }

        public override void BasicRecoverAsync(bool requeue)
        {
            ChannelSend(new BasicRecoverAsync(requeue));
        }

        public override void BasicReject(ulong deliveryTag, bool requeue)
        {
            ChannelSend(new BasicReject(deliveryTag, requeue));
        }

        public override ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue)
        {
            var method = new BasicReject(deliveryTag, requeue);
            return ModelSendAsync(method);
        }

        public override void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ChannelRpc(new QueueUnbind(queue, exchange, routingKey, arguments), ProtocolCommandId.QueueUnbindOk);
        }

        public override void TxCommit()
        {
            ChannelRpc(new TxCommit(), ProtocolCommandId.TxCommitOk);
        }

        public override void TxRollback()
        {
            ChannelRpc(new TxRollback(), ProtocolCommandId.TxRollbackOk);
        }

        public override void TxSelect()
        {
            ChannelRpc(new TxSelect(), ProtocolCommandId.TxSelectOk);
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
                        return HandleBasicCancelOk(in cmd);
                    }
                case ProtocolCommandId.BasicConsumeOk:
                    {
                        return HandleBasicConsumeOk(in cmd);
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
                        return HandleQueueDeclareOk(in cmd);
                    }
                default: return false;
            }
        }
    }
}

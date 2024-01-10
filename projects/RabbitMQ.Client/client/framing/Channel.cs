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
using System.Threading;
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

        public override void _Private_ConnectionCloseOk()
        {
            ChannelSend(new ConnectionCloseOk());
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple)
        {
            var method = new BasicAck(deliveryTag, multiple);
            // TODO cancellation token?
            return ModelSendAsync(method, CancellationToken.None);
        }

        public override ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue)
        {
            var method = new BasicNack(deliveryTag, multiple, requeue);
            // TODO use cancellation token
            return ModelSendAsync(method, CancellationToken.None);
        }

        public override Task BasicRejectAsync(ulong deliveryTag, bool requeue)
        {
            var method = new BasicReject(deliveryTag, requeue);
            // TODO cancellation token?
            return ModelSendAsync(method, CancellationToken.None).AsTask();
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
                        return HandleBasicGetEmpty(in cmd);
                    }
                case ProtocolCommandId.BasicGetOk:
                    {
                        return HandleBasicGetOk(in cmd);
                    }
                case ProtocolCommandId.BasicNack:
                    {
                        HandleBasicNack(in cmd);
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
                        HandleChannelCloseOk(in cmd);
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
                        HandleConnectionUnblocked(in cmd);
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

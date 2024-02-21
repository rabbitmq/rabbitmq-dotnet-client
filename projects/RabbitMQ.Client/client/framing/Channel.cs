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

        public override Task ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat, CancellationToken cancellationToken)
        {
            var method = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
            return ModelSendAsync(method, cancellationToken).AsTask();
        }

        public override Task _Private_ChannelCloseOkAsync(CancellationToken cancellationToken)
        {
            var method = new ChannelCloseOk();
            return ModelSendAsync(method, cancellationToken).AsTask();
        }

        public override Task _Private_ChannelFlowOkAsync(bool active, CancellationToken cancellationToken)
        {
            var method = new ChannelFlowOk(active);
            return ModelSendAsync(method, cancellationToken).AsTask();
        }

        public override Task _Private_ConnectionCloseOkAsync(CancellationToken cancellationToken)
        {
            var method = new ConnectionCloseOk();
            return ModelSendAsync(method, cancellationToken).AsTask();
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple)
        {
            var method = new BasicAck(deliveryTag, multiple);
            // TODO use cancellation token
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

        protected override Task<bool> DispatchCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            switch (cmd.CommandId)
            {
                case ProtocolCommandId.BasicDeliver:
                    {
                        HandleBasicDeliver(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.BasicAck:
                    {
                        HandleBasicAck(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.BasicCancel:
                    {
                        HandleBasicCancel(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.BasicCancelOk:
                    {
                        bool result = HandleBasicCancelOk(in cmd);
                        return Task.FromResult(result);
                    }
                case ProtocolCommandId.BasicConsumeOk:
                    {
                        bool result = HandleBasicConsumeOk(in cmd);
                        return Task.FromResult(result);
                    }
                case ProtocolCommandId.BasicGetEmpty:
                    {
                        bool result = HandleBasicGetEmpty(in cmd);
                        return Task.FromResult(result);
                    }
                case ProtocolCommandId.BasicGetOk:
                    {
                        bool result = HandleBasicGetOk(in cmd);
                        return Task.FromResult(result);
                    }
                case ProtocolCommandId.BasicNack:
                    {
                        HandleBasicNack(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.BasicReturn:
                    {
                        HandleBasicReturn(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ChannelClose:
                    {
                        return HandleChannelCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelCloseOk:
                    {
                        HandleChannelCloseOk(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ChannelFlow:
                    {
                        return HandleChannelFlowAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionBlocked:
                    {
                        HandleConnectionBlocked(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ConnectionClose:
                    {
                        return HandleConnectionCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionSecure:
                    {
                        HandleConnectionSecure(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ConnectionStart:
                    {
                        HandleConnectionStart(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ConnectionTune:
                    {
                        HandleConnectionTune(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.ConnectionUnblocked:
                    {
                        HandleConnectionUnblocked(in cmd);
                        return Task.FromResult(true);
                    }
                case ProtocolCommandId.QueueDeclareOk:
                    {
                        bool result = HandleQueueDeclareOk(in cmd);
                        return Task.FromResult(result);
                    }
                default: return Task.FromResult(false);
            }
        }
    }
}

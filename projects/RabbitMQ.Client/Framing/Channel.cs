// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.Framing
{
    internal class Channel : ChannelBase
    {
        public Channel(ConnectionConfig config, ISession session, ushort? consumerDispatchConcurrency = null)
            : base(config, session, consumerDispatchConcurrency)
        {
        }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken)
        {
            var method = new BasicAck(deliveryTag, multiple);
            return ModelSendAsync(in method, cancellationToken);
        }

        public override ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicNack(deliveryTag, multiple, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

        public override ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicReject(deliveryTag, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

        /// <summary>
        /// Returning <c>true</c> from this method means that the command was server-originated,
        /// and handled already.
        /// Returning <c>false</c> (the default) means that the incoming command is the response to
        /// a client-initiated RPC call, and must be handled.
        /// </summary>
        /// <param name="cmd">The incoming command from the AMQP server</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        protected override Task<bool> DispatchCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            switch (cmd.CommandId)
            {
                case ProtocolCommandId.BasicCancel:
                    {
                        // Note: always returns true
                        return HandleBasicCancelAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicDeliver:
                    {
                        // Note: always returns true
                        return HandleBasicDeliverAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicAck:
                    {
                        return HandleBasicAck(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicNack:
                    {
                        return HandleBasicNack(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicReturn:
                    {
                        // Note: always returns true
                        return HandleBasicReturn(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelClose:
                    {
                        // Note: always returns true
                        return HandleChannelCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelCloseOk:
                    {
                        // Note: always returns true
                        return HandleChannelCloseOkAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelFlow:
                    {
                        // Note: always returns true
                        return HandleChannelFlowAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionBlocked:
                    {
                        // Note: always returns true
                        return HandleConnectionBlockedAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionClose:
                    {
                        // Note: always returns true
                        return HandleConnectionCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionSecure:
                    {
                        // Note: always returns true
                        return HandleConnectionSecureAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionStart:
                    {
                        // Note: always returns true
                        return HandleConnectionStartAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionTune:
                    {
                        // Note: always returns true
                        return HandleConnectionTuneAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionUnblocked:
                    {
                        // Note: always returns true
                        return HandleConnectionUnblockedAsync(cancellationToken);
                    }
                default:
                    {
                        return Task.FromResult(false);
                    }
            }
        }
    }
}

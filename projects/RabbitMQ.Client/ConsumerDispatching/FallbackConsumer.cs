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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Logging;

namespace RabbitMQ.Client.ConsumerDispatching
{
    internal sealed class FallbackConsumer : IAsyncBasicConsumer
    {
        public IChannel? Channel { get; } = null;

        Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicCancelOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicConsumeOkAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleBasicDeliverAsync)} for tag {consumerTag}");
            return Task.CompletedTask;
        }

        Task IAsyncBasicConsumer.HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
        {
            ESLog.Info($"Unhandled {nameof(IAsyncBasicConsumer.HandleChannelShutdownAsync)}");
            return Task.CompletedTask;
        }
    }
}

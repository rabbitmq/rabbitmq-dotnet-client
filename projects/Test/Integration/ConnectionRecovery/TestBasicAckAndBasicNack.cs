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

using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;
using QueueDeclareOk = RabbitMQ.Client.QueueDeclareOk;

namespace Test.Integration.ConnectionRecovery
{
    public class TestBasicAckAndBasicNack : TestConnectionRecoveryBase
    {
        private readonly string _queueName;

        public TestBasicAckAndBasicNack(ITestOutputHelper output) : base(output)
        {
            _queueName = $"{nameof(TestBasicAckAndBasicNack)}-{Guid.NewGuid()}";
        }

        public override async Task DisposeAsync()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.ClientProvidedName += "-TearDown";
            using (IConnection conn = await cf.CreateConnectionAsync())
            {
                using (IChannel ch = await conn.CreateChannelAsync())
                {
                    await ch.QueueDeleteAsync(_queueName);
                    await ch.CloseAsync();
                }
                await conn.CloseAsync();
            }

            await base.DisposeAsync();
        }

        [Fact]
        public async Task TestBasicAckAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new AckingBasicConsumer(_channel, TotalMessageCount, allMessagesSeenTcs);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(_queueName, false, false, false);
            string queueName = q.QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicNackAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new NackingBasicConsumer(_channel, TotalMessageCount, allMessagesSeenTcs);

            QueueDeclareOk q = await _channel.QueueDeclareAsync(_queueName, false, false, false);
            string queueName = q.QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicRejectAfterChannelRecovery()
        {
            var allMessagesSeenTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cons = new RejectingBasicConsumer(_channel, TotalMessageCount, allMessagesSeenTcs);

            string queueName = (await _channel.QueueDeclareAsync(_queueName, false, false, false)).QueueName;
            Assert.Equal(queueName, _queueName);

            await _channel.BasicQosAsync(0, 1, false);
            await _channel.BasicConsumeAsync(queueName, false, cons);

            TaskCompletionSource<bool> sl = PrepareForShutdown(_conn);
            TaskCompletionSource<bool> rl = PrepareForRecovery(_conn);

            await PublishMessagesWhileClosingConnAsync(queueName);

            await WaitAsync(sl, "connection shutdown");
            await WaitAsync(rl, "connection recovery");
            await WaitAsync(allMessagesSeenTcs, "all messages seen");
        }

        [Fact]
        public async Task TestBasicAckAfterBasicGetAndChannelRecovery()
        {
            string q = GenerateQueueName();
            await _channel.QueueDeclareAsync(q, false, false, false);
            // create an offset
            await _channel.BasicPublishAsync("", q, _messageBody);
            await Task.Delay(50);
            BasicGetResult g = await _channel.BasicGetAsync(q, false);
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_conn.IsOpen);
            Assert.True(_channel.IsOpen);
            // ack the message after recovery - this should be out of range and ignored
            await _channel.BasicAckAsync(g.DeliveryTag, false);
            // do a sync operation to 'check' there is no channel exception
            await _channel.BasicGetAsync(q, false);
        }

        [Fact]
        public async Task TestBasicAckEventHandlerRecovery()
        {
            await _channel.ConfirmSelectAsync();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ((AutorecoveringChannel)_channel).BasicAcksAsync += (m, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };
            ((AutorecoveringChannel)_channel).BasicNacksAsync += (m, args) =>
            {
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            await CloseAndWaitForRecoveryAsync();
            Assert.True(_channel.IsOpen);

            await WithTemporaryNonExclusiveQueueAsync(_channel, (ch, q) =>
            {
                return ch.BasicPublishAsync("", q, _messageBody).AsTask();
            });

            await WaitAsync(tcs, "basic acks/nacks");
        }
    }
}

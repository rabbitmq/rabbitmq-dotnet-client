// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestBasicGet : IntegrationFixture
    {
        public TestBasicGet(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestBasicGetRoundTrip()
        {
            const string msg = "for async basic.get";

            QueueDeclareOk queueResult = await _channel.QueueDeclareAsync(string.Empty, false, true, true);
            string queueName = queueResult.QueueName;

            await _channel.BasicPublishAsync(string.Empty, queueName, true, _encoding.GetBytes(msg));

            BasicGetResult getResult = await _channel.BasicGetAsync(queueName, true);
            Assert.Equal(msg, _encoding.GetString(getResult.Body.ToArray()));

            QueueDeclareOk queueResultPassive = await _channel.QueueDeclarePassiveAsync(queue: queueName);
            Assert.Equal((uint)0, queueResultPassive.MessageCount);

            Assert.Null(await _channel.BasicGetAsync(queueName, true));
        }

        [Fact]
        public Task TestBasicGetWithClosedChannel()
        {
            return WithNonEmptyQueueAsync((_, q) =>
            {
                return WithClosedChannelAsync(ch =>
                {
                    return Assert.ThrowsAsync<AlreadyClosedException>(() =>
                    {
                        return ch.BasicGetAsync(q, true);
                    });
                });
            });
        }

        [Fact]
        public Task TestBasicGetWithEmptyResponse()
        {
            return WithEmptyQueueAsync(async (channel, queue) =>
            {
                BasicGetResult res = await channel.BasicGetAsync(queue, false);
                Assert.Null(res);
            });
        }

        [Fact]
        public Task TestBasicGetWithNonEmptyResponseAndAutoAckMode()
        {
            const string msg = "for basic.get";
            return WithNonEmptyQueueAsync(async (channel, queue) =>
            {
                BasicGetResult res = await channel.BasicGetAsync(queue, true);
                Assert.Equal(msg, _encoding.GetString(res.Body.ToArray()));
                await AssertMessageCountAsync(queue, 0);
            }, msg);
        }

        [Fact]
        public async Task BasicGetThrowsWhenTheHeadersAreNestedTooDeep_GHSA_qhmm()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            await using IConnection conn = await cf.CreateConnectionAsync();
            var closed = new TaskCompletionSource<ShutdownEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            conn.ConnectionShutdownAsync += (_, ea) =>
            {
                closed.TrySetResult(ea);
                return Task.CompletedTask;
            };
            await using IChannel ch = await conn.CreateChannelAsync();
            QueueDeclareOk q = await ch.QueueDeclareAsync(string.Empty, false, true, true);

            IDictionary<string, object> headers = new Dictionary<string, object>();
            for (int i = 0; i < 40; i++)
            {
                headers = new Dictionary<string, object> { ["a"] = headers };
            }
            using (var util = new Util(_output))
            {
                await util.PublishToQueueAsync(q.QueueName, new Dictionary<string, object> { ["headers"] = headers }, "deep");
            }

            // The management API publishes on another broker channel, so wait for the message to be enqueued.
            DateTime deadline = DateTime.UtcNow + WaitSpan;
            while ((await ch.QueueDeclarePassiveAsync(q.QueueName)).MessageCount == 0)
            {
                Assert.True(DateTime.UtcNow < deadline, "the published message never reached the queue");
                await Task.Delay(50);
            }

            // Without completing the continuation on a parse failure, the get never returns.
            await Assert.ThrowsAsync<SyntaxErrorException>(() => ch.BasicGetAsync(q.QueueName, true).WaitAsync(WaitSpan));

            await closed.Task.WaitAsync(WaitSpan);
            Assert.Equal(Constants.SyntaxError, (await closed.Task).ReplyCode);
        }

        private Task EnsureNotEmptyAsync(string q, string body)
        {
            return WithTemporaryChannelAsync(ch =>
            {
                return ch.BasicPublishAsync("", q, _encoding.GetBytes(body)).AsTask();
            });
        }

        private async Task WithClosedChannelAsync(Func<IChannel, Task> action)
        {
            IChannel channel = await _conn.CreateChannelAsync();
            await channel.CloseAsync();
            await action(channel);
        }

        private Task WithNonEmptyQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithNonEmptyQueueAsync(action, "msg");
        }

        private Task WithNonEmptyQueueAsync(Func<IChannel, string, Task> action, string msg)
        {
            return WithTemporaryExclusiveQueueAsync(async (ch, q) =>
            {
                await EnsureNotEmptyAsync(q, msg);
                await action(ch, q);
            });
        }

        private Task WithEmptyQueueAsync(Func<IChannel, string, Task> action)
        {
            return WithTemporaryExclusiveQueueAsync(async (channel, queue) =>
            {
                await channel.QueuePurgeAsync(queue);
                await action(channel, queue);
            });
        }
    }
}

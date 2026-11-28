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


using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestTracingDefects : SequentialIntegrationFixture
    {
        public TestTracingDefects(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ADeliverySpanIsNotAChildOfTheSpanThatCreatedTheChannel_GH2154()
        {
            using var appSource = new ActivitySource("Test.GH2154");
            using var appRecorder = new ActivityRecorder(appSource.Name, "create channel") { VerifyParent = false };
            using var deliverRecorder = new ActivityRecorder(RabbitMQActivitySource.SubscriberSourceName, "deliver")
            { VerifyParent = false };
            string queueName = GenerateQueueName();

            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UseRoutingKeyAsOperationName = false };
            await using IConnection conn = await cf.CreateConnectionAsync();
            IChannel ch;
            using (Activity creating = appSource.StartActivity("create channel"))
            {
                Assert.NotNull(creating);
                ch = await conn.CreateChannelAsync();
            }

            await using (ch)
            {
                await ch.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: false);
                var consumer = new AsyncEventingBasicConsumer(ch);
                var receivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ReceivedAsync += (_, _) =>
                {
                    receivedTcs.TrySetResult(true);
                    return Task.CompletedTask;
                };
                await ch.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
                // The publisher source has no listener here, so the message carries no trace context.
                await ch.BasicPublishAsync("", queueName, Encoding.UTF8.GetBytes("hi"));
                await receivedTcs.Task.WaitAsync(WaitSpan);
                Assert.True(await receivedTcs.Task);
            }

            Activity deliver = deliverRecorder.VerifyActivityRecordedOnce();
            Assert.Null(deliver.ParentId);
        }
    }
}

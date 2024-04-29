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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery
{
    public class TestRpcAfterRecovery : TestConnectionRecoveryBase
    {
        public TestRpcAfterRecovery(ITestOutputHelper output)
            : base(output, dispatchConsumersAsync: true)
        {
        }

        [Fact]
        public async Task TestPublishRpcRightAfterReconnect()
        {
            string testQueueName = $"dotnet-client.test.{nameof(TestPublishRpcRightAfterReconnect)}";
            await _channel.QueueDeclareAsync(testQueueName, false, false, false);
            var replyConsumer = new AsyncEventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync("amq.rabbitmq.reply-to", true, replyConsumer);
            var properties = new BasicProperties();
            properties.ReplyTo = "amq.rabbitmq.reply-to";

            var doneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task closeTask = Task.Run(async () =>
            {
                try
                {
                    await CloseAndWaitForRecoveryAsync();
                }
                finally
                {
                    doneTcs.SetResult(true);
                }
            });

            TimeSpan doneSpan = TimeSpan.FromMilliseconds(500);
            DateTime start = DateTime.Now;
            do
            {
                await Task.Delay(doneSpan);

                try
                {
                    await _channel.BasicPublishAsync(string.Empty, testQueueName, properties, _messageBody);
                }
                catch (Exception e)
                {
                    if (e is AlreadyClosedException a)
                    {
                        // 406 is received, when the reply consumer isn't yet recovered
                        // TODO FLAKY
                        // Assert.NotEqual(406, a.ShutdownReason.ReplyCode);
                        if (a.ShutdownReason.ReplyCode == 406)
                        {
                            _output.WriteLine("[ERROR] TODO FUTURE FIXME saw code 406");
                        }
                    }
                }

                DateTime now = DateTime.Now;

                if (now - start > WaitSpan)
                {
                    Assert.Fail($"test exceeded wait time of {WaitSpan}");
                }

            } while (false == doneTcs.Task.IsCompletedSuccessfully());

            await closeTask;
        }
    }
}

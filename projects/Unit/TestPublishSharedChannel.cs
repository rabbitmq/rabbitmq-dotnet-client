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

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublishSharedChannel
    {
        private const string ExchangeName = "TestPublishSharedChannel_Ex";
        private const string QueueName = "TestPublishSharedChannel_Queue";
        private const string PublishKey = "TestPublishSharedChannel_RoutePub";
        private const int Loops = 20;
        private const int Repeats = 1000;

        private readonly byte[] _body = new byte[2048];

        private Exception _raisedException;

        [Test]
        public async Task MultiThreadPublishOnSharedChannel()
        {
            // Arrange
            var connFactory = new ConnectionFactory
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (IConnection conn = connFactory.CreateConnection())
            {
                conn.ConnectionShutdown += (_, args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        Assert.Fail("Unexpected connection shutdown!");
                    }
                };

                await using (IChannel channel = await conn.CreateChannelAsync().ConfigureAwait(false))
                {
                    await channel.DeclareExchangeAsync(ExchangeName, "topic", durable: false, autoDelete: true);
                    await channel.DeclareQueueWithoutConfirmationAsync(QueueName, false, false, true);
                    await channel.BindQueueAsync(QueueName, ExchangeName, PublishKey);

                    // Act
                    var pubTask = Task.Run(() => PublishMessagesAsync(channel));
                    var pubTask2 = Task.Run(() => PublishMessagesAsync(channel));

                    await Task.WhenAll(pubTask, pubTask2);
                }
            }

            // Assert
            Assert.Null(_raisedException);

            async Task PublishMessagesAsync(IChannel channel)
            {
                try
                {
                    for (int i = 0; i < Loops; i++)
                    {
                        for (int j = 0; j < Repeats; j++)
                        {
                            await channel.PublishMessageAsync(ExchangeName, PublishKey, null, _body).ConfigureAwait(false);
                        }

                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    _raisedException = e;
                }
            }
        }
    }
}

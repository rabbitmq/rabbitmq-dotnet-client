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
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestPublishSharedChannelAsync : IntegrationFixture
    {
        private const string QueueName = "TestPublishSharedChannel_Queue";
        private static readonly CachedString ExchangeName = new CachedString("TestPublishSharedChannel_Ex");
        private static readonly CachedString PublishKey = new CachedString("TestPublishSharedChannel_RoutePub");
        private const int Loops = 20;
        private const int Repeats = 1000;

        private readonly byte[] _body = new byte[2048];

        private Exception _raisedException;

        public TestPublishSharedChannelAsync(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: test sets up its own factory, conns, channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        public override Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task MultiThreadPublishOnSharedChannel()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;

            using (IConnection conn = await cf.CreateConnectionAsync())
            {
                try
                {
                    Assert.IsNotType<RabbitMQ.Client.Framing.Impl.AutorecoveringConnection>(conn);
                    conn.ConnectionShutdownAsync += HandleConnectionShutdownAsync;

                    using (IChannel channel = await conn.CreateChannelAsync())
                    {
                        try
                        {
                            channel.ChannelShutdown += HandleChannelShutdown;
                            await channel.ExchangeDeclareAsync(ExchangeName.Value, ExchangeType.Topic, passive: false, durable: false, autoDelete: true,
                                noWait: false, arguments: null);
                            await channel.QueueDeclareAsync(QueueName, exclusive: false, autoDelete: true);
                            await channel.QueueBindAsync(QueueName, ExchangeName.Value, PublishKey.Value);

                            for (int i = 0; i < Loops; i++)
                            {
                                for (int j = 0; j < Repeats; j++)
                                {
                                    await channel.BasicPublishAsync(ExchangeName, PublishKey, false, _body);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _raisedException = e;
                        }
                        finally
                        {
                            await channel.CloseAsync();
                        }
                    }
                }
                finally
                {
                    await conn.CloseAsync();
                }
            }

            Assert.Null(_raisedException);
        }
    }
}

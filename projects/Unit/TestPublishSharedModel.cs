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
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace RabbitMQ.Client.Unit
{

    public class TestPublishSharedModel
    {
        private const string QueueName = "TestPublishSharedModel_Queue";
        private static readonly CachedString ExchangeName = new CachedString("TestPublishSharedModel_Ex");
        private static readonly CachedString PublishKey = new CachedString("TestPublishSharedModel_RoutePub");
        private const int Loops = 20;
        private const int Repeats = 1000;

        private readonly byte[] _body = new byte[2048];

        private Exception _raisedException;

        [Fact]
        public async Task MultiThreadPublishOnSharedModel()
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
                        Assert.True(false, "Unexpected connection shutdown!");
                    }
                };

                using (IModel model = conn.CreateModel())
                {
                    model.ExchangeDeclare(ExchangeName.Value, "topic", durable: false, autoDelete: true);
                    model.QueueDeclare(QueueName, false, false, true, null);
                    model.QueueBind(QueueName, ExchangeName.Value, PublishKey.Value, null);

                    // Act
                    await Task.WhenAll(NewFunction(model), NewFunction(model));
                }
            }

            // Assert
            Assert.Null(_raisedException);

            async Task NewFunction(IModel model)
            {
                try
                {
                    for (int i = 0; i < Loops; i++)
                    {
                        for (int j = 0; j < Repeats; j++)
                        {
                            model.BasicPublish(ExchangeName, PublishKey, _body, false);
                        }

                        await Task.Delay(1);
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

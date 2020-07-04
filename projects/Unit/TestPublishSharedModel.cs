// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublishSharedModel
    {
        private const string ExchangeName = "TestPublishSharedModel_Ex";
        private const string QueueName = "TestPublishSharedModel_Queue";
        private const string PublishKey = "TestPublishSharedModel_RoutePub";
        private const int Loops = 20;
        private const int Repeats = 1000;

        private readonly byte[] _body = new byte[2048];

        private Exception _raisedException;

        [Test]
        public async Task MultiThreadPublishOnSharedModel()
        {
            // Arrange
            var connFactory = new ConnectionFactory
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = false
            };

            using (var conn = connFactory.CreateConnection())
            {
                conn.ConnectionShutdown += (_, args) =>
                {
                    if (args.Initiator != ShutdownInitiator.Application)
                    {
                        Assert.Fail("Unexpected connection shutdown!");
                    }
                };

                using (var model = conn.CreateModel())
                {
                    model.ExchangeDeclare(ExchangeName, "topic", durable: false, autoDelete: true);
                    model.QueueDeclare(QueueName, false, false, true, null);
                    model.QueueBind(QueueName, ExchangeName, PublishKey, null);

                    // Act
                    var pubTask = Task.Run(() => NewFunction(model));
                    var pubTask2 = Task.Run(() => NewFunction(model));

                    await Task.WhenAll(pubTask, pubTask2);
                }
            }

            // Assert
            Assert.Null(_raisedException);

            void NewFunction(IModel model)
            {
                try
                {
                    for (int i = 0; i < Loops; i++)
                    {
                        for (int j = 0; j < Repeats; j++)
                        {
                            model.BasicPublish(ExchangeName, PublishKey, false, null, _body);
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

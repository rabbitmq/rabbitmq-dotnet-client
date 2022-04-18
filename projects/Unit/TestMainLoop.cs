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

using RabbitMQ.Client.Events;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{

    public class TestMainLoop : IntegrationFixture
    {
        public TestMainLoop(ITestOutputHelper output) : base(output)
        {
        }

        private sealed class FaultyConsumer : DefaultBasicConsumer
        {
            public FaultyConsumer(IModel model) : base(model) { }

            public override void HandleBasicDeliver(string consumerTag,
                                               ulong deliveryTag,
                                               bool redelivered,
                                               string exchange,
                                               string routingKey,
                                               in ReadOnlyBasicProperties properties,
                                               ReadOnlyMemory<byte> body)
            {
                throw new Exception("I am a bad consumer");
            }
        }

        [Fact]
        public void TestCloseWithFaultyConsumer()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection c = connFactory.CreateConnection();
            IModel m = _conn.CreateModel();
            object o = new object();
            string q = GenerateQueueName();
            m.QueueDeclare(q, false, false, false, null);

            CallbackExceptionEventArgs ea = null;
            m.CallbackException += (_, evt) =>
            {
                ea = evt;
                c.Close();
                Monitor.PulseAll(o);
            };
            m.BasicConsume(q, true, new FaultyConsumer(_model));
            m.BasicPublish("", q, _encoding.GetBytes("message"));
            WaitOn(o);

            Assert.NotNull(ea);
            Assert.False(c.IsOpen);
            Assert.Equal(200, c.CloseReason.ReplyCode);
        }
    }
}

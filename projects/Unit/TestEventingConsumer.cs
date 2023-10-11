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

using System.Threading;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    public class TestEventingConsumer : IntegrationFixture
    {
        public TestEventingConsumer(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestEventingConsumerRegistrationEvents()
        {
            string q = _channel.QueueDeclare();

            var registeredLatch = new ManualResetEventSlim(false);
            object registeredSender = null;
            var unregisteredLatch = new ManualResetEventSlim(false);
            object unregisteredSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(_channel);
            ec.Registered += (s, args) =>
            {
                registeredSender = s;
                registeredLatch.Set();
            };

            ec.Unregistered += (s, args) =>
            {
                unregisteredSender = s;
                unregisteredLatch.Set();
            };

            string tag = _channel.BasicConsume(q, false, ec);
            Wait(registeredLatch);

            Assert.NotNull(registeredSender);
            Assert.Equal(ec, registeredSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)registeredSender).Channel);

            _channel.BasicCancel(tag);
            Wait(unregisteredLatch);
            Assert.NotNull(unregisteredSender);
            Assert.Equal(ec, unregisteredSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)unregisteredSender).Channel);
        }

        [Fact]
        public void TestEventingConsumerDeliveryEvents()
        {
            string q = _channel.QueueDeclare();
            object o = new object();

            bool receivedInvoked = false;
            object receivedSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(_channel);
            ec.Received += (s, args) =>
            {
                receivedInvoked = true;
                receivedSender = s;

                Monitor.PulseAll(o);
            };

            _channel.BasicConsume(q, true, ec);
            _channel.BasicPublish("", q, _encoding.GetBytes("msg"));

            WaitOn(o);
            Assert.True(receivedInvoked);
            Assert.NotNull(receivedSender);
            Assert.Equal(ec, receivedSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)receivedSender).Channel);

            bool shutdownInvoked = false;
            object shutdownSender = null;

            ec.Shutdown += (s, args) =>
            {
                shutdownInvoked = true;
                shutdownSender = s;

                Monitor.PulseAll(o);
            };

            _channel.Close();
            WaitOn(o);

            Assert.True(shutdownInvoked);
            Assert.NotNull(shutdownSender);
            Assert.Equal(ec, shutdownSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)shutdownSender).Channel);
        }
    }
}

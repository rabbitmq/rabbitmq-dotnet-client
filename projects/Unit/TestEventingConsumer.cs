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
using System.Threading.Tasks;
using NUnit.Framework;

using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestEventingConsumer : IntegrationFixture
    {
        [Test]
        public async Task TestEventingConsumerRegistrationEvents()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);

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

            string tag = await _channel.ActivateConsumerAsync(ec, q, false).ConfigureAwait(false);
            Wait(registeredLatch);

            Assert.IsNotNull(registeredSender);
            Assert.AreEqual(ec, registeredSender);
            Assert.AreEqual(_channel, ((EventingBasicConsumer)registeredSender).Channel);

            await _channel.CancelConsumerAsync(tag).ConfigureAwait(false);
            Wait(unregisteredLatch);
            Assert.IsNotNull(unregisteredSender);
            Assert.AreEqual(ec, unregisteredSender);
            Assert.AreEqual(_channel, ((EventingBasicConsumer)unregisteredSender).Channel);
        }

        [Test]
        public async Task TestEventingConsumerDeliveryEvents()
        {
            (string q, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);
            var latch = new ManualResetEventSlim(false);

            bool receivedInvoked = false;
            object receivedSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(_channel);
            ec.Received += (s, args) =>
            {
                receivedInvoked = true;
                receivedSender = s;

                latch.Set();
            };

            await _channel.ActivateConsumerAsync(ec, q, true).ConfigureAwait(false);
            await _channel.PublishMessageAsync("", q, null, _encoding.GetBytes("msg")).ConfigureAwait(false);

            Wait(latch);
            Assert.IsTrue(receivedInvoked);
            Assert.IsNotNull(receivedSender);
            Assert.AreEqual(ec, receivedSender);
            Assert.AreEqual(_channel, ((EventingBasicConsumer)receivedSender).Channel);

            bool shutdownInvoked = false;
            object shutdownSender = null;

            ec.Shutdown += (s, args) =>
            {
                shutdownInvoked = true;
                shutdownSender = s;

                latch.Set();
            };

            latch.Reset();
            await _channel.CloseAsync().ConfigureAwait(false);
            Wait(latch);

            Assert.IsTrue(shutdownInvoked);
            Assert.IsNotNull(shutdownSender);
            Assert.AreEqual(ec, shutdownSender);
            Assert.AreEqual(_channel, ((EventingBasicConsumer)shutdownSender).Channel);
        }
    }
}

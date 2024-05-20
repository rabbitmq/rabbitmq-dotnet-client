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

using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestEventingConsumer : IntegrationFixture
    {
        public TestEventingConsumer(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestEventingConsumerRegistrationEvents()
        {
            string q = await _channel.QueueDeclareAsync();

            var registeredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            object registeredSender = null;

            var unregisteredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            object unregisteredSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(_channel);
            ec.Registered += (s, args) =>
            {
                registeredSender = s;
                registeredTcs.SetResult(true);
            };

            ec.Unregistered += (s, args) =>
            {
                unregisteredSender = s;
                unregisteredTcs.SetResult(true);
            };

            ConsumerTag tag = await _channel.BasicConsumeAsync(q, false, ec);
            await WaitAsync(registeredTcs, "consumer registered");

            Assert.NotNull(registeredSender);
            Assert.Equal(ec, registeredSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)registeredSender).Channel);

            await _channel.BasicCancelAsync(tag);

            await WaitAsync(unregisteredTcs, "consumer unregistered");
            Assert.NotNull(unregisteredSender);
            Assert.Equal(ec, unregisteredSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)unregisteredSender).Channel);
        }

        [Fact]
        public async Task TestEventingConsumerDeliveryEvents()
        {
            var tcs0 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string q = await _channel.QueueDeclareAsync();

            bool receivedInvoked = false;
            object receivedSender = null;

            var ec = new EventingBasicConsumer(_channel);
            ec.Received += (s, args) =>
            {
                receivedInvoked = true;
                receivedSender = s;
                tcs0.SetResult(true);
            };

            await _channel.BasicConsumeAsync(q, true, ec);
            await _channel.BasicPublishAsync(ExchangeName.Empty, q, _encoding.GetBytes("msg"));

            await WaitAsync(tcs0, "received event");
            var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                tcs1.SetResult(true);
            };

            await _channel.CloseAsync();

            await WaitAsync(tcs1, "shutdown event");

            Assert.True(shutdownInvoked);
            Assert.NotNull(shutdownSender);
            Assert.Equal(ec, shutdownSender);
            Assert.Equal(_channel, ((EventingBasicConsumer)shutdownSender).Channel);
        }
    }
}

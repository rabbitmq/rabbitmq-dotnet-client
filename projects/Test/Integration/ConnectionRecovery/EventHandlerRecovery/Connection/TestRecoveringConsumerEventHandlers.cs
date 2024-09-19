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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration.ConnectionRecovery.EventHandlerRecovery.Connection
{
    public class TestRecoveringConsumerEventHandlers : TestConnectionRecoveryBase
    {
        public TestRecoveringConsumerEventHandlers(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task TestRecoveringConsumerEventHandlers_Called(int iterations)
        {
            RabbitMQ.Client.QueueDeclareOk q = await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false);
            var cons = new AsyncEventingBasicConsumer(_channel);
            await _channel.BasicConsumeAsync(q, true, cons);

            int counter = 0;
            ((AutorecoveringConnection)_conn).RecoveringConsumerAsync += (sender, args) =>
            {
                Interlocked.Increment(ref counter);
                return Task.CompletedTask;
            };

            for (int i = 0; i < iterations; i++)
            {
                await CloseAndWaitForRecoveryAsync();
            }

            Assert.Equal(iterations, counter);
        }

        [Fact]
        public async Task TestRecoveringConsumerEventHandler_EventArgumentsArePassedDown()
        {
            const string key = "first-argument";
            const string value = "some-value";

            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { key, value }
            };

            RabbitMQ.Client.QueueDeclareOk q = await _channel.QueueDeclareAsync(GenerateQueueName(), false, false, false);
            var cons = new AsyncEventingBasicConsumer(_channel);
            string expectedCTag = await _channel.BasicConsumeAsync(consumer: cons, queue: q, autoAck: false,
                arguments: arguments, consumerTag: string.Empty);

            bool ctagMatches = false;
            bool consumerArgumentMatches = false;
            ((AutorecoveringConnection)_conn).RecoveringConsumerAsync += (sender, args) =>
            {
                // We cannot assert here because NUnit throws when an assertion fails. This exception is caught and
                // passed to a CallbackExceptionHandler, instead of failing the test. Instead, we have to do this trick
                // and assert in the test function.
                ctagMatches = args.ConsumerTag == expectedCTag;
                consumerArgumentMatches = (string)args.ConsumerArguments[key] == value;
                return Task.CompletedTask;
            };

            await CloseAndWaitForRecoveryAsync();
            Assert.True(ctagMatches, "expected consumer tag to match");
            Assert.True(consumerArgumentMatches, "expected consumer arguments to match");
            string actualVal = (string)Assert.Contains(key, arguments);
            Assert.Equal(value, actualVal);
        }
    }
}

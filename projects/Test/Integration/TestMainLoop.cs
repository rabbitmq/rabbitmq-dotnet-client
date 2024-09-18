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
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestMainLoop : IntegrationFixture
    {
        public TestMainLoop(ITestOutputHelper output) : base(output)
        {
        }

        protected override void DisposeAssertions()
        {
            /*
             * Note: don't do anything since these tests expect callback
             * exceptions
             */
        }

        private sealed class FaultyConsumer : AsyncDefaultBasicConsumer
        {
            public FaultyConsumer(IChannel channel) : base(channel) { }

            public override Task HandleBasicDeliverAsync(string consumerTag,
                ulong deliveryTag,
                bool redelivered,
                string exchange,
                string routingKey,
                IReadOnlyBasicProperties properties,
                ReadOnlyMemory<byte> body)
            {
                throw new Exception("I am a bad consumer");
            }
        }

        [Fact]
        public async Task TestCloseWithFaultyConsumer()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            QueueDeclareOk q = await _channel.QueueDeclareAsync(string.Empty, false, false, false);

            CallbackExceptionEventArgs ea = null;
            Task closeTask = null;
            _channel.CallbackExceptionAsync += (_, evt) =>
            {
                ea = evt;
                /*
                 * NOTE:
                 * await-ing CloseAsync here WILL result in a deadlock
                 */
                closeTask = _channel.CloseAsync();
                tcs.SetResult(true);
                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(q, true, new FaultyConsumer(_channel));
            await _channel.BasicPublishAsync(string.Empty, q, _encoding.GetBytes("message"));

            await WaitAsync(tcs, "CallbackException");

            Assert.NotNull(ea);
            await closeTask.WaitAsync(WaitSpan);
            Assert.False(_channel.IsOpen);
            Assert.Equal(200, _channel.CloseReason.ReplyCode);
        }
    }
}

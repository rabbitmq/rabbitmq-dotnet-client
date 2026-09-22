// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestDeliveredBodyIntegrity : IntegrationFixture
    {
        public TestDeliveredBodyIntegrity(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ADeliveredBodyIsNotHandedToTwoOwners_GH2039()
        {
            /*
             * The success path, which instance identity cannot observe: the reader returns the body on
             * a worker thread, and net8.0's pool caches it in that thread's slot. Body integrity can,
             * because a double return hands the array out again while the consumer is still reading
             * it. Verified by mutation: disposing the work item on the success path corrupts 156 of
             * these bodies, and also fails TestBasicPublish with a broker-side frame_too_large,
             * because the same thread rents outbound frame buffers.
             */
            const int Count = 400;
            const int Size = 4096;
            string queueName = (await _channel.QueueDeclareAsync(GenerateQueueName(), false, true, false)).QueueName;

            int corrupt = 0;
            int received = 0;
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (_, ea) =>
            {
                ReadOnlySpan<byte> body = ea.Body.Span;
                byte expected = body.Length > 0 ? body[0] : (byte)0;
                if (body.Length != Size)
                {
                    Interlocked.Increment(ref corrupt);
                }
                else
                {
                    foreach (byte b in body)
                    {
                        if (b != expected)
                        {
                            Interlocked.Increment(ref corrupt);
                            break;
                        }
                    }
                }

                if (Interlocked.Increment(ref received) == Count)
                {
                    done.TrySetResult(true);
                }

                return Task.CompletedTask;
            };
            await _channel.BasicConsumeAsync(queueName, true, consumer);

            for (int i = 0; i < Count; i++)
            {
                var payload = new byte[Size];
                payload.AsSpan().Fill((byte)(i % 251 + 1));
                await _channel.BasicPublishAsync(string.Empty, queueName, payload);
            }

            await WaitAsync(done, "all deliveries received");
            Assert.Equal(Count, Volatile.Read(ref received));
            Assert.Equal(0, Volatile.Read(ref corrupt));
        }
    }
}

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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestPublisherConfirms : IntegrationFixture
    {
        private const string QueueName = "RabbitMQ.Client.Unit.TestPublisherConfirms";

        [Test]
        public Task TestWaitForConfirmsWithoutTimeout()
        {
            return TestWaitForConfirmsAsync(200, ch =>
            {
                Assert.IsTrue(ch.WaitForConfirms());
            });
        }

        [Test]
        public Task TestWaitForConfirmsWithTimeout()
        {
            return TestWaitForConfirmsAsync(200, ch =>
            {
                Assert.IsTrue(ch.WaitForConfirms(TimeSpan.FromSeconds(4)));
            });
        }

        [Test]
        public Task TestWaitForConfirmsWithTimeout_AllMessagesAcked_WaitingHasTimedout_ReturnTrue()
        {
            return TestWaitForConfirmsAsync(200, ch =>
            {
                Assert.IsTrue(ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
            });
        }

        [Test]
        public Task TestWaitForConfirmsWithTimeout_MessageNacked_WaitingHasTimedout_ReturnFalse()
        {
            return TestWaitForConfirmsAsync(200, ch =>
            {
                SingleMessageRetrieval message = ch.RetrieveSingleMessageAsync(QueueName, false).AsTask().GetAwaiter().GetResult();

                // Fake a nack retrieval
                typeof(Channel)
                  .GetMethod("HandleBasicNack", BindingFlags.Instance | BindingFlags.NonPublic)
                  .Invoke(((AutorecoveringChannel)ch).NonDisposedDelegate, new object []{ message.DeliveryTag, false });

                Assert.IsFalse(ch.WaitForConfirms(TimeSpan.FromMilliseconds(1)));
            });
        }

        [Test]
        public async Task TestWaitForConfirmsWithEvents()
        {
            IChannel ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);

            await ch.DeclareQueueAsync(QueueName).ConfigureAwait(false);
            int n = 200;
            // number of event handler invocations
            int c = 0;

            ch.PublishTagAcknowledged += (_, __, ___) =>
            {
                System.Threading.Interlocked.Increment(ref c);
            };
            try
            {
                for (int i = 0; i < n; i++)
                {
                    await ch.PublishMessageAsync("", QueueName, null, _encoding.GetBytes("msg")).ConfigureAwait(false);
                }
                Thread.Sleep(TimeSpan.FromSeconds(1));
                ch.WaitForConfirms(TimeSpan.FromSeconds(5));

                // Note: number of event invocations is not guaranteed
                // to be equal to N because acks can be batched,
                // so we primarily care about event handlers being invoked
                // in this test
                Assert.IsTrue(c > 20);
            }
            finally
            {
                await ch.DeleteQueueAsync(QueueName).ConfigureAwait(false);
                await ch.CloseAsync().ConfigureAwait(false);
            }
        }

        protected async Task TestWaitForConfirmsAsync(int numberOfMessagesToPublish, Action<IChannel> fn)
        {
            var ch = await _conn.CreateChannelAsync().ConfigureAwait(false);
            await ch.ActivatePublishTagsAsync().ConfigureAwait(false);
            await ch.DeclareQueueAsync(QueueName).ConfigureAwait(false);

            for (int i = 0; i < numberOfMessagesToPublish; i++)
            {
                await ch.PublishMessageAsync("", QueueName, null, _encoding.GetBytes("msg")).ConfigureAwait(false);
            }

            try
            {
                fn(ch);
            }
            finally
            {
                await ch.DeleteQueueAsync(QueueName).ConfigureAwait(false);
                await ch.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}

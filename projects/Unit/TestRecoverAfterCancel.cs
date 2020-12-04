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

using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestRecoverAfterCancel
    {
        private IConnection _connection;
        private IChannel _channel;
        private string _queue;

        [SetUp]
        public async Task Connect()
        {
            _connection = new ConnectionFactory().CreateConnection();
            _channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
            _queue = (await _channel.DeclareQueueAsync("", false, true, false).ConfigureAwait(false)).QueueName;
        }

        [TearDown]
        public void Disconnect()
        {
            _connection.Abort();
        }

        [Test]
        public async Task TestRecoverAfterCancel_()
        {
            await _channel.PublishMessageAsync("", _queue, null, Encoding.UTF8.GetBytes("message")).ConfigureAwait(false);
            EventingBasicConsumer Consumer = new EventingBasicConsumer(_channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer.Received += (_, e) => EventQueue.Enqueue((e.Redelivered, e.Body.ToArray()));

            string CTag = await _channel.ActivateConsumerAsync(Consumer, _queue, false).ConfigureAwait(false);
            (bool Redelivered, byte[] Body) Event = EventQueue.Dequeue();
            await _channel.CancelConsumerAsync(CTag).ConfigureAwait(false);
            await _channel.ResendUnackedMessages(true);

            EventingBasicConsumer Consumer2 = new EventingBasicConsumer(_channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue2 = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer2.Received += (_, e) => EventQueue2.Enqueue((e.Redelivered, e.Body.ToArray()));
            await _channel.ActivateConsumerAsync(Consumer2, _queue, false).ConfigureAwait(false);
            (bool Redelivered, byte[] Body) Event2 = EventQueue2.Dequeue();

            CollectionAssert.AreEqual(Event.Body, Event2.Body);
            Assert.IsFalse(Event.Redelivered);
            Assert.IsTrue(Event2.Redelivered);
        }
    }
}

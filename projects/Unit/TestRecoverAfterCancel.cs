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
using System.Collections.Concurrent;
using System.Text;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;

using Xunit;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{

    public class TestRecoverAfterCancel : IDisposable
    {
        IConnection _connection;
        IChannel _channel;
        string _queue;
        int _callbackCount;

        public TestRecoverAfterCancel()
        {
            _connection = new ConnectionFactory().CreateConnection();
            _channel = _connection.CreateModel();
            _queue = _channel.QueueDeclare("", false, true, false, null);
        }

        public int ModelNumber(IChannel model)
        {
            return ((ModelBase)model).Session.ChannelNumber;
        }

        public void Dispose()
        {
            _connection.Abort();
        }

        [Fact]
        public void TestRecoverAfterCancel_()
        {
            UTF8Encoding enc = new UTF8Encoding();
            _channel.BasicPublish("", _queue, enc.GetBytes("message"));
            EventingBasicConsumer Consumer = new EventingBasicConsumer(_channel);
            BlockingCollection<(bool Redelivered, byte[] Body)> EventQueue = new BlockingCollection<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer.Received += (_, e) => EventQueue.Add((e.Redelivered, e.Body.ToArray()));

            string CTag = _channel.BasicConsume(_queue, false, Consumer);
            (bool Redelivered, byte[] Body) Event = EventQueue.Take();
            _channel.BasicCancel(CTag);
            _channel.BasicRecover(true);

            EventingBasicConsumer Consumer2 = new EventingBasicConsumer(_channel);
            BlockingCollection<(bool Redelivered, byte[] Body)> EventQueue2 = new BlockingCollection<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer2.Received += (_, e) => EventQueue2.Add((e.Redelivered, e.Body.ToArray()));
            _channel.BasicConsume(_queue, false, Consumer2);
            (bool Redelivered, byte[] Body) Event2 = EventQueue2.Take();

            Assert.Equal(Event.Body, Event2.Body);
            Assert.False(Event.Redelivered);
            Assert.True(Event2.Redelivered);
        }

        [Fact]
        public void TestRecoverCallback()
        {
            _callbackCount = 0;
            _channel.BasicRecoverOk += IncrCallback;
            _channel.BasicRecover(true);
            Assert.Equal(1, _callbackCount);
        }

        void IncrCallback(object sender, EventArgs args)
        {
            _callbackCount++;
        }
    }
}

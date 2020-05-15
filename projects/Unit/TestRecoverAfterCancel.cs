// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

#pragma warning disable 0618

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestRecoverAfterCancel
    {
        IConnection Connection;
        IModel Channel;
        string Queue;
        int callbackCount;

        public int ModelNumber(IModel model)
        {
            return ((ModelBase)model).Session.ChannelNumber;
        }

        [SetUp] public async ValueTask Connect()
        {
            Connection = await new ConnectionFactory().CreateConnection();
            Channel = await Connection.CreateModel();
            Queue = await Channel.QueueDeclare("", false, true, false, null);
        }

        [TearDown] public async ValueTask Disconnect()
        {
            await Connection.Abort();
        }

        [Test]
        public async ValueTask TestRecoverAfterCancel_()
        {
            UTF8Encoding enc = new UTF8Encoding();
            await Channel.BasicPublish("", Queue, null, enc.GetBytes("message"));
            EventingBasicConsumer Consumer = new EventingBasicConsumer(Channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer.Received += (_, e) => EventQueue.Enqueue((e.Redelivered, e.Body.ToArray()));

            string CTag = await Channel.BasicConsume(Queue, false, Consumer);
            (bool Redelivered, byte[] Body) Event = EventQueue.Dequeue();
            await Channel.BasicCancel(CTag);
            await Channel.BasicRecover(true);

            EventingBasicConsumer Consumer2 = new EventingBasicConsumer(Channel);
            SharedQueue<(bool Redelivered, byte[] Body)> EventQueue2 = new SharedQueue<(bool Redelivered, byte[] Body)>();
            // Making sure we copy the delivery body since it could be disposed at any time.
            Consumer2.Received += (_, e) => EventQueue2.Enqueue((e.Redelivered, e.Body.ToArray()));
            await Channel.BasicConsume(Queue, false, Consumer2);
            (bool Redelivered, byte[] Body) Event2 = EventQueue2.Dequeue();

            Assert.IsFalse(Event.Redelivered);
            Assert.IsTrue(Event2.Redelivered);
            CollectionAssert.AreEqual(Event.Body, Event2.Body);
        }

        [Test]
        public async ValueTask TestRecoverCallback()
        {
            callbackCount = 0;
            Channel.BasicRecoverOk += IncrCallback;
            await Channel.BasicRecover(true);
            Assert.AreEqual(1, callbackCount);
        }

        void IncrCallback(object sender, EventArgs args)
        {
            callbackCount++;
        }

    }
}

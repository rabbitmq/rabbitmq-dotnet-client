// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
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
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.Text;

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Events;
//using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestRecoverAfterCancel
    {
        IConnection Connection;
        IModel Channel;
        String Queue;
        int callbackCount;

        public static int ModelNumber(IModel model)
        {
            return ((ModelBase)model).Session.ChannelNumber;
        }

        [SetUp] public void Connect()
        {
            Connection = new ConnectionFactory().CreateConnection();
            Channel = Connection.CreateModel();
            Queue = Channel.QueueDeclare("", false, true, false, null);
        }

        [TearDown] public void Disconnect()
        {
            Connection.Abort();
        }

        [Test]
        public void TestRecoverAfterCancel_()
        {
            var enc = new UTF8Encoding();
            Channel.BasicPublish("", Queue, null, enc.GetBytes("message"));
            var Consumer = new EventingBasicConsumer(Channel);
            var queue = new System.Collections.Concurrent.BlockingCollection<BasicDeliverEventArgs>();
            Consumer.Received += (asdf, args) => queue.Add(args);

            String CTag = Channel.BasicConsume(Queue, false, Consumer);
            BasicDeliverEventArgs Event = queue.Take(); 
            Channel.BasicCancel(CTag);
            Channel.BasicRecover(true);

            var Consumer2 = new EventingBasicConsumer(Channel);
            var queue2 = new System.Collections.Concurrent.BlockingCollection<BasicDeliverEventArgs>();
            Consumer2.Received += (asdf, args) => queue2.Add(args);
            Channel.BasicConsume(Queue, false, Consumer2);
            BasicDeliverEventArgs Event2 = queue2.Take(); 

            Assert.AreEqual(Event.Body, Event2.Body);
            Assert.IsFalse(Event.Redelivered);
            Assert.IsTrue(Event2.Redelivered);
        }

        [Test]
        public void TestRecoverCallback()
        {
            callbackCount = 0;
            Channel.BasicRecoverOk += IncrCallback;
            Channel.BasicRecover(true);
            Assert.AreEqual(1, callbackCount);
        }

        void IncrCallback(object sender, EventArgs args)
        {
            callbackCount++;
        }

    }
}

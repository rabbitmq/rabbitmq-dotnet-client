// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2013 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Threading;

using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using RabbitMQ.Util;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConsumerCancelNotify {

        Object lockObject = new Object();
        bool notified = false;

        [Test]
        public void TestConsumerCancelNotification() {
            string queue = "queue_consumer_notify";
            ConnectionFactory connFactory = new ConnectionFactory();
            IConnection conn = connFactory.CreateConnection();
            IModel chan = conn.CreateModel();
            chan.QueueDeclare(queue, false, true, false, null);
            IBasicConsumer consumer = new CancelNotificationConsumer(chan, this);
            chan.BasicConsume(queue, false, consumer);

            chan.QueueDelete(queue);
            lock (lockObject) {
                if (!notified) {
                    Monitor.Wait(lockObject);
                }
                Assert.IsTrue(notified);
            }
        }

        public class CancelNotificationConsumer : QueueingBasicConsumer
        {
            TestConsumerCancelNotify testClass;

            public CancelNotificationConsumer(IModel model, TestConsumerCancelNotify tc) : base(model) {
                this.testClass = tc;
            }

            public override void HandleBasicCancel(string consumerTag) {
                lock (testClass.lockObject) {
                    testClass.notified = true;
                    Monitor.PulseAll(testClass.lockObject);
                }
            }
        }
    }
}


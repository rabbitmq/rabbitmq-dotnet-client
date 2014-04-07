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
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using NUnit.Framework;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestQueueDeclareOkPayload : IntegrationFixture
    {

        private readonly String QueueName = "declare-ok-test-queue";

        [Test]
        public void TestQueueDeclareOk()
        {
            Model.ConfirmSelect();
            QueueDeclareOk result;

            result = QueueDeclare();
            Assert.AreEqual(0, result.MessageCount);
            Assert.AreEqual(0, result.ConsumerCount);
            Assert.AreEqual(QueueName, result.QueueName);
            EnsureNotEmpty(result.QueueName);
            Model.WaitForConfirms();

            result = QueueDeclare();
            Assert.AreEqual(1, result.MessageCount);
            Assert.AreEqual(0, result.ConsumerCount);
            Assert.AreEqual(QueueName, result.QueueName);

            QueueingBasicConsumer consumer = new QueueingBasicConsumer(Model);
            Model.BasicConsume(QueueName, true, consumer);
            consumer.Queue.Dequeue();

            result = QueueDeclare();
            Assert.AreEqual(0, result.MessageCount);
            Assert.AreEqual(1, result.ConsumerCount);
            Assert.AreEqual(QueueName, result.QueueName);
        }

        private QueueDeclareOk QueueDeclare()
        {
            return Model.QueueDeclare(QueueName, false, true, true, null);
        }
    }
}

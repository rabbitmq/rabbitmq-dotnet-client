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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using System;

namespace RabbitMQ.Client.Unit
{
    internal class TestBasicPublishBatch : IntegrationFixture
    {
        [Test]
        public void TestBasicPublishBatchSend()
        {
            Model.ConfirmSelect();
            Model.QueueDeclare(queue: "test-message-batch-a", durable: false);
            Model.QueueDeclare(queue: "test-message-batch-b", durable: false);
            var batch = Model.CreateBasicPublishBatch();
            batch.Add("", "test-message-batch-a", false, null, new byte [] {});
            batch.Add("", "test-message-batch-b", false, null, new byte [] {});
            batch.Publish();
            Model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            var resultA = Model.BasicGet("test-message-batch-a", true);
            Assert.NotNull(resultA);
            var resultB = Model.BasicGet("test-message-batch-b", true);
            Assert.NotNull(resultB);
        }

        [Test]
        [TestCase("This is my sentence", 0, -1, "This is my sentence")]
        [TestCase("This is the first sentence", 12, 5, "first")]
        [TestCase("This is the second sentence", 12, 6, "second")]
        [TestCase("This is the second sentence", 12, -1, "second sentence")]
        [TestCase("This is the another sentence", 255, -1, "")]
        [TestCase("This is the another sentence!", 0, 255, "This is the another sentence!")]
        public void TestBasicPublishWithBodyBatchSend(string bodyText, int start, int len, string expected)
        {
            Model.ConfirmSelect();
            Model.QueueDeclare(queue: "test-message-batch-a", durable: false);
            var batch = Model.CreateBasicPublishBatch();
            var bodyA = System.Text.Encoding.ASCII.GetBytes(bodyText);
            batch.Add("", "test-message-batch-a", false, null, bodyA, start, len);
            batch.Publish();
            Model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            var resultA = Model.BasicGet("test-message-batch-a", true);
            Assert.NotNull(resultA);
            Assert.AreEqual(expected, System.Text.Encoding.ASCII.GetString(resultA.Body));
        }

    }
}

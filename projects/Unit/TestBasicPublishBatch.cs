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

using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    internal class TestBasicPublishBatch : IntegrationFixture
    {
        [Test]
        public void TestBasicPublishBatchSend()
        {
            _model.ConfirmSelect();
            _model.QueueDeclare(queue: "test-message-batch-a", durable: false);
            _model.QueueDeclare(queue: "test-message-batch-b", durable: false);
            IBasicPublishBatch batch = _model.CreateBasicPublishBatch();
            batch.Add("", "test-message-batch-a", false, null, new ReadOnlyMemory<byte>());
            batch.Add("", "test-message-batch-b", false, null, new ReadOnlyMemory<byte>());
            batch.Publish();
            _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            BasicGetResult resultA = _model.BasicGet("test-message-batch-a", true);
            Assert.NotNull(resultA);
            BasicGetResult resultB = _model.BasicGet("test-message-batch-b", true);
            Assert.NotNull(resultB);
        }

        [Test]
        public void TestBasicPublishBatchSendWithSizeHint()
        {
            _model.ConfirmSelect();
            _model.QueueDeclare(queue: "test-message-batch-a", durable: false);
            _model.QueueDeclare(queue: "test-message-batch-b", durable: false);
            IBasicPublishBatch batch = _model.CreateBasicPublishBatch(2);
            ReadOnlyMemory<byte> bodyAsMemory = new byte [] {};
            batch.Add("", "test-message-batch-a", false, null, bodyAsMemory);
            batch.Add("", "test-message-batch-b", false, null, bodyAsMemory);
            batch.Publish();
            _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            BasicGetResult resultA = _model.BasicGet("test-message-batch-a", true);
            Assert.NotNull(resultA);
            BasicGetResult resultB = _model.BasicGet("test-message-batch-b", true);
            Assert.NotNull(resultB);
        }

        [Test]
        public void TestBasicPublishBatchSendWithWrongSizeHint()
        {
            _model.ConfirmSelect();
            _model.QueueDeclare(queue: "test-message-batch-a", durable: false);
            _model.QueueDeclare(queue: "test-message-batch-b", durable: false);
            IBasicPublishBatch batch = _model.CreateBasicPublishBatch(1);
            ReadOnlyMemory<byte> bodyAsMemory = new byte [] {};
            batch.Add("", "test-message-batch-a", false, null, bodyAsMemory);
            batch.Add("", "test-message-batch-b", false, null, bodyAsMemory);
            batch.Publish();
            _model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            BasicGetResult resultA = _model.BasicGet("test-message-batch-a", true);
            Assert.NotNull(resultA);
            BasicGetResult resultB = _model.BasicGet("test-message-batch-b", true);
            Assert.NotNull(resultB);
        }
    }
}

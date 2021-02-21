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

using NUnit.Framework;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestChannelSoftErrors : IntegrationFixture
    {
        [Test]
        public void TestBindOnNonExistingQueue()
        {
            TestDelegate action = () => _model.QueueBind("NonExistingQueue", "", string.Empty);
            var exception = Assert.Throws<OperationInterruptedException>(action);

            Assert.IsTrue(exception.Message.Contains("403"), "Message doesn't contain the expected 403 part: {0}", exception.Message);
            Assert.IsFalse(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.IsTrue(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }

        [Test]
        public void TestBindOnNonExistingExchange()
        {
            TestDelegate action = () => _model.ExchangeBind("NonExistingQueue", "", string.Empty);
            var exception = Assert.Throws<OperationInterruptedException>(action);

            Assert.IsTrue(exception.Message.Contains("403"), "Message doesn't contain the expected 403 part: {0}", exception.Message);
            Assert.IsFalse(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.IsTrue(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }

        [Test]
        public void TestConsumeOnNonExistingQueue()
        {
            var consumer = new EventingBasicConsumer(_model);
            TestDelegate action = () => _model.BasicConsume("NonExistingQueue", true, consumer);
            var exception = Assert.Throws<OperationInterruptedException>(action);

            Assert.IsTrue(exception.Message.Contains("404"), "Message doesn't contain the expected 404 part: {0}", exception.Message);
            Assert.IsFalse(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.IsTrue(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }
    }
}

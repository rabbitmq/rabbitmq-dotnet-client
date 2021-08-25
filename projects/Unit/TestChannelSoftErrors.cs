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

using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    public class TestChannelSoftErrors : IntegrationFixture
    {
        public TestChannelSoftErrors(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestBindOnNonExistingQueue()
        {
            OperationInterruptedException exception = Assert.Throws<OperationInterruptedException>(() => _model.QueueBind("NonExistingQueue", "", string.Empty));
            Assert.True(exception.Message.Contains("403"), $"Message doesn't contain the expected 403 part: {exception.Message}");
            Assert.False(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.True(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }

        [Fact]
        public void TestBindOnNonExistingExchange()
        {
            OperationInterruptedException exception = Assert.Throws<OperationInterruptedException>(() => _model.ExchangeBind("NonExistingQueue", "", string.Empty));
            Assert.True(exception.Message.Contains("403"), $"Message doesn't contain the expected 403 part: {exception.Message}");
            Assert.False(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.True(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }

        [Fact]
        public void TestConsumeOnNonExistingQueue()
        {
            OperationInterruptedException exception = Assert.Throws<OperationInterruptedException>(() =>
            {
                var consumer = new EventingBasicConsumer(_model); _model.BasicConsume("NonExistingQueue", true, consumer);
            });

            Assert.True(exception.Message.Contains("404"), $"Message doesn't contain the expected 404 part: {exception.Message}");
            Assert.False(_model.IsOpen, "Channel should be closed due to the soft error");
            Assert.True(_conn.IsOpen, "Connection should still be open due to the soft error only closing the channel");
        }
    }
}

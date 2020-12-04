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
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    internal class TestBasicPublishBatch : IntegrationFixture
    {
        [Test]
        public async Task TestBasicPublishBatchSend()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-a", durable: false).ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-b", durable: false).ConfigureAwait(false);
            MessageBatch batch = new MessageBatch();
            batch.Add("", "test-message-batch-a", null, ReadOnlyMemory<byte>.Empty, false);
            batch.Add("", "test-message-batch-b", null, ReadOnlyMemory<byte>.Empty, false);
            await _channel.PublishBatchAsync(batch).ConfigureAwait(false);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            SingleMessageRetrieval resultA = await _channel.RetrieveSingleMessageAsync("test-message-batch-a", true).ConfigureAwait(false);
            Assert.IsFalse(resultA.IsEmpty);
            SingleMessageRetrieval resultB = await _channel.RetrieveSingleMessageAsync("test-message-batch-b", true).ConfigureAwait(false);
            Assert.IsFalse(resultB.IsEmpty);
        }

        [Test]
        public async Task TestBasicPublishBatchSendWithSizeHint()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-a", durable: false).ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-b", durable: false).ConfigureAwait(false);
            MessageBatch batch = new MessageBatch(2);
            ReadOnlyMemory<byte> bodyAsMemory = new byte [] {};
            batch.Add("", "test-message-batch-a", null, bodyAsMemory, false);
            batch.Add("", "test-message-batch-b", null, bodyAsMemory, false);
            await _channel.PublishBatchAsync(batch).ConfigureAwait(false);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            SingleMessageRetrieval resultA = await _channel.RetrieveSingleMessageAsync("test-message-batch-a", true).ConfigureAwait(false);
            Assert.IsFalse(resultA.IsEmpty);
            SingleMessageRetrieval resultB = await _channel.RetrieveSingleMessageAsync("test-message-batch-b", true).ConfigureAwait(false);
            Assert.IsFalse(resultB.IsEmpty);
        }

        [Test]
        public async Task TestBasicPublishBatchSendWithWrongSizeHint()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-a", durable: false).ConfigureAwait(false);
            await _channel.DeclareQueueAsync(queue: "test-message-batch-b", durable: false).ConfigureAwait(false);
            MessageBatch batch = new MessageBatch(1);
            ReadOnlyMemory<byte> bodyAsMemory = new byte [] {};
            batch.Add("", "test-message-batch-a", null, bodyAsMemory, false);
            batch.Add("", "test-message-batch-b", null, bodyAsMemory, false);
            await _channel.PublishBatchAsync(batch).ConfigureAwait(false);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(15));
            SingleMessageRetrieval resultA = await _channel.RetrieveSingleMessageAsync("test-message-batch-a", true).ConfigureAwait(false);
            Assert.IsFalse(resultA.IsEmpty);
            SingleMessageRetrieval resultB = await _channel.RetrieveSingleMessageAsync("test-message-batch-b", true).ConfigureAwait(false);
            Assert.IsFalse(resultB.IsEmpty);
        }
    }
}

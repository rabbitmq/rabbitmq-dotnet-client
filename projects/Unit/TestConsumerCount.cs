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

using System.Threading.Tasks;
using NUnit.Framework;

using RabbitMQ.Client.client.impl.Channel;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    internal class TestConsumerCount : IntegrationFixture
    {
        [Test]
        public async Task TestConsumerCountMethod()
        {
            string q = GenerateQueueName();
            await _channel.DeclareQueueAsync(q, false, true, false).ConfigureAwait(false);
            Assert.AreEqual(0, await _channel.GetQueueConsumerCountAsync(q).ConfigureAwait(false));

            string tag = await _channel.ActivateConsumerAsync(new EventingBasicConsumer(_channel), q, true).ConfigureAwait(false);
            Assert.AreEqual(1, await _channel.GetQueueConsumerCountAsync(q).ConfigureAwait(false));

            await _channel.CancelConsumerAsync(tag).ConfigureAwait(false);
            Assert.AreEqual(0, await _channel.GetQueueConsumerCountAsync(q).ConfigureAwait(false));
        }
    }
}

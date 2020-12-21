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
    [TestFixture]
    public class TestExtensions : IntegrationFixture
    {
        [Test]
        public async Task TestConfirmBarrier()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                await _channel.PublishMessageAsync("", string.Empty, null, new byte[] {}).ConfigureAwait(false);
            }
            Assert.That(_channel.WaitForConfirms(), Is.True);
        }

        [Test]
        public void TestConfirmBeforeWait()
        {
            Assert.Throws(typeof (InvalidOperationException), () => _channel.WaitForConfirms());
        }

        [Test]
        public async Task TestExchangeBinding()
        {
            await _channel.ActivatePublishTagsAsync().ConfigureAwait(false);

            await _channel.DeclareExchangeAsync("src", ExchangeType.Direct, false, false).ConfigureAwait(false);
            await _channel.DeclareExchangeAsync("dest", ExchangeType.Direct, false, false).ConfigureAwait(false);
            (string queue, _, _) = await _channel.DeclareQueueAsync().ConfigureAwait(false);

            await _channel.BindExchangeAsync("dest", "src", string.Empty).ConfigureAwait(false);
            await _channel.BindExchangeAsync("dest", "src", string.Empty).ConfigureAwait(false);
            await _channel.BindQueueAsync(queue, "dest", string.Empty).ConfigureAwait(false);

            await _channel.PublishMessageAsync("src", string.Empty, null, new byte[] {}).ConfigureAwait(false);
            _channel.WaitForConfirms();
            var singleMessage = await _channel.RetrieveSingleMessageAsync(queue, true).ConfigureAwait(false);
            Assert.IsFalse(singleMessage.IsEmpty);

            await _channel.UnbindExchangeAsync("dest", "src", string.Empty).ConfigureAwait(false);
            await _channel.PublishMessageAsync("src", string.Empty, null, new byte[] {}).ConfigureAwait(false);
            _channel.WaitForConfirms();
            singleMessage = await _channel.RetrieveSingleMessageAsync(queue, true).ConfigureAwait(false);
            Assert.IsTrue(singleMessage.IsEmpty);

            await _channel.DeleteExchangeAsync("src").ConfigureAwait(false);
            await _channel.DeleteExchangeAsync("dest").ConfigureAwait(false);
        }
    }
}

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

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestNoWait : IntegrationFixture
    {
        [Test]
        public async Task TestQueueDeclareNoWait()
        {
            string q = GenerateQueueName();
            await _channel.DeclareQueueWithoutConfirmationAsync(q, false, true, false).ConfigureAwait(false);
            await _channel.DeclareQueuePassiveAsync(q).ConfigureAwait(false);
        }

        [Test]
        public async Task TestQueueBindNoWait()
        {
            string q = GenerateQueueName();
            await _channel.DeclareQueueWithoutConfirmationAsync(q, false, true, false).ConfigureAwait(false);
            await _channel.BindQueueAsync(q, "amq.fanout", "", waitForConfirmation:false).ConfigureAwait(false);
        }

        [Test]
        public async Task TestQueueDeleteNoWait()
        {
            string q = GenerateQueueName();
            await _channel.DeclareQueueWithoutConfirmationAsync(q, false, true, false).ConfigureAwait(false);
            await _channel.DeleteQueueWithoutConfirmationAsync(q).ConfigureAwait(false);
        }

        [Test]
        public async Task TestExchangeDeclareNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                await _channel.DeclareExchangeAsync(x, "fanout", false, true, waitForConfirmation:false).ConfigureAwait(false);
                await _channel.DeclareExchangePassiveAsync(x).ConfigureAwait(false);
            }
            finally
            {
                await _channel.DeleteExchangeAsync(x).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task TestExchangeBindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                await _channel.DeclareExchangeAsync(x, "fanout", false, true, waitForConfirmation:false).ConfigureAwait(false);
                await _channel.BindExchangeAsync(x, "amq.fanout", "", waitForConfirmation:false).ConfigureAwait(false);
            }
            finally
            {
                await _channel.DeleteExchangeAsync(x).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task TestExchangeUnbindNoWait()
        {
            string x = GenerateExchangeName();
            try
            {
                await _channel.DeclareExchangeAsync(x, "fanout", false, true).ConfigureAwait(false);
                await _channel.BindExchangeAsync(x, "amq.fanout", "").ConfigureAwait(false);
                await _channel.UnbindExchangeAsync(x, "amq.fanout", "", waitForConfirmation:false).ConfigureAwait(false);
            }
            finally
            {
                await _channel.DeleteExchangeAsync(x);
            }
        }

        [Test]
        public async Task TestExchangeDeleteNoWait()
        {
            string x = GenerateExchangeName();
            await _channel.DeclareExchangeAsync(x, "fanout", false, true, waitForConfirmation:false).ConfigureAwait(false);
            await _channel.DeleteExchangeAsync(x, waitForConfirmation:false).ConfigureAwait(false);
        }
    }
}

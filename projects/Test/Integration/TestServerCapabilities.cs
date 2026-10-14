// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestServerCapabilities : IntegrationFixture
    {
        public TestServerCapabilities(ITestOutputHelper output) : base(output)
        {
        }

        [SkippableFact]
        public async Task TestConsumerCancelOkCapabilityReachesTheSession_GH2051()
        {
            await using IChannel ch = await _conn.CreateChannelAsync();
            ISession session = ((AutorecoveringChannel)ch).InnerChannel.Session;

            // What the broker sent, decoded independently of the flag under test.
            bool advertised = Connection.ServerHasCapability(
                session.Connection.ServerProperties, Connection.AcceptConsumerCancelOkCapability);

            /*
             * Skipped rather than asserted when the broker is silent, because `false == false`
             * passes however broken the plumbing is: a version of this test without the skip stayed
             * green with SessionBase's forwarding replaced by `false`, which turns the whole
             * feature into a no-op. `accept_consumer_cancel_ok` is on the broker's main branch and
             * absent from v4.3.6, so this starts asserting when CI moves to a broker that has it.
             */
            Skip.IfNot(advertised,
                $"broker does not advertise {Connection.AcceptConsumerCancelOkCapability}");

            // The handshake assignment, then SessionBase forwarding to its connection. Neither hop
            // is reachable from a unit test: the fake session throws from Connection.
            Assert.True(session.Connection.ServerAcceptsConsumerCancelOk);
            Assert.True(session.ServerAcceptsConsumerCancelOk);
        }
    }
}

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
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration
{
    public class TestConnectionFactory : AsyncIntegrationFixture
    {
        public TestConnectionFactory(ITestOutputHelper output) : base(output)
        {
        }

        protected override void SetUp()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
        }

        [Fact]
        public async Task TestCreateConnectionAsync_WithAlreadyCanceledToken()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            ConnectionFactory cf = CreateConnectionFactory();

            bool passed = false;
            /*
             * If anyone wonders why TaskCanceledException is explicitly checked,
             * even though it's a subclass of OperationCanceledException:
             * https://github.com/rabbitmq/rabbitmq-dotnet-client/commit/383ca5c5f161edb717cf8fae7bf143c13143f634#r135400615
             */
            try
            {
                await cf.CreateConnectionAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                passed = true;
            }
            catch (OperationCanceledException)
            {
                passed = true;
            }

            Assert.True(passed, "FAIL did not see TaskCanceledException nor OperationCanceledException");
        }

        [Fact]
        public async Task TestCreateConnectionAsync_UsesValidEndpointWhenMultipleSupplied()
        {
            using var cts = new CancellationTokenSource(WaitSpan);
            ConnectionFactory cf = CreateConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { invalidEp, ep }, cts.Token);
        }
    }
}

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

using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.AsyncIntegration
{
    public class AsyncIntegrationFixture : IntegrationFixtureBase, IAsyncLifetime
    {
        protected readonly bool _dispatchConsumersAsync = false;
        protected readonly ushort _consumerDispatchConcurrency = 1;
        protected readonly bool _openChannel = true;

        public AsyncIntegrationFixture(ITestOutputHelper output,
            bool dispatchConsumersAsync = false,
            ushort consumerDispatchConcurrency = 1,
            bool openChannel = true) : base(output)
        {
            _dispatchConsumersAsync = dispatchConsumersAsync;
            _consumerDispatchConcurrency = consumerDispatchConcurrency;
            _openChannel = openChannel;
        }

        protected static Task AssertRanToCompletion(params Task[] tasks)
        {
            return DoAssertRanToCompletion(tasks);
        }

        protected static Task AssertRanToCompletion(IEnumerable<Task> tasks)
        {
            return DoAssertRanToCompletion(tasks);
        }

        public override async Task InitializeAsync()
        {
            _connFactory = CreateConnectionFactory();
            _connFactory.DispatchConsumersAsync = _dispatchConsumersAsync;
            _connFactory.ConsumerDispatchConcurrency = _consumerDispatchConcurrency;

            _conn = await _connFactory.CreateConnectionAsync();
            if (_connFactory.AutomaticRecoveryEnabled)
            {
                Assert.IsType<RabbitMQ.Client.Framing.Impl.AutorecoveringConnection>(_conn);
            }
            else
            {
                Assert.IsType<RabbitMQ.Client.Framing.Impl.Connection>(_conn);
            }

            if (_openChannel)
            {
                _channel = await _conn.CreateChannelAsync();
            }

            base.AddCallbackHandlers();
        }

        private static async Task DoAssertRanToCompletion(IEnumerable<Task> tasks)
        {
            Task whenAllTask = Task.WhenAll(tasks);
            await whenAllTask;
            Assert.True(whenAllTask.IsCompletedSuccessfully());
        }
    }
}

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


using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConnectionTracingOptionsCopy : IntegrationFixture
    {
        public TestConnectionTracingOptionsCopy(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ChangingTheFactorysTracingOptionsDoesNotReachAnOpenChannel_GH2152()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.TracingOptions = new ConnectionTracingOptions { UsePublisherAsParent = false };
            await using IConnection conn = await cf.CreateConnectionAsync();
            await using IChannel ch = await conn.CreateChannelAsync();

            cf.TracingOptions.UsePublisherAsParent = true;

            ConnectionTracingOptions channelOptions = ((AutorecoveringChannel)ch).InnerChannel.TracingOptions!;
            Assert.NotSame(cf.TracingOptions, channelOptions);
            Assert.False(channelOptions.UsePublisherAsParent);
        }
    }
}

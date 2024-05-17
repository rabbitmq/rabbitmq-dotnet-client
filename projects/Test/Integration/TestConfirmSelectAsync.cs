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
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConfirmSelectAsync : IntegrationFixture
    {
        readonly byte[] _message = GetRandomBody(64);

        public TestConfirmSelectAsync(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestConfirmSelectIdempotency()
        {
            await _channel.ConfirmSelectAsync();
            Assert.Equal(1ul, _channel.NextPublishSeqNo);
            await Publish();
            Assert.Equal(2ul, _channel.NextPublishSeqNo);
            await Publish();
            Assert.Equal(3ul, _channel.NextPublishSeqNo);

            await _channel.ConfirmSelectAsync();
            await Publish();
            Assert.Equal(4ul, _channel.NextPublishSeqNo);
            await Publish();
            Assert.Equal(5ul, _channel.NextPublishSeqNo);
            await Publish();
            Assert.Equal(6ul, _channel.NextPublishSeqNo);
        }

        private ValueTask Publish()
        {
            return _channel.BasicPublishAsync(exchange: ExchangeName.Empty,
                routingKey: Guid.NewGuid().ToString(), _message);
        }
    }
}

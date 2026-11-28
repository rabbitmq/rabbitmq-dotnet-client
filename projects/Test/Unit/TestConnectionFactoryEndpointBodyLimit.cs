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


using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestConnectionFactoryEndpointBodyLimit
    {
        private const uint OneMiB = 1_048_576;
        private const uint Default = ConnectionFactory.DefaultMaxInboundMessageBodySize;

        public static TheoryData<string, uint, uint> Cases => new TheoryData<string, uint, uint>
        {
            // An endpoint built without a limit carries the default, which must not replace the factory's.
            { "no limit", OneMiB, OneMiB },
            { "no limit, cloned", OneMiB, OneMiB },
            { "no limit, cloned with hostname", OneMiB, OneMiB },
            { "no limit, parsed", OneMiB, OneMiB },
            { "no limit", 100 * OneMiB, 100 * OneMiB },
            // A zero factory limit rejects every non-empty body, so even the default replaces it.
            { "no limit", 0, Default },
            // A limit passed to the constructor replaces the factory's, lower or higher, as in 7.3.0.
            { "1 MiB", Default, OneMiB },
            { "100 MiB", OneMiB, 100 * OneMiB },
            { "100 MiB, cloned", OneMiB, 100 * OneMiB },
            { "100 MiB, cloned with hostname", OneMiB, 100 * OneMiB },
            { "64 MiB", OneMiB, Default },
            { "100 MiB, from another factory", OneMiB, 100 * OneMiB },
            { "from a default factory", OneMiB, Default },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void AssigningAnEndpointReplacesTheBodyLimitOnlyWhenTheEndpointSetIt_GH2129(string endpoint, uint factoryLimit, uint expected)
        {
            var cf = new ConnectionFactory { MaxInboundMessageBodySize = factoryLimit };

            cf.Endpoint = endpoint switch
            {
                "no limit" => new AmqpTcpEndpoint("broker"),
                "no limit, cloned" => (AmqpTcpEndpoint)new AmqpTcpEndpoint("broker").Clone(),
                "no limit, cloned with hostname" => new AmqpTcpEndpoint("broker").CloneWithHostname("other"),
                "no limit, parsed" => AmqpTcpEndpoint.Parse("broker:5672"),
                "1 MiB" => new AmqpTcpEndpoint("broker", -1, new SslOption(), OneMiB),
                "100 MiB" => new AmqpTcpEndpoint("broker", -1, new SslOption(), 100 * OneMiB),
                "100 MiB, cloned" => (AmqpTcpEndpoint)new AmqpTcpEndpoint("broker", -1, new SslOption(), 100 * OneMiB).Clone(),
                "100 MiB, cloned with hostname" => new AmqpTcpEndpoint("broker", -1, new SslOption(), 100 * OneMiB).CloneWithHostname("other"),
                "64 MiB" => new AmqpTcpEndpoint("broker", -1, new SslOption(), Default),
                "100 MiB, from another factory" => new ConnectionFactory { MaxInboundMessageBodySize = 100 * OneMiB }.Endpoint,
                "from a default factory" => new ConnectionFactory().Endpoint,
                _ => throw new System.ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
            };

            Assert.Equal(expected, cf.MaxInboundMessageBodySize);
            Assert.Equal(expected, cf.Endpoint.MaxInboundMessageBodySize);
        }
    }
}

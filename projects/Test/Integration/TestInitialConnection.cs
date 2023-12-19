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
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestInitialConnection : IntegrationFixture
    {
        public TestInitialConnection(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameList()
        {
            var c = CreateAutorecoveringConnection(new List<string>() { "127.0.0.1", "localhost" });
            Assert.True(c.IsOpen);
            c.Close();
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameListAndUnreachableHosts()
        {
            var c = CreateAutorecoveringConnection(new List<string>() { "191.72.44.22", "127.0.0.1", "localhost" });
            Assert.True(c.IsOpen);
            c.Close();
        }

        [Fact]
        public void TestBasicConnectionRecoveryWithHostnameListWithOnlyUnreachableHosts()
        {
            Assert.Throws<BrokerUnreachableException>(() =>
            {
                CreateAutorecoveringConnection(new List<string>() {
                    "191.72.44.22",
                    "145.23.22.18",
                    "192.255.255.255"
                }, expectException: true);
            });
        }
    }
}

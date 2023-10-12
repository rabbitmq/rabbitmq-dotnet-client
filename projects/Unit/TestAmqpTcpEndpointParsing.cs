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
using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestAmqpTcpEndpointParsing
    {
        [Fact]
        public void TestHostWithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("host:1234");

            Assert.Equal("host", e.HostName);
            Assert.Equal(1234, e.Port);
        }

        [Fact]
        public void TestHostWithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("host");

            Assert.Equal("host", e.HostName);
            Assert.Equal(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Fact]
        public void TestEmptyHostWithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse(":1234");

            Assert.Equal("", e.HostName);
            Assert.Equal(1234, e.Port);
        }

        [Fact]
        public void TestEmptyHostWithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse(":");

            Assert.Equal("", e.HostName);
            Assert.Equal(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Fact]
        public void TestCompletelyEmptyString()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("");

            Assert.Equal("", e.HostName);
            Assert.Equal(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Fact]
        public void TestInvalidPort()
        {
            Assert.Throws<FormatException>(() => AmqpTcpEndpoint.Parse("host:port"));
        }

        [Fact]
        public void TestMultipleNone()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple("  ");
            Assert.Empty(es);
        }

        [Fact]
        public void TestMultipleOne()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(" host:1234 ");
            Assert.Single(es);
            Assert.Equal("host", es[0].HostName);
            Assert.Equal(1234, es[0].Port);
        }

        [Fact]
        public void TestMultipleTwo()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(" host:1234, other:2345 ");
            Assert.Equal(2, es.Length);
            Assert.Equal("host", es[0].HostName);
            Assert.Equal(1234, es[0].Port);
            Assert.Equal("other", es[1].HostName);
            Assert.Equal(2345, es[1].Port);
        }

        [Fact]
        public void TestMultipleTwoMultipleCommas()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(", host:1234,, ,,, other:2345,, ");
            Assert.Equal(2, es.Length);
            Assert.Equal("host", es[0].HostName);
            Assert.Equal(1234, es[0].Port);
            Assert.Equal("other", es[1].HostName);
            Assert.Equal(2345, es[1].Port);
        }

        [Fact]
        public void TestIpv6WithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("[::1]:1234");

            Assert.Equal("::1", e.HostName);
            Assert.Equal(1234, e.Port);
        }

        [Fact]
        public void TestIpv6WithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("[::1]");

            Assert.Equal("::1", e.HostName);
            Assert.Equal(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }
    }
}

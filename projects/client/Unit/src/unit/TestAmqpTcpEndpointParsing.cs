// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at http://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using System;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestAmqpTcpEndpointParsing
    {
        [Test]
        public void TestHostWithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("host:1234");

            Assert.AreEqual("host", e.HostName);
            Assert.AreEqual(1234, e.Port);
        }

        [Test]
        public void TestHostWithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("host");

            Assert.AreEqual("host", e.HostName);
            Assert.AreEqual(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Test]
        public void TestEmptyHostWithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse(":1234");

            Assert.AreEqual("", e.HostName);
            Assert.AreEqual(1234, e.Port);
        }

        [Test]
        public void TestEmptyHostWithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse(":");

            Assert.AreEqual("", e.HostName);
            Assert.AreEqual(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Test]
        public void TestCompletelyEmptyString()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("");

            Assert.AreEqual("", e.HostName);
            Assert.AreEqual(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }

        [Test]
        public void TestInvalidPort()
        {
            try
            {
                AmqpTcpEndpoint.Parse("host:port");
                Assert.Fail("Expected FormatException");
            }
            catch (FormatException)
            {
                // OK.
            }
        }

        [Test]
        public void TestMultipleNone()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple("  ");
            Assert.AreEqual(0, es.Length);
        }

        [Test]
        public void TestMultipleOne()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(" host:1234 ");
            Assert.AreEqual(1, es.Length);
            Assert.AreEqual("host", es[0].HostName);
            Assert.AreEqual(1234, es[0].Port);
        }

        [Test]
        public void TestMultipleTwo()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(" host:1234, other:2345 ");
            Assert.AreEqual(2, es.Length);
            Assert.AreEqual("host", es[0].HostName);
            Assert.AreEqual(1234, es[0].Port);
            Assert.AreEqual("other", es[1].HostName);
            Assert.AreEqual(2345, es[1].Port);
        }

        [Test]
        public void TestMultipleTwoMultipleCommas()
        {
            AmqpTcpEndpoint[] es = AmqpTcpEndpoint.ParseMultiple(", host:1234,, ,,, other:2345,, ");
            Assert.AreEqual(2, es.Length);
            Assert.AreEqual("host", es[0].HostName);
            Assert.AreEqual(1234, es[0].Port);
            Assert.AreEqual("other", es[1].HostName);
            Assert.AreEqual(2345, es[1].Port);
        }

        [Test]
        public void TestIpv6WithPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("[::1]:1234");

            Assert.AreEqual("::1", e.HostName);
            Assert.AreEqual(1234, e.Port);
        }

        [Test]
        public void TestIpv6WithoutPort()
        {
            AmqpTcpEndpoint e = AmqpTcpEndpoint.Parse("[::1]");

            Assert.AreEqual("::1", e.HostName);
            Assert.AreEqual(Protocols.DefaultProtocol.DefaultPort, e.Port);
        }
    }
}

// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2015 Pivotal Software, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2011-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestAmqpUri
    {
        private readonly string[] IPv6Loopbacks = { "[0000:0000:0000:0000:0000:0000:0000:0001]", "[::1]" };

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void TestAmqpUriParseFail()
        {
            /* Various failure cases */
            ParseFail("http://www.rabbitmq.com");
            ParseFail("amqp://foo:bar:baz");
            ParseFail("amqp://foo[::1]");
            ParseFail("amqp://foo:[::1]");
            ParseFail("amqp://foo:1000xyz");
            ParseFail("amqp://foo:1000000");
            ParseFail("amqp://foo/bar/baz");

            ParseFail("amqp://foo%1");
            ParseFail("amqp://foo%1x");
            ParseFail("amqp://foo%xy");
        }

        [Test]
        public void TestAmqpUriParseSucceed()
        {
            /* From the spec */
            ParseSuccess("amqp://user:pass@host:10000/vhost",
                "user", "pass", "host", 10000, "vhost");
            ParseSuccess("aMQps://user%61:%61pass@host:10000/v%2fhost",
                "usera", "apass", "host", 10000, "v/host");
            ParseSuccess("amqp://localhost", "guest", "guest", "localhost", 5672, "/");
            ParseSuccess("amqp://:@localhost/", "", "", "localhost", 5672, "/");
            ParseSuccess("amqp://user@localhost",
                "user", "guest", "localhost", 5672, "/");
            ParseSuccess("amqp://user:pass@localhost",
                "user", "pass", "localhost", 5672, "/");
            ParseSuccess("amqp://host", "guest", "guest", "host", 5672, "/");
            ParseSuccess("amqp://localhost:10000",
                "guest", "guest", "localhost", 10000, "/");
            ParseSuccess("amqp://localhost/vhost",
                "guest", "guest", "localhost", 5672, "vhost");
            ParseSuccess("amqp://host/", "guest", "guest", "host", 5672, "/");
            ParseSuccess("amqp://host/%2f",
                "guest", "guest", "host", 5672, "/");
            ParseSuccess("amqp://[::1]", "guest", "guest",
                IPv6Loopbacks,
                5672, "/");

            /* Various other success cases */
            ParseSuccess("amqp://host:100",
                "guest", "guest", "host", 100, "/");
            ParseSuccess("amqp://[::1]:100",
                "guest", "guest",
                IPv6Loopbacks,
                100, "/");

            ParseSuccess("amqp://host/blah",
                "guest", "guest", "host", 5672, "blah");
            ParseSuccess("amqp://host:100/blah",
                "guest", "guest", "host", 100, "blah");
            ParseSuccess("amqp://localhost:100/blah",
                "guest", "guest", "localhost", 100, "blah");
            ParseSuccess("amqp://[::1]/blah",
                "guest", "guest",
                IPv6Loopbacks,
                5672, "blah");
            ParseSuccess("amqp://[::1]:100/blah",
                "guest", "guest",
                IPv6Loopbacks,
                100, "blah");

            ParseSuccess("amqp://user:pass@host",
                "user", "pass", "host", 5672, "/");
            ParseSuccess("amqp://user:pass@host:100",
                "user", "pass", "host", 100, "/");
            ParseSuccess("amqp://user:pass@localhost:100",
                "user", "pass", "localhost", 100, "/");
            ParseSuccess("amqp://user:pass@[::1]",
                "user", "pass",
                IPv6Loopbacks,
                5672, "/");
            ParseSuccess("amqp://user:pass@[::1]:100",
                "user", "pass",
                IPv6Loopbacks,
                100, "/");
        }

        private static void AssertUriPartEquivalence(string user, string password, int port, string vhost, ConnectionFactory cf)
        {
            Assert.AreEqual(user, cf.UserName);
            Assert.AreEqual(password, cf.Password);
            Assert.AreEqual(port, cf.Port);
            Assert.AreEqual(vhost, cf.VirtualHost);
        }

        private void ParseFail(string uri)
        {
            var cf = new ConnectionFactory();
            cf.Uri = uri;
        }

        private void ParseSuccess(string uri, string user, string password, string host, int port, string vhost)
        {
            var factory = new ConnectionFactory { Uri = uri };
            AssertUriPartEquivalence(user, password, port, vhost, factory);
            Assert.AreEqual(host, factory.HostName);
        }

        private void ParseSuccess(string uri, string user, string password,
            string[] hosts, int port, string vhost)
        {
            var factory = new ConnectionFactory { Uri = uri };
            AssertUriPartEquivalence(user, password, port, vhost, factory);
            Assert.IsTrue((Array.IndexOf(hosts, factory.HostName)) != -1);
        }
    }
}

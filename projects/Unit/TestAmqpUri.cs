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

using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestAmqpUri
    {
        private readonly string[] _iPv6Loopbacks = { "[0000:0000:0000:0000:0000:0000:0000:0001]", "[::1]" };

        [Fact]
        [Trait("Category", "MonoBug")]
        public void TestAmqpUriParseFail()
        {
            if (IsRunningOnMono() == false)
            {
                /* Various failure cases */
                ParseFailWith<ArgumentException>("https://www.rabbitmq.com");
                ParseFailWith<UriFormatException>("amqp://foo:bar:baz");
                ParseFailWith<UriFormatException>("amqp://foo[::1]");
                ParseFailWith<UriFormatException>("amqp://foo:[::1]");
                ParseFailWith<UriFormatException>("amqp://foo:1000xyz");
                ParseFailWith<UriFormatException>("amqp://foo:1000000");
                ParseFailWith<ArgumentException>("amqp://foo/bar/baz");

                ParseFailWith<UriFormatException>("amqp://foo%1");
                ParseFailWith<UriFormatException>("amqp://foo%1x");
                ParseFailWith<UriFormatException>("amqp://foo%xy");

                ParseFailWith<UriFormatException>("amqps://foo%1");
                ParseFailWith<UriFormatException>("amqps://foo%1x");
                ParseFailWith<UriFormatException>("AMQPS://foo%xy");
            }
        }

        [Fact]
        [Trait("Category", "MonoBug")]
        public void TestAmqpUriParseSucceed()
        {
            if (IsRunningOnMono() == false)
            {
                /* From the spec */
                ParseSuccess("amqp://user:pass@host:10000/vhost",
                    "user", "pass", "host", 10000, "vhost", false);
                ParseSuccess("amqps://user:pass@host:10000/vhost",
                    "user", "pass", "host", 10000, "vhost", true);
                ParseSuccess("aMQps://user%61:%61pass@host:10000/v%2fhost",
                    "usera", "apass", "host", 10000, "v/host", true);
                ParseSuccess("amqp://localhost", "guest", "guest", "localhost", 5672, "/");
                ParseSuccess("amqp://:@localhost/", "", "", "localhost", 5672, "/");
                ParseSuccess("amqp://user@localhost",
                    "user", "guest", "localhost", 5672, "/");
                ParseSuccess("amqps://user@localhost",
                    "user", "guest", "localhost", 5671, "/", true);
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
                    _iPv6Loopbacks,
                    5672, "/", false);
                ParseSuccess("AMQPS://[::1]", "guest", "guest",
                    _iPv6Loopbacks,
                    5671, "/", true);
                ParseSuccess("AMQPS://[::1]", "guest", "guest",
                    _iPv6Loopbacks,
                    5671, "/", true);

                /* Various other success cases */
                ParseSuccess("amqp://host:100",
                    "guest", "guest", "host", 100, "/");
                ParseSuccess("amqp://[::1]:100",
                    "guest", "guest",
                    _iPv6Loopbacks,
                    100, "/");

                ParseSuccess("amqp://host/blah",
                    "guest", "guest", "host", 5672, "blah");
                ParseSuccess("amqp://host:100/blah",
                    "guest", "guest", "host", 100, "blah");
                ParseSuccess("amqp://localhost:100/blah",
                    "guest", "guest", "localhost", 100, "blah");
                ParseSuccess("amqp://[::1]/blah",
                    "guest", "guest",
                    _iPv6Loopbacks,
                    5672, "blah");
                ParseSuccess("amqp://[::1]:100/blah",
                    "guest", "guest",
                    _iPv6Loopbacks,
                    100, "blah");

                ParseSuccess("amqp://user:pass@host",
                    "user", "pass", "host", 5672, "/");
                ParseSuccess("amqp://user:pass@host:100",
                    "user", "pass", "host", 100, "/");
                ParseSuccess("amqp://user:pass@localhost:100",
                    "user", "pass", "localhost", 100, "/");
                ParseSuccess("amqp://user:pass@[::1]",
                    "user", "pass",
                    _iPv6Loopbacks,
                    5672, "/");
                ParseSuccess("amqp://user:pass@[::1]:100",
                    "user", "pass",
                    _iPv6Loopbacks,
                    100, "/");

                ParseSuccess("amqps://user:pass@host",
                    "user", "pass", "host", 5671, "/", true);
                ParseSuccess("amqps://user:pass@host:100",
                    "user", "pass", "host", 100, "/", true);
                ParseSuccess("amqps://user:pass@localhost:100",
                    "user", "pass", "localhost", 100, "/", true);
                ParseSuccess("amqps://user:pass@[::1]",
                    "user", "pass",
                    _iPv6Loopbacks,
                    5671, "/", true);
                ParseSuccess("amqps://user:pass@[::1]:100",
                    "user", "pass",
                    _iPv6Loopbacks,
                    100, "/", true);
            }
        }

        private static void AssertUriPartEquivalence(ConnectionFactory cf, string user, string password, int port, string vhost, bool tlsEnabled = false)
        {
            Assert.Equal(user, cf.UserName);
            Assert.Equal(password, cf.Password);
            Assert.Equal(port, cf.Port);
            Assert.Equal(vhost, cf.VirtualHost);
            Assert.Equal(tlsEnabled, cf.Ssl.Enabled);

            Assert.Equal(port, cf.Endpoint.Port);
            Assert.Equal(tlsEnabled, cf.Endpoint.Ssl.Enabled);
        }

        private void ParseFailWith<T>(string uri) where T : Exception
        {
            var cf = new ConnectionFactory();
            Assert.Throws<T>(() => cf.Uri = new Uri(uri));
        }

        private void ParseSuccess(string uri, string user, string password, string host, int port, string vhost, bool tlsEnabled = false)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri)
            };
            AssertUriPartEquivalence(factory, user, password, port, vhost, tlsEnabled);
            Assert.Equal(host, factory.HostName);
        }

        private void ParseSuccess(string uri, string user, string password,
            string[] hosts, int port, string vhost, bool tlsEnabled = false)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri)
            };
            AssertUriPartEquivalence(factory, user, password, port, vhost, tlsEnabled);
            Assert.True(Array.IndexOf(hosts, factory.HostName) != -1);
        }

        public static bool IsRunningOnMono()
        {
#if NETCOREAPP
            return false;
#else
            return Type.GetType("Mono.Runtime") != null;
#endif
        }
    }
}

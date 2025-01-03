// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client;
using Xunit;

namespace Test.Unit
{
    public class TestAmqpUri
    {
        private static readonly string[] s_iPv6Loopbacks = { "[0000:0000:0000:0000:0000:0000:0000:0001]", "[::1]" };

        [Fact]
        public void TestAmqpUriParseFail()
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

        [Fact]
        public void TestAmqpUriParseSucceed()
        {
            /* From the spec */
            Uri amqpUserPassAtHostPort10000VhostUri = new Uri("amqp://user:pass@host:10000/vhost");
            Uri actualUri = ParseSuccess(amqpUserPassAtHostPort10000VhostUri.ToString(),
                "user", "pass", "host", 10000, "vhost", false);
            Assert.Equal(amqpUserPassAtHostPort10000VhostUri, actualUri);

            Uri amqpsUserPassAtHostPort10000VhostUri = new Uri("amqps://user:pass@host:10000/vhost");
            actualUri = ParseSuccess(amqpsUserPassAtHostPort10000VhostUri.ToString(),
                "user", "pass", "host", 10000, "vhost", true);
            Assert.Equal(amqpsUserPassAtHostPort10000VhostUri, actualUri);

            const string amqpsUserPassSpecialCharsAtHostUriStr = "aMQps://user%61:%61pass@host:10000/v%2Fhost";
            Uri amqpsUserPassSpecialCharsAtHostUri = new Uri(amqpsUserPassSpecialCharsAtHostUriStr);
            actualUri = ParseSuccess(amqpsUserPassSpecialCharsAtHostUriStr,
                "usera", "apass", "host", 10000, "v/host", true);
            Assert.Equal(amqpsUserPassSpecialCharsAtHostUri, actualUri);

            Uri amqpGuestAtLocalhostRootVhostUri = new Uri("amqp://guest:guest@localhost:5672/%2F");
            actualUri = ParseSuccess("amqp://localhost",
                "guest", "guest", "localhost", 5672, "/");
            Assert.Equal(amqpGuestAtLocalhostRootVhostUri, actualUri);

            actualUri = ParseSuccess("amqp://:@localhost/",
                "", "", "localhost", 5672, "/");
            Assert.Equal(amqpGuestAtLocalhostRootVhostUri, actualUri);

            actualUri = ParseSuccess("amqp://user@localhost",
                "user", "guest", "localhost", 5672, "/");
            Assert.Equal(amqpGuestAtLocalhostRootVhostUri, actualUri);

            Uri amqpsGuestAtLocalhostRootVhostUri = new Uri("amqps://guest:guest@localhost:5671/%2F");
            actualUri = ParseSuccess("amqps://user@localhost",
                "user", "guest", "localhost", 5671, "/", true);
            Assert.Equal(amqpsGuestAtLocalhostRootVhostUri, actualUri);

            Uri amqpUserPassAtLocalhostRootVhostUri = new Uri("amqp://user:pass@localhost:5672/%2F");
            actualUri = ParseSuccess("amqp://user:pass@localhost",
                "user", "pass", "localhost", 5672, "/");
            Assert.Equal(amqpUserPassAtLocalhostRootVhostUri, actualUri);

            Uri amqpGuestAtHostRootVhostUri = new Uri("amqp://guest:guest@host:5672/%2F");
            actualUri = ParseSuccess("amqp://host",
                "guest", "guest", "host", 5672, "/");
            Assert.Equal(amqpGuestAtHostRootVhostUri, actualUri);

            Uri amqpGuestAtLocalhostPort10000Uri = new Uri("amqp://guest:guest@localhost:10000/%2F");
            actualUri = ParseSuccess("amqp://localhost:10000",
                "guest", "guest", "localhost", 10000, "/");
            Assert.Equal(amqpGuestAtLocalhostPort10000Uri, actualUri);

            Uri amqpGuestAtLocalhostVhostUri = new Uri("amqp://guest:guest@localhost:5672/vhost");
            actualUri = ParseSuccess("amqp://localhost/vhost",
                "guest", "guest", "localhost", 5672, "vhost");
            Assert.Equal(amqpGuestAtLocalhostVhostUri, actualUri);

            actualUri = ParseSuccess("amqp://host/",
                "guest", "guest", "host", 5672, "/");
            Assert.Equal(amqpGuestAtHostRootVhostUri, actualUri);

            actualUri = ParseSuccess("amqp://host/%2f",
                "guest", "guest", "host", 5672, "/");
            Assert.Equal(amqpGuestAtHostRootVhostUri, actualUri);

            Uri amqpGuestAtLocalhost6RootVhostUri = new Uri("amqp://guest:guest@[::1]:5672/%2F");
            actualUri = ParseSuccess("amqp://[::1]",
                "guest", "guest", s_iPv6Loopbacks, 5672, "/", false);
            Assert.Equal(amqpGuestAtLocalhost6RootVhostUri, actualUri);

            Uri amqpsGuestAtLocalhost6RootVhostUri = new Uri("amqps://guest:guest@[::1]:5671/%2F");
            actualUri = ParseSuccess("AMQPS://[::1]",
                "guest", "guest", s_iPv6Loopbacks, 5671, "/", true);
            Assert.Equal(amqpsGuestAtLocalhost6RootVhostUri, actualUri);

            actualUri = ParseSuccess("AMQPS://[::1]",
                "guest", "guest", s_iPv6Loopbacks, 5671, "/", true);
            Assert.Equal(amqpsGuestAtLocalhost6RootVhostUri, actualUri);

            /* Various other success cases */
            Uri amqpGuestAtHostPort100RootVhostUri = new Uri("amqp://guest:guest@host:100/%2F");
            actualUri = ParseSuccess("amqp://host:100",
                "guest", "guest", "host", 100, "/");
            Assert.Equal(amqpGuestAtHostPort100RootVhostUri, actualUri);

            Uri amqpGuestAtLocalhost6Port100RootVhostUri = new Uri("amqp://guest:guest@[::1]:100/%2F");
            actualUri = ParseSuccess("amqp://[::1]:100",
                "guest", "guest", s_iPv6Loopbacks, 100, "/");
            Assert.Equal(amqpGuestAtLocalhost6Port100RootVhostUri, actualUri);

            Uri amqpGuestAtHostBlahVhostUri = new Uri("amqp://guest:guest@host:5672/blah");
            actualUri = ParseSuccess("amqp://host/blah",
                "guest", "guest", "host", 5672, "blah");
            Assert.Equal(amqpGuestAtHostBlahVhostUri, actualUri);

            Uri amqpGuestAtHostPort100BlahVhostUri = new Uri("amqp://guest:guest@host:100/blah");
            actualUri = ParseSuccess("amqp://host:100/blah",
                "guest", "guest", "host", 100, "blah");
            Assert.Equal(amqpGuestAtHostPort100BlahVhostUri, actualUri);

            Uri amqpGuestAtLocalhostPort100BlahVhostUri = new Uri("amqp://guest:guest@localhost:100/blah");
            actualUri = ParseSuccess("amqp://localhost:100/blah",
                "guest", "guest", "localhost", 100, "blah");
            Assert.Equal(amqpGuestAtLocalhostPort100BlahVhostUri, actualUri);

            Uri amqpGuestAtLocalhost6BlahVhostUri = new Uri("amqp://guest:guest@[::1]:5672/blah");
            actualUri = ParseSuccess("amqp://[::1]/blah",
                "guest", "guest", s_iPv6Loopbacks, 5672, "blah");
            Assert.Equal(amqpGuestAtLocalhost6BlahVhostUri, actualUri);

            Uri amqpGuestAtLocalhost6Port100BlahVhostUri = new Uri("amqp://guest:guest@[::1]:100/blah");
            actualUri = ParseSuccess("amqp://[::1]:100/blah",
                "guest", "guest", s_iPv6Loopbacks, 100, "blah");
            Assert.Equal(amqpGuestAtLocalhost6Port100BlahVhostUri, actualUri);

            Uri amqpUserPassAtHostRootVhostUri = new Uri("amqp://user:pass@host:5672/%2F");
            actualUri = ParseSuccess("amqp://user:pass@host",
                "user", "pass", "host", 5672, "/");
            Assert.Equal(amqpUserPassAtHostRootVhostUri, actualUri);

            Uri amqpUserPassAtHostPort100RootVhostUri = new Uri("amqp://user:pass@host:100/%2F");
            actualUri = ParseSuccess("amqp://user:pass@host:100",
                "user", "pass", "host", 100, "/");
            Assert.Equal(amqpUserPassAtHostPort100RootVhostUri, actualUri);

            Uri amqpUserPassAtLocalhostPort100RootVhostUri = new Uri("amqp://user:pass@localhost:100/%2F");
            actualUri = ParseSuccess("amqp://user:pass@localhost:100",
                "user", "pass", "localhost", 100, "/");
            Assert.Equal(amqpUserPassAtLocalhostPort100RootVhostUri, actualUri);

            Uri amqpUserPassAtLocalhost6RootVhostUri = new Uri("amqp://user:pass@[::1]:5672/%2F");
            actualUri = ParseSuccess("amqp://user:pass@[::1]",
                "user", "pass", s_iPv6Loopbacks, 5672, "/");
            Assert.Equal(amqpUserPassAtLocalhost6RootVhostUri, actualUri);

            Uri amqpUserPassAtLocalhost6Port100RootVhostUri = new Uri("amqp://user:pass@[::1]:100/%2F");
            actualUri = ParseSuccess("amqp://user:pass@[::1]:100",
                "user", "pass", s_iPv6Loopbacks, 100, "/");
            Assert.Equal(amqpUserPassAtLocalhost6Port100RootVhostUri, actualUri);

            Uri amqpsUserPassAtHostRootVhostUri = new Uri("amqps://user:pass@host:5671/%2F");
            actualUri = ParseSuccess("amqps://user:pass@host",
                "user", "pass", "host", 5671, "/", true);
            Assert.Equal(amqpsUserPassAtHostRootVhostUri, actualUri);

            Uri amqpsUserPassAtHostPort100RootVhostUri = new Uri("amqps://user:pass@host:100/%2F");
            actualUri = ParseSuccess("amqps://user:pass@host:100",
                "user", "pass", "host", 100, "/", true);
            Assert.Equal(amqpsUserPassAtHostPort100RootVhostUri, actualUri);

            Uri amqpsUserPassAtLocalhostPort100RootVhostUri = new Uri("amqps://user:pass@localhost:100/%2F");
            actualUri = ParseSuccess("amqps://user:pass@localhost:100",
                "user", "pass", "localhost", 100, "/", true);
            Assert.Equal(amqpsUserPassAtLocalhostPort100RootVhostUri, actualUri);

            Uri amqpsUserPassAtLocalhost6RootVhostUri = new Uri("amqps://user:pass@[::1]:5671/%2F");
            actualUri = ParseSuccess("amqps://user:pass@[::1]",
                "user", "pass", s_iPv6Loopbacks, 5671, "/", true);
            Assert.Equal(amqpsUserPassAtLocalhost6RootVhostUri, actualUri);

            Uri amqpsUserPassAtLocalhost6Port100RootVhostUri = new Uri("amqps://user:pass@[::1]:100/%2F");
            actualUri = ParseSuccess("amqps://user:pass@[::1]:100",
                "user", "pass", s_iPv6Loopbacks, 100, "/", true);
            Assert.Equal(amqpsUserPassAtLocalhost6Port100RootVhostUri, actualUri);
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

        private Uri ParseSuccess(string uri, string user, string password, string host, int port, string vhost, bool tlsEnabled = false)
        {
            var expectedUri = new Uri(uri);

            var factory = new ConnectionFactory { Uri = expectedUri };

            AssertUriPartEquivalence(factory, user, password, port, vhost, tlsEnabled);

            Assert.Equal(host, factory.HostName);

            return factory.Uri;
        }

        private Uri ParseSuccess(string uri, string user, string password,
            string[] hosts, int port, string vhost, bool tlsEnabled = false)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri)
            };

            AssertUriPartEquivalence(factory, user, password, port, vhost, tlsEnabled);

            Assert.True(Array.IndexOf(hosts, factory.HostName) != -1);

            return factory.Uri;
        }
    }
}

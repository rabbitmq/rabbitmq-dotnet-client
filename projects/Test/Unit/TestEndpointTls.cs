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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Test.Unit
{
    public class TestEndpointTls
    {
        public enum Overload
        {
            EndpointList,
            EndpointResolver,
            EndpointProperty
        }

        [Theory]
        [InlineData(Overload.EndpointList, false)]
        [InlineData(Overload.EndpointList, true)]
        [InlineData(Overload.EndpointResolver, false)]
        [InlineData(Overload.EndpointResolver, true)]
        [InlineData(Overload.EndpointProperty, false)]
        public async Task AnEndpointWithoutTlsUsesTheFactoryTls_GHSA_5wrf(Overload overload, bool automaticRecovery)
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery, (cf, port) =>
            {
                var endpoint = new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port);
                switch (overload)
                {
                    case Overload.EndpointList:
                        return cf.CreateConnectionAsync(new[] { endpoint });
                    case Overload.EndpointResolver:
                        return cf.CreateConnectionAsync(new DefaultEndpointResolver(new[] { endpoint }), null);
                    default:
                        cf.Endpoint = endpoint;
                        return cf.CreateConnectionAsync();
                }
            });

            AssertTlsClientHello(firstBytes);
            Assert.Contains("factory.example.com", firstBytes);
        }

        [Fact]
        public async Task AnEndpointWithItsOwnTlsUsesItsOwnOptions_GHSA_5wrf()
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: false, (cf, port) =>
            {
                var own = new SslOption("node.example.com", enabled: true);
                return cf.CreateConnectionAsync(new[] { new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port, own) });
            });

            AssertTlsClientHello(firstBytes);
            Assert.Contains("node.example.com", firstBytes);
            Assert.DoesNotContain("factory.example.com", firstBytes);
        }

        [Fact]
        public async Task TurningTlsOffAfterConnectingDoesNotReachTheConnection_GHSA_5wrf()
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: true, (cf, port) =>
            {
                var resolver = new CallbackEndpointResolver(() => cf.Ssl.Enabled = false,
                    new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port));
                return cf.CreateConnectionAsync(resolver, null);
            });

            AssertTlsClientHello(firstBytes);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ChangingTheFactoryTlsAfterConnectingDoesNotReachTheConnection_GHSA_5wrf(bool hostnames)
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: true, (cf, port) =>
            {
                cf.HostName = IPAddress.Loopback.ToString();
                cf.Port = port;
                cf.EndpointResolverFactory = endpoints => new CallbackEndpointResolver(() =>
                {
                    cf.Ssl.ServerName = "other.example.com";
                    cf.Port = 1;
                }, endpoints);
                return hostnames ? cf.CreateConnectionAsync(new[] { cf.HostName }) : cf.CreateConnectionAsync();
            });

            AssertTlsClientHello(firstBytes);
            Assert.Contains("factory.example.com", firstBytes);
        }

        [Fact]
        public async Task TurningTlsOffDuringAConnectionAttemptDoesNotReachIt_GHSA_5wrf()
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: true, (cf, port) =>
            {
                cf.HostName = IPAddress.Loopback.ToString();
                cf.Port = port;
                // Runs after the endpoints were read and before the TLS decision.
                cf.SocketFactory = family =>
                {
                    cf.Ssl.Enabled = false;
                    return ConnectionFactory.DefaultSocketFactory(family);
                };
                return cf.CreateConnectionAsync();
            });

            AssertTlsClientHello(firstBytes);
        }

        [Fact]
        public async Task ChangingAnEndpointTlsAfterConnectingDoesNotReachTheConnection_GHSA_5wrf()
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: true, (cf, port) =>
            {
                var own = new SslOption("node.example.com", enabled: true);
                cf.EndpointResolverFactory = endpoints => new CallbackEndpointResolver(() => own.ServerName = "other.example.com",
                    endpoints);
                return cf.CreateConnectionAsync(new[] { new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port, own) });
            });

            AssertTlsClientHello(firstBytes);
            Assert.Contains("node.example.com", firstBytes);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheEndpointAConnectionReportsDoesNotChangeItsRecoveries_GHSA_5wrf(bool endpointHasOwnTls)
        {
            var endpoint = endpointHasOwnTls
                ? new AmqpTcpEndpoint("broker.example.com", -1, new SslOption("node.example.com", enabled: true))
                : new AmqpTcpEndpoint("broker.example.com");
            var resolver = new ConnectionFactory.TlsEndpointResolver(new DefaultEndpointResolver(new[] { endpoint }),
                new SslOption("factory.example.com", enabled: true));

            AmqpTcpEndpoint reported = resolver.All().Single();
            reported.Ssl.ServerName = "other.example.com";
            reported.Ssl.Enabled = false;

            AmqpTcpEndpoint recovered = resolver.All().Single();
            Assert.True(recovered.Ssl.Enabled);
            Assert.Equal(endpointHasOwnTls ? "node.example.com" : "factory.example.com", recovered.Ssl.ServerName);
            Assert.Equal(5671, recovered.Port);
        }

        [Fact]
        public async Task AFactoryWithoutSslOptionsStillFailsAsUnreachable_GHSA_5wrf()
        {
            var cf = new ConnectionFactory { HostName = IPAddress.Loopback.ToString(), Port = 1, Ssl = null! };

            Task<IConnection> connecting = cf.CreateConnectionAsync();

            await Assert.ThrowsAsync<BrokerUnreachableException>(() => connecting);
        }

        [Fact]
        public async Task ANullEndpointDoesNotStopTheNextOneBeingTried_GHSA_5wrf()
        {
            string firstBytes = await CaptureFirstBytesAsync(automaticRecovery: false, (cf, port) =>
            {
                var resolver = new CallbackEndpointResolver(() => { }, null!, new AmqpTcpEndpoint(IPAddress.Loopback.ToString(), port));
                return cf.CreateConnectionAsync(resolver, null);
            });

            AssertTlsClientHello(firstBytes);
        }

        [Fact]
        public void AnEndpointWithoutTlsKeepsTheFactoryTls_GHSA_5wrf()
        {
            var cf = new ConnectionFactory();
            cf.Ssl.Enabled = true;
            SslOption ssl = cf.Ssl;

            cf.Endpoint = new AmqpTcpEndpoint("broker.example.com");

            Assert.Same(ssl, cf.Ssl);
            Assert.Equal("broker.example.com", cf.HostName);
            Assert.Equal(5671, cf.Endpoint.Port);

            cf.Endpoint = new AmqpTcpEndpoint("broker.example.com", 5000);
            Assert.Same(ssl, cf.Ssl);
            Assert.Equal(5000, cf.Endpoint.Port);
        }

        [Fact]
        public void AnEndpointWithItsOwnTlsReplacesTheFactoryTls_GHSA_5wrf()
        {
            var cf = new ConnectionFactory();
            cf.Ssl.Enabled = true;
            var own = new SslOption("node.example.com", enabled: true);

            cf.Endpoint = new AmqpTcpEndpoint("node.example.com", -1, own);

            Assert.Same(own, cf.Ssl);
        }

        [Fact]
        public void AnEndpointWithoutTlsReplacesTheOptionsOfAFactoryWithoutTls_GHSA_5wrf()
        {
            var cf = new ConnectionFactory();
            var plain = new SslOption();

            cf.Endpoint = new AmqpTcpEndpoint("broker.example.com", -1, plain);

            Assert.Same(plain, cf.Ssl);
            Assert.Equal(5672, cf.Endpoint.Port);
        }

        [Fact]
        public void AnEndpointGivenTheFactoryTlsKeepsItsDefaultPortAndAddressFamily_GHSA_5wrf()
        {
            var endpoint = new AmqpTcpEndpoint("broker.example.com") { AddressFamily = AddressFamily.InterNetworkV6 };

            AmqpTcpEndpoint copy = endpoint.CloneWithSsl(new SslOption("broker.example.com", enabled: true));

            Assert.Equal(5671, copy.Port);
            Assert.Equal(AddressFamily.InterNetworkV6, copy.AddressFamily);
            Assert.False(endpoint.Ssl.Enabled);
        }

        private static void AssertTlsClientHello(string firstBytes)
        {
            // A TLS handshake record starts 0x16 0x03; an AMQP client opens with "AMQP".
            Assert.False(firstBytes.StartsWith("AMQP", StringComparison.Ordinal), "the client sent AMQP in cleartext");
            Assert.True(firstBytes.Length >= 2 && firstBytes[0] == 0x16 && firstBytes[1] == 0x03,
                $"the client did not open with a TLS handshake record: {firstBytes.Length} bytes");
        }

        private static async Task ReadAtLeastAsync(NetworkStream stream, List<byte> received, int count)
        {
            var buffer = new byte[4096];
            while (received.Count < count)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                Assert.True(read > 0, $"the client closed after {received.Count} bytes");
                received.AddRange(new ArraySegment<byte>(buffer, 0, read));
            }
        }

        // Returns what the client sends first as ASCII, so the server name in a ClientHello can be matched.
        private static async Task<string> CaptureFirstBytesAsync(bool automaticRecovery,
            Func<ConnectionFactory, int, Task<IConnection>> connect)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var cf = new ConnectionFactory
                {
                    AutomaticRecoveryEnabled = automaticRecovery,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                    HandshakeContinuationTimeout = TimeSpan.FromSeconds(5)
                };
                cf.Ssl.Enabled = true;
                cf.Ssl.ServerName = "factory.example.com";

                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                Task<IConnection> connecting = connect(cf, ((IPEndPoint)listener.LocalEndpoint).Port);

                await Task.WhenAny(accept, connecting, Task.Delay(TimeSpan.FromSeconds(15)));
                Assert.True(accept.IsCompleted, $"the client never connected to the listener: {connecting.Exception?.InnerException}");

                var received = new List<byte>();
                using (TcpClient client = await accept)
                {
                    NetworkStream stream = client.GetStream();
                    await ReadAtLeastAsync(stream, received, 5);
                    if (received[0] == 0x16)
                    {
                        // A TLS record's 5-byte header ends with the length of its body.
                        await ReadAtLeastAsync(stream, received, 5 + ((received[3] << 8) | received[4]));
                    }
                }

                await Assert.ThrowsAsync<BrokerUnreachableException>(() => connecting);
                return Encoding.ASCII.GetString(received.ToArray());
            }
            finally
            {
                listener.Stop();
            }
        }

        // Runs the callback each time the endpoints are read: after CreateConnectionAsync has taken its
        // copies, where a recovery would next read them.
        private sealed class CallbackEndpointResolver : IEndpointResolver
        {
            private readonly Action _beforeEnumerating;
            private readonly IEnumerable<AmqpTcpEndpoint> _endpoints;

            internal CallbackEndpointResolver(Action beforeEnumerating, params AmqpTcpEndpoint[] endpoints)
                : this(beforeEnumerating, (IEnumerable<AmqpTcpEndpoint>)endpoints)
            {
            }

            // Keeps the sequence, as a custom resolver may, so a lazy one is enumerated again on each read.
            internal CallbackEndpointResolver(Action beforeEnumerating, IEnumerable<AmqpTcpEndpoint> endpoints)
            {
                _beforeEnumerating = beforeEnumerating;
                _endpoints = endpoints;
            }

            public IEnumerable<AmqpTcpEndpoint> All()
            {
                _beforeEnumerating();
                return _endpoints;
            }
        }
    }
}

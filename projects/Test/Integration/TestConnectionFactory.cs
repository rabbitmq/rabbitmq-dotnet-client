﻿// This source code is dual-licensed under the Apache License, version
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConnectionFactory : IntegrationFixture
    {
        public TestConnectionFactory(ITestOutputHelper output) : base(output)
        {
        }

        public override Task InitializeAsync()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        [Fact]
        public void TestProperties()
        {
            string u = "username";
            string pw = "password";
            string v = "vhost";
            string h = "192.168.0.1";
            int p = 5674;
            uint mms = 64 * 1024 * 1024;

            var cf = new ConnectionFactory
            {
                UserName = u,
                Password = pw,
                VirtualHost = v,
                HostName = h,
                Port = p,
                MaxInboundMessageBodySize = mms
            };

            Assert.Equal(u, cf.UserName);
            Assert.Equal(pw, cf.Password);
            Assert.Equal(v, cf.VirtualHost);
            Assert.Equal(h, cf.HostName);
            Assert.Equal(p, cf.Port);
            Assert.Equal(mms, cf.MaxInboundMessageBodySize);

            Assert.Equal(h, cf.Endpoint.HostName);
            Assert.Equal(p, cf.Endpoint.Port);
            Assert.Equal(mms, cf.Endpoint.MaxInboundMessageBodySize);
        }

        [Fact]
        public void TestConnectionFactoryWithCustomSocketFactory()
        {
            const int testBufsz = 1024;
            int defaultReceiveBufsz = 0;
            int defaultSendBufsz = 0;
            using (var defaultSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP))
            {
                defaultReceiveBufsz = defaultSocket.ReceiveBufferSize;
                defaultSendBufsz = defaultSocket.SendBufferSize;
            }

            var cf = new ConnectionFactory
            {
                SocketFactory = (AddressFamily af) =>
                {
                    var socket = new Socket(af, SocketType.Stream, ProtocolType.Tcp)
                    {
                        SendBufferSize = testBufsz,
                        ReceiveBufferSize = testBufsz,
                        NoDelay = false
                    };
                    return new TcpClientAdapter(socket);
                }
            };

            ITcpClient c = cf.SocketFactory(AddressFamily.InterNetwork);
            Assert.IsType<TcpClientAdapter>(c);
            TcpClientAdapter tcpClientAdapter = (TcpClientAdapter)c;
            Socket s = tcpClientAdapter.Client;
            Assert.NotEqual(defaultReceiveBufsz, s.ReceiveBufferSize);
            Assert.NotEqual(defaultSendBufsz, s.SendBufferSize);
            Assert.False(s.NoDelay);
        }

        [Fact]
        public async Task TestCreateConnectionWithInvalidPortThrows()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 1234;

            await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync();
            });
        }

        [Fact]
        public async Task TestCreateConnectionWithClientProvidedNameUsesSpecifiedPort()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 123;

            await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync();
            });
        }

        [Fact]
        public async Task TestCreateConnectionWithClientProvidedNameUsesDefaultName()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            string expectedName = cf.ClientProvidedName;

            await using IConnection conn = await cf.CreateConnectionAsync();
            Assert.Equal(expectedName, conn.ClientProvidedName);
            Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithClientProvidedNameUsesNameArgumentValue()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            string expectedName = cf.ClientProvidedName;

            await using IConnection conn = await cf.CreateConnectionAsync(expectedName);
            Assert.Equal(expectedName, conn.ClientProvidedName);
            Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesNameArgumentValue()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            string expectedName = cf.ClientProvidedName;

            await using IConnection conn = await cf.CreateConnectionAsync(expectedName);
            Assert.Equal(expectedName, conn.ClientProvidedName);
            Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionAmqpTcpEndpointListAndClientProvidedName()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            string expectedName = cf.ClientProvidedName;

            var xs = new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost") };
            await using IConnection conn = await cf.CreateConnectionAsync(xs, expectedName);
            Assert.Equal(expectedName, conn.ClientProvidedName);
            Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionUsesDefaultPort()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";

            await using IConnection conn = await cf.CreateConnectionAsync();
            Assert.Equal(5672, conn.Endpoint.Port);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionUsesDefaultMaxMessageSize()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";

            Assert.Equal(ConnectionFactory.DefaultMaxInboundMessageBodySize, cf.MaxInboundMessageBodySize);
            Assert.Equal(ConnectionFactory.DefaultMaxInboundMessageBodySize, cf.Endpoint.MaxInboundMessageBodySize);

            await using IConnection conn = await cf.CreateConnectionAsync();
            Assert.Equal(ConnectionFactory.DefaultMaxInboundMessageBodySize, conn.Endpoint.MaxInboundMessageBodySize);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithoutAutoRecoverySelectsAHostFromTheList()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.HostName = "not_localhost";

            IConnection conn = await cf.CreateConnectionAsync(new List<string> { "localhost" });
            await conn.CloseAsync();
            await conn.DisposeAsync();
            Assert.Equal("not_localhost", cf.HostName);
            Assert.Equal("localhost", conn.Endpoint.HostName);
        }

        [Fact]
        public async Task TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "not_localhost";
            cf.Port = 1234;
            var ep = new AmqpTcpEndpoint("localhost");
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            });
        }

        [Fact]
        public async Task TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.HostName = "not_localhost";
            cf.Port = 1234;
            var ep = new AmqpTcpEndpoint("localhost");
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithForcedAddressFamily()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.HostName = "not_localhost";
            var ep = new AmqpTcpEndpoint("localhost")
            {
                AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork
            };
            cf.Endpoint = ep;
            await using IConnection conn = await cf.CreateConnectionAsync();
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithInvalidAmqpTcpEndpointThrows()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            });
        }

        [Fact]
        public async Task TestCreateConnectionUsesValidEndpointWhenMultipleSupplied()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { invalidEp, ep });
            await conn.CloseAsync();
        }

        [Fact]
        public void TestCreateAmqpTCPEndPointOverridesMaxMessageSizeWhenGreaterThanMaximumAllowed()
        {
            var ep = new AmqpTcpEndpoint("localhost", -1, new SslOption(),
                2 * InternalConstants.DefaultRabbitMqMaxInboundMessageBodySize);
            Assert.Equal(InternalConstants.DefaultRabbitMqMaxInboundMessageBodySize, ep.MaxInboundMessageBodySize);
        }

        [Fact]
        public async Task TestCreateConnectionUsesConfiguredMaxMessageSize()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.MaxInboundMessageBodySize = 1500;
            await using IConnection conn = await cf.CreateConnectionAsync();
            Assert.Equal(cf.MaxInboundMessageBodySize, conn.Endpoint.MaxInboundMessageBodySize);
            await conn.CloseAsync();
        }
        [Fact]
        public async Task TestCreateConnectionWithAmqpEndpointListUsesAmqpTcpEndpointMaxMessageSize()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.MaxInboundMessageBodySize = 1500;
            var ep = new AmqpTcpEndpoint("localhost");
            Assert.Equal(ConnectionFactory.DefaultMaxInboundMessageBodySize, ep.MaxInboundMessageBodySize);
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            Assert.Equal(ConnectionFactory.DefaultMaxInboundMessageBodySize, conn.Endpoint.MaxInboundMessageBodySize);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithAmqpEndpointResolverUsesAmqpTcpEndpointMaxMessageSize()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.MaxInboundMessageBodySize = 1500;
            var ep = new AmqpTcpEndpoint("localhost", -1, new SslOption(), 1200);
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            Assert.Equal(ep.MaxInboundMessageBodySize, conn.Endpoint.MaxInboundMessageBodySize);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithHostnameListUsesConnectionFactoryMaxMessageSize()
        {
            ConnectionFactory cf = CreateConnectionFactory();
            cf.MaxInboundMessageBodySize = 1500;
            await using IConnection conn = await cf.CreateConnectionAsync(new List<string> { "localhost" });
            Assert.Equal(cf.MaxInboundMessageBodySize, conn.Endpoint.MaxInboundMessageBodySize);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionAsync_WithAlreadyCanceledToken()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            ConnectionFactory cf = CreateConnectionFactory();

            bool passed = false;
            /*
                 * If anyone wonders why TaskCanceledException is explicitly checked,
                 * even though it's a subclass of OperationCanceledException:
                 * https://github.com/rabbitmq/rabbitmq-dotnet-client/commit/383ca5c5f161edb717cf8fae7bf143c13143f634#r135400615
                 */
            try
            {
                await cf.CreateConnectionAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                passed = true;
            }
            catch (OperationCanceledException)
            {
                passed = true;
            }

            Assert.True(passed, "FAIL did not see TaskCanceledException nor OperationCanceledException");
        }

        [Fact]
        public async Task TestCreateConnectionAsync_UsesValidEndpointWhenMultipleSupplied()
        {
            using var cts = new CancellationTokenSource(WaitSpan);
            ConnectionFactory cf = CreateConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            await using IConnection conn = await cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { invalidEp, ep }, cts.Token);
            await conn.CloseAsync(cts.Token);
        }

        [Theory]
        [InlineData(2998)]
        [InlineData(2999)]
        [InlineData(3000)]
        [InlineData(3001)]
        [InlineData(3002)]
        [InlineData(3003)]
        public async Task TestCreateConnectionAsync_TruncatesWhenClientNameIsLong_GH980(ushort count)
        {
            string cpn = GetUniqueString(count);
            using var cts = new CancellationTokenSource(WaitSpan);
            ConnectionFactory cf0 = new ConnectionFactory { ClientProvidedName = cpn };
            await using (IConnection conn = await cf0.CreateConnectionAsync(cts.Token))
            {
                await conn.CloseAsync(cts.Token);
                Assert.True(cf0.ClientProvidedName.Length <= InternalConstants.DefaultRabbitMqMaxClientProvideNameLength);
                Assert.Contains(cf0.ClientProvidedName, cpn);
            }

            ConnectionFactory cf1 = new ConnectionFactory();
            await using (IConnection conn = await cf1.CreateConnectionAsync(cpn, cts.Token))
            {
                await conn.CloseAsync(cts.Token);
                Assert.True(conn.ClientProvidedName.Length <= InternalConstants.DefaultRabbitMqMaxClientProvideNameLength);
                Assert.Contains(conn.ClientProvidedName, cpn);
            }
        }

        [Fact]
        public async Task TestCreateConnectionRegisterAnActivity()
        {
            using ActivityRecorder connectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;
            ConnectionFactory cf = CreateConnectionFactory();
            await using IConnection conn = await cf.CreateConnectionAsync();
            var connectionActivity = connectionRecorder.VerifyActivityRecordedOnce();
            connectionActivity.HasTag("network.peer.address");
            connectionActivity.HasTag("network.local.address");
            connectionActivity.HasTag("server.address");
            connectionActivity.HasTag("client.address");
            connectionActivity.HasTag("network.peer.port");
            connectionActivity.HasTag("network.local.port");
            connectionActivity.HasTag("server.port");
            connectionActivity.HasTag("client.port");
            connectionActivity.HasTag("network.type");
            var tcpConnectionActivity = tcpConnectionRecorder.VerifyActivityRecordedOnce();
            tcpConnectionActivity.HasTag("server.port");
            tcpConnectionActivity.HasTag("server.address");
            Assert.Equal(connectionActivity, tcpConnectionActivity.Parent);
            await conn.CloseAsync();
        }

        [Fact]
        public async Task TestCreateConnectionWithFailureRecordException()
        {
            using ActivityRecorder recorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "connection attempt");
            using ActivityRecorder tcpConnectionRecorder =
                new(RabbitMQActivitySource.ConnectionSourceName, "tcp connection attempt");
            tcpConnectionRecorder.VerifyParent = false;
            ConnectionFactory cf = CreateConnectionFactory();
            var unreachablePort = 1234;
            var ep = new AmqpTcpEndpoint("localhost", unreachablePort);
            var exception = await Assert.ThrowsAsync<BrokerUnreachableException>(() =>
            {
                return cf.CreateConnectionAsync(new List<AmqpTcpEndpoint> { ep });
            });
            Activity connectionActivity = recorder.VerifyActivityRecordedOnce();
            connectionActivity.HasRecordedException(exception);
            connectionActivity.IsInError();
            Activity tcpConnectionActivity = tcpConnectionRecorder.VerifyActivityRecordedOnce();
            tcpConnectionActivity.HasRecordedException("RabbitMQ.Client.Exceptions.ConnectFailureException");
        }
    }
}

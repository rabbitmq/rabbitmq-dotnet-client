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

using System.Collections.Generic;
using System.Net.Sockets;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestConnectionFactory
    {
        [Fact]
        public void TestProperties()
        {
            string u = "username";
            string pw = "password";
            string v = "vhost";
            string h = "192.168.0.1";
            int p = 5674;
            uint mms = 512 * 1024 * 1024;

            var cf = new ConnectionFactory
            {
                UserName = u,
                Password = pw,
                VirtualHost = v,
                HostName = h,
                Port = p,
                MaxMessageSize = mms
            };

            Assert.Equal(cf.UserName, u);
            Assert.Equal(cf.Password, pw);
            Assert.Equal(cf.VirtualHost, v);
            Assert.Equal(cf.HostName, h);
            Assert.Equal(cf.Port, p);
            Assert.Equal(cf.MaxMessageSize, mms);

            Assert.Equal(cf.Endpoint.HostName, h);
            Assert.Equal(cf.Endpoint.Port, p);
            Assert.Equal(cf.Endpoint.MaxMessageSize, mms);
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

            ConnectionFactory cf = new()
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
        public void TestCreateConnectionUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost",
                Port = 1234
            };

            Assert.Throws<BrokerUnreachableException>(() => { using IConnection conn = cf.CreateConnection(); });
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost",
                Port = 1234
            };
            Assert.Throws<BrokerUnreachableException>(() => { using IConnection conn = cf.CreateConnection("some_name"); });
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesDefaultName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                ClientProvidedName = "some_name"
            };
            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal("some_name", conn.ClientProvidedName);
                Assert.Equal("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false
            };
            using (IConnection conn = cf.CreateConnection("some_name"))
            {
                Assert.Equal("some_name", conn.ClientProvidedName);
                Assert.Equal("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            using (IConnection conn = cf.CreateConnection("some_name"))
            {
                Assert.Equal("some_name", conn.ClientProvidedName);
                Assert.Equal("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionAmqpTcpEndpointListAndClientProvidedName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var xs = new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost") };
            using (IConnection conn = cf.CreateConnection(xs, "some_name"))
            {
                Assert.Equal("some_name", conn.ClientProvidedName);
                Assert.Equal("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionUsesDefaultPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost"
            };
            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(5672, conn.Endpoint.Port);
            }
        }

        [Fact]
        public void TestCreateConnectionUsesDefaultMaxMessageSize()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost"
            };

            Assert.Equal(ConnectionFactory.DefaultMaxMessageSize, cf.MaxMessageSize);
            Assert.Equal(ConnectionFactory.DefaultMaxMessageSize, cf.Endpoint.MaxMessageSize);

            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(ConnectionFactory.DefaultMaxMessageSize, conn.Endpoint.MaxMessageSize);
            }
        }

        [Fact]
        public void TestCreateConnectionWithoutAutoRecoverySelectsAHostFromTheList()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                HostName = "not_localhost"
            };
            IConnection conn = cf.CreateConnection(new List<string> { "localhost" }, "oregano");
            conn.Close();
            conn.Dispose();
            Assert.Equal("not_localhost", cf.HostName);
            Assert.Equal("localhost", conn.Endpoint.HostName);
        }

        [Fact]
        public void TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep })) { }
        }

        [Fact]
        public void TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.Throws<BrokerUnreachableException>(() => { using IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep }); });
        }

        [Fact]
        public void TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep })) { }
        }

        [Fact]
        public void TestCreateConnectionWithForcedAddressFamily()
        {
            var cf = new ConnectionFactory
            {
                HostName = "not_localhost"
            };
            var ep = new AmqpTcpEndpoint("localhost")
            {
                AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork
            };
            cf.Endpoint = ep;
            using (IConnection conn = cf.CreateConnection()) { };
        }

        [Fact]
        public void TestCreateConnectionUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.Throws<BrokerUnreachableException>(() => { using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep })) { } });
        }

        [Fact]
        public void TestCreateConnectioUsesValidEndpointWhenMultipleSupplied()
        {
            var cf = new ConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { invalidEp, ep })) { };
        }

        [Fact]
        public void TestCreateAmqpTCPEndPointOverridesMaxMessageSizeWhenGreaterThanMaximumAllowed()
        {
            var ep = new AmqpTcpEndpoint("localhost", -1, new SslOption(), ConnectionFactory.MaximumMaxMessageSize);
        }

        [Fact]
        public void TestCreateConnectionUsesConfiguredMaxMessageSize()
        {
            var cf = new ConnectionFactory();
            cf.MaxMessageSize = 1500;
            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(cf.MaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }
        [Fact]
        public void TestCreateConnectionWithAmqpEndpointListUsesAmqpTcpEndpointMaxMessageSize()
        {
            var cf = new ConnectionFactory();
            cf.MaxMessageSize = 1500;
            var ep = new AmqpTcpEndpoint("localhost");
            Assert.Equal(ConnectionFactory.DefaultMaxMessageSize, ep.MaxMessageSize);
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep }))
            {
                Assert.Equal(ConnectionFactory.DefaultMaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }

        [Fact]
        public void TestCreateConnectionWithAmqpEndpointResolverUsesAmqpTcpEndpointMaxMessageSize()
        {
            var cf = new ConnectionFactory();
            cf.MaxMessageSize = 1500;
            var ep = new AmqpTcpEndpoint("localhost", -1, new SslOption(), 1200);
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep }))
            {
                Assert.Equal(ep.MaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }

        [Fact]
        public void TestCreateConnectionWithHostnameListUsesConnectionFactoryMaxMessageSize()
        {
            var cf = new ConnectionFactory();
            cf.MaxMessageSize = 1500;
            using (IConnection conn = cf.CreateConnection(new List<string> { "localhost" }))
            {
                Assert.Equal(cf.MaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }
    }
}

﻿// This source code is dual-licensed under the Apache License, version
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

        protected override void SetUp()
        {
            // NB: nothing to do here since each test creates its own factory,
            // connections and channels
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
        }

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
        public void TestCreateConnectionUsesSpecifiedPort()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 1234;

            Assert.Throws<BrokerUnreachableException>(() =>
            {
                using IConnection conn = cf.CreateConnection();
            });
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesSpecifiedPort()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 123;

            Assert.Throws<BrokerUnreachableException>(() =>
            {
                using IConnection conn = cf.CreateConnection();
            });
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesDefaultName()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            string expectedName = cf.ClientProvidedName;

            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(expectedName, conn.ClientProvidedName);
                Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameUsesNameArgumentValue()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            string expectedName = cf.ClientProvidedName;

            using (IConnection conn = cf.CreateConnection(expectedName))
            {
                Assert.Equal(expectedName, conn.ClientProvidedName);
                Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesNameArgumentValue()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            string expectedName = cf.ClientProvidedName;

            using (IConnection conn = cf.CreateConnection(expectedName))
            {
                Assert.Equal(expectedName, conn.ClientProvidedName);
                Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionAmqpTcpEndpointListAndClientProvidedName()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            string expectedName = cf.ClientProvidedName;

            var xs = new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost") };
            using (IConnection conn = cf.CreateConnection(xs, expectedName))
            {
                Assert.Equal(expectedName, conn.ClientProvidedName);
                Assert.Equal(expectedName, conn.ClientProperties["connection_name"]);
            }
        }

        [Fact]
        public void TestCreateConnectionUsesDefaultPort()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";

            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(5672, conn.Endpoint.Port);
            }
        }

        [Fact]
        public void TestCreateConnectionUsesDefaultMaxMessageSize()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";

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
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.HostName = "not_localhost";

            IConnection conn = cf.CreateConnection(new List<string> { "localhost" });
            conn.Close();
            conn.Dispose();
            Assert.Equal("not_localhost", cf.HostName);
            Assert.Equal("localhost", conn.Endpoint.HostName);
        }

        [Fact]
        public void TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "not_localhost";
            cf.Port = 1234;
            var ep = new AmqpTcpEndpoint("localhost");
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep })) { }
        }

        [Fact]
        public void TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            var cf = CreateConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.Throws<BrokerUnreachableException>(() => { using IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep }); });
        }

        [Fact]
        public void TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            var cf = CreateConnectionFactory();
            cf.HostName = "not_localhost";
            cf.Port = 1234;
            var ep = new AmqpTcpEndpoint("localhost");
            using (IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep })) { }
        }

        [Fact]
        public void TestCreateConnectionWithForcedAddressFamily()
        {
            var cf = CreateConnectionFactory();
            cf.HostName = "not_localhost";
            var ep = new AmqpTcpEndpoint("localhost")
            {
                AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork
            };
            cf.Endpoint = ep;
            using IConnection conn = cf.CreateConnection();
        }

        [Fact]
        public void TestCreateConnectionUsesInvalidAmqpTcpEndpoint()
        {
            var cf = CreateConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.Throws<BrokerUnreachableException>(() =>
            {
                using IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { ep });
            });
        }

        [Fact]
        public void TestCreateConnectioUsesValidEndpointWhenMultipleSupplied()
        {
            var cf = CreateConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { invalidEp, ep });
        }

        [Fact]
        public void TestCreateAmqpTCPEndPointOverridesMaxMessageSizeWhenGreaterThanMaximumAllowed()
        {
            var ep = new AmqpTcpEndpoint("localhost", -1, new SslOption(), ConnectionFactory.MaximumMaxMessageSize);
        }

        [Fact]
        public void TestCreateConnectionUsesConfiguredMaxMessageSize()
        {
            var cf = CreateConnectionFactory();
            cf.MaxMessageSize = 1500;
            using (IConnection conn = cf.CreateConnection())
            {
                Assert.Equal(cf.MaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }
        [Fact]
        public void TestCreateConnectionWithAmqpEndpointListUsesAmqpTcpEndpointMaxMessageSize()
        {
            var cf = CreateConnectionFactory();
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
            var cf = CreateConnectionFactory();
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
            var cf = CreateConnectionFactory();
            cf.MaxMessageSize = 1500;
            using (IConnection conn = cf.CreateConnection(new List<string> { "localhost" }))
            {
                Assert.Equal(cf.MaxMessageSize, conn.Endpoint.MaxMessageSize);
            };
        }
    }
}

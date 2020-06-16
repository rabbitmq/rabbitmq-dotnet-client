﻿// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2020 VMware, Inc. or its affiliates.
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
// The MPL v1.1:
//
//---------------------------------------------------------------------------
//  The contents of this file are subject to the Mozilla Public License
//  Version 1.1 (the "License"); you may not use this file except in
//  compliance with the License. You may obtain a copy of the License
//  at https://www.mozilla.org/MPL/
//
//  Software distributed under the License is distributed on an "AS IS"
//  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
//  the License for the specific language governing rights and
//  limitations under the License.
//
//  The Original Code is RabbitMQ.
//
//  The Initial Developer of the Original Code is Pivotal Software, Inc.
//  Copyright (c) 2011-2020 VMware, Inc. or its affiliates.  All rights reserved.
//---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    class TestConnectionFactory
    {
        [Test]
        public void TestProperties()
        {
            string u  = "username";
            string pw = "password";
            string v  = "vhost";
            string h  = "192.168.0.1";
            int p  = 5674;

            var cf = new ConnectionFactory
            {
                UserName = u,
                Password = pw,
                VirtualHost = v,
                HostName = h,
                Port = p
            };

            Assert.AreEqual(cf.UserName, u);
            Assert.AreEqual(cf.Password, pw);
            Assert.AreEqual(cf.VirtualHost, v);
            Assert.AreEqual(cf.HostName, h);
            Assert.AreEqual(cf.Port, p);
        }

        [Test]
        public void TestCreateConnectionUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost",
                Port = 1234
            };
            Assert.ThrowsAsync<BrokerUnreachableException>(async () =>
                    {
                        using (IConnection conn = await cf.CreateConnection()) { }
                    });
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost",
                Port = 1234
            };
            Assert.ThrowsAsync<BrokerUnreachableException>(async () =>
                    {
                        using(IConnection conn = await cf.CreateConnection("some_name")) {}
                    });
        }

        [Test]
        public async ValueTask TestCreateConnectionWithClientProvidedNameUsesDefaultName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                ClientProvidedName = "some_name"
            };
            using (IConnection conn = await cf.CreateConnection())
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public async ValueTask TestCreateConnectionWithClientProvidedNameUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false
            };
            using (IConnection conn = await cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public async ValueTask TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            using (IConnection conn = await cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public async ValueTask TestCreateConnectionAmqpTcpEndpointListAndClientProvidedName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var xs = new System.Collections.Generic.List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost") };
            using(IConnection conn = await cf.CreateConnection(xs, "some_name"))
            {
              Assert.AreEqual("some_name", conn.ClientProvidedName);
              Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public async ValueTask TestCreateConnectionUsesDefaultPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost"
            };
            using (IConnection conn = await cf.CreateConnection()){
                Assert.AreEqual(5672, conn.Endpoint.Port);
            }
        }

        [Test]
        public async ValueTask TestCreateConnectionWithoutAutoRecoverySelectsAHostFromTheList()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                HostName = "not_localhost"
            };
            IConnection conn = await cf.CreateConnection(new System.Collections.Generic.List<string> { "localhost" }, "oregano");
            await conn.Close();
            conn.Dispose();
            Assert.AreEqual("not_localhost", cf.HostName);
            Assert.AreEqual("localhost", conn.Endpoint.HostName);
        }

        [Test]
        public async ValueTask TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = await cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
        public void TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.ThrowsAsync<BrokerUnreachableException>(async () =>
                    {
                        using(IConnection conn = await cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })){}
                    });
        }

        [Test]
        public async ValueTask TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = await cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
        public async ValueTask TestCreateConnectionWithForcedAddressFamily()
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
            using(IConnection conn = await cf.CreateConnection()){};
        }

        [Test]
        public void TestCreateConnectionUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.ThrowsAsync<BrokerUnreachableException>(async () =>
                    {
                        using(IConnection conn = await cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
                    });
        }

        [Test]
        public async ValueTask TestCreateConnectioUsesValidEndpointWhenMultipleSupplied()
        {
            var cf = new ConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = await cf.CreateConnection(new List<AmqpTcpEndpoint> { invalidEp, ep })) {};
        }
    }
}

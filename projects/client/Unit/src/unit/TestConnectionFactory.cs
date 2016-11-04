// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2011-2016 Pivotal Software, Inc.
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
//  Copyright (c) 2011-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
            var u  = "username";
            var pw = "password";
            var v  = "vhost";
            var h  = "192.168.0.1";
            var p  = 5674;

            var cf = new ConnectionFactory();
            cf.UserName = u;
            cf.Password = pw;
            cf.VirtualHost = v;
            cf.HostName = h;
            cf.Port = p;

            Assert.AreEqual(cf.UserName, u);
            Assert.AreEqual(cf.Password, pw);
            Assert.AreEqual(cf.VirtualHost, v);
            Assert.AreEqual(cf.HostName, h);
            Assert.AreEqual(cf.Port, p);
        }

        [Test]
        public void TestCreateConnectionUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 1234;
            Assert.That(() =>
                    {
                        using(var conn = cf.CreateConnection()) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameUsesSpecifiedPort()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            cf.Port = 1234;
            Assert.That(() =>
                    {
                        using(var conn = cf.CreateConnection("some_name")) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameUsesName()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            using(var conn = cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesName()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            using(var conn = cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }
        [Test]
        public void TestCreateConnectionUsesDefaultPort()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "localhost";
            using(var conn = cf.CreateConnection()){
                Assert.AreEqual(5672, conn.Endpoint.Port);
            }
        }

        [Test]
        public void TestCreateConnectionWithoutAutoRecoverySelectsAHostFromTheList()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = false;
            cf.HostName = "not_localhost";
            var conn = cf.CreateConnection(new System.Collections.Generic.List<string> { "localhost" }, "oregano");
            conn.Close();
            conn.Dispose();
            Assert.AreEqual("not_localhost", cf.HostName);
            Assert.AreEqual("localhost", conn.Endpoint.HostName);       
        }

        [Test]
        public void TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.HostName = "not_localhost";
            cf.Port = 1234 ;
            var ep = new AmqpTcpEndpoint("localhost");
            using(var conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
        public void TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.That(() =>
                    {
                        using(var conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })){}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            cf.HostName = "not_localhost";
            cf.Port = 1234 ;
            var ep = new AmqpTcpEndpoint("localhost");
            using(var conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
        public void TestCreateConnectionWithForcedAddressFamily()
        {
            var cf = new ConnectionFactory();
            cf.HostName = "not_localhost";
            var ep = new AmqpTcpEndpoint("localhost");
            ep.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork;
            cf.Endpoint = ep;
            using(var conn = cf.CreateConnection());
        }

        [Test]
        public void TestCreateConnectionUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.That(() =>
                    {
                        using(var conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectioUsesValidEndpointWhenMultipleSupplied()
        {
            var cf = new ConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using(var conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { invalidEp, ep })) {};
        }
    }
}

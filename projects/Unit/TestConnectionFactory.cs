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
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionFactory
    {
        [Test]
        public void TestProperties()
        {
            string u  = "username";
            string pw = "password";
            string v  = "vhost";
            string h  = "192.168.0.1";
            int p  = 5674;
            uint mms  = 512 * 1024 * 1024;

            var cf = new ConnectionFactory
            {
                UserName = u,
                Password = pw,
                VirtualHost = v,
                HostName = h,
                Port = p,
                MaxMessageSize = mms
            };

            Assert.AreEqual(cf.UserName, u);
            Assert.AreEqual(cf.Password, pw);
            Assert.AreEqual(cf.VirtualHost, v);
            Assert.AreEqual(cf.HostName, h);
            Assert.AreEqual(cf.Port, p);
            Assert.AreEqual(cf.MaxMessageSize, mms);

            Assert.AreEqual(cf.Endpoint.HostName, h);
            Assert.AreEqual(cf.Endpoint.Port, p);
            Assert.AreEqual(cf.Endpoint.MaxMessageSize, mms);
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
            Assert.That(() =>
                    {
                        using(IConnection conn = cf.CreateConnection()) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
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
            Assert.That(() =>
                    {
                        using(IConnection conn = cf.CreateConnection("some_name")) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameUsesDefaultName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                ClientProvidedName = "some_name"
            };
            using (IConnection conn = cf.CreateConnection())
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false
            };
            using (IConnection conn = cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public void TestCreateConnectionWithClientProvidedNameAndAutorecoveryUsesNameArgumentValue()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            using (IConnection conn = cf.CreateConnection("some_name"))
            {
                Assert.AreEqual("some_name", conn.ClientProvidedName);
                Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public void TestCreateConnectionAmqpTcpEndpointListAndClientProvidedName()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var xs = new System.Collections.Generic.List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost") };
            using(IConnection conn = cf.CreateConnection(xs, "some_name"))
            {
              Assert.AreEqual("some_name", conn.ClientProvidedName);
              Assert.AreEqual("some_name", conn.ClientProperties["connection_name"]);
            }
        }

        [Test]
        public void TestCreateConnectionWithSynchronousWriteLoop()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost",
                EnableSynchronousWriteLoop = true
            };
            using (IConnection conn = cf.CreateConnection()){
                Assert.AreEqual(5672, conn.Endpoint.Port);
            }
        }

        [Test]
        public void TestCreateConnectionUsesDefaultPort()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost"
            };
            using (IConnection conn = cf.CreateConnection()){
                Assert.AreEqual(5672, conn.Endpoint.Port);
            }
        }

        [Test]
        public void TestCreateConnectionUsesDefaultMaxMessageSize()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "localhost"
            };

            Assert.AreEqual(0, cf.MaxMessageSize);
            Assert.AreEqual(0, cf.Endpoint.MaxMessageSize);

            using (IConnection conn = cf.CreateConnection())
            {
                Assert.AreEqual(0, conn.Endpoint.MaxMessageSize);
            }
        }

        [Test]
        public void TestCreateConnectionWithoutAutoRecoverySelectsAHostFromTheList()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = false,
                HostName = "not_localhost"
            };
            IConnection conn = cf.CreateConnection(new System.Collections.Generic.List<string> { "localhost" }, "oregano");
            conn.Close();
            conn.Dispose();
            Assert.AreEqual("not_localhost", cf.HostName);
            Assert.AreEqual("localhost", conn.Endpoint.HostName);
        }

        [Test]
        public void TestCreateConnectionWithAutoRecoveryUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
        public void TestCreateConnectionWithAutoRecoveryUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true
            };
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.That(() =>
                    {
                        using(IConnection conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })){}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectionUsesAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory
            {
                HostName = "not_localhost",
                Port = 1234
            };
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
        }

        [Test]
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
            using(IConnection conn = cf.CreateConnection()){};
        }

        [Test]
        public void TestCreateConnectionUsesInvalidAmqpTcpEndpoint()
        {
            var cf = new ConnectionFactory();
            var ep = new AmqpTcpEndpoint("localhost", 1234);
            Assert.That(() =>
                    {
                        using(IConnection conn = cf.CreateConnection(new System.Collections.Generic.List<AmqpTcpEndpoint> { ep })) {}
                    }, Throws.TypeOf<BrokerUnreachableException>());
        }

        [Test]
        public void TestCreateConnectioUsesValidEndpointWhenMultipleSupplied()
        {
            var cf = new ConnectionFactory();
            var invalidEp = new AmqpTcpEndpoint("not_localhost");
            var ep = new AmqpTcpEndpoint("localhost");
            using(IConnection conn = cf.CreateConnection(new List<AmqpTcpEndpoint> { invalidEp, ep })) {};
        }

        [Test]
        public void TestCreateConnectionTwiceDoesntClearAuthMechanisms()
        {
            var cf = new ConnectionFactory();
            cf.AuthMechanisms.Clear();
            var cf2 = new ConnectionFactory();
            Assert.That(cf2.AuthMechanisms.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestCredentialsProvider()
        {
            var cf = new ConnectionFactory();

            Assert.AreEqual(ConnectionFactory.DefaultUser, cf.UserName);
            Assert.AreEqual(ConnectionFactory.DefaultPass, cf.Password);
            Assert.AreEqual(ConnectionFactory.DefaultUser, cf.CredentialsProvider.UserName);
            Assert.AreEqual(ConnectionFactory.DefaultPass, cf.CredentialsProvider.Password);

            string newUserName = Guid.NewGuid().ToString();
            cf.UserName = newUserName;

            Assert.AreEqual(newUserName, cf.UserName);
            Assert.AreEqual(ConnectionFactory.DefaultPass, cf.Password);
            Assert.AreEqual(newUserName, cf.CredentialsProvider.UserName);
            Assert.AreEqual(ConnectionFactory.DefaultPass, cf.CredentialsProvider.Password);

            var expected = Encoding.UTF8.GetBytes($"\0{newUserName}\0{ConnectionFactory.DefaultPass}");
            IAuthMechanism pm = new PlainMechanism();
            var actual = pm.handleChallenge(null, cf);
            Assert.AreEqual(expected, actual);

            string newPassword = Guid.NewGuid().ToString();
            cf.Password = newPassword;

            Assert.AreEqual(newUserName, cf.UserName);
            Assert.AreEqual(newPassword, cf.Password);
            Assert.AreEqual(newUserName, cf.CredentialsProvider.UserName);
            Assert.AreEqual(newPassword, cf.CredentialsProvider.Password);

            expected = Encoding.UTF8.GetBytes($"\0{newUserName}\0{newPassword}");
            pm = new PlainMechanism();
            actual = pm.handleChallenge(null, cf);
            Assert.AreEqual(expected, actual);

            newUserName = Guid.NewGuid().ToString();
            newPassword = Guid.NewGuid().ToString();
            string clientProvidedName = Guid.NewGuid().ToString();

            ICredentialsProvider p = new BasicCredentialsProvider(clientProvidedName, newUserName, newPassword);
            cf.CredentialsProvider = p;
            cf.ClientProvidedName = clientProvidedName;

            Assert.AreEqual(clientProvidedName, cf.ClientProvidedName);
            Assert.AreEqual(newUserName, cf.UserName);
            Assert.AreEqual(newPassword, cf.Password);
            Assert.AreEqual(clientProvidedName, cf.CredentialsProvider.Name);
            Assert.AreEqual(newUserName, cf.CredentialsProvider.UserName);
            Assert.AreEqual(newPassword, cf.CredentialsProvider.Password);
        }
    }
}

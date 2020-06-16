// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestSsl
    {
        public async ValueTask SendReceive(ConnectionFactory cf)
        {
            using (IConnection conn = await cf.CreateConnection())
            {
                IModel ch = await conn.CreateModel();

                await ch.ExchangeDeclare("Exchange_TestSslEndPoint", ExchangeType.Direct);
                string qName = await ch.QueueDeclare();
                await ch.QueueBind(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null);

                string message = "Hello C# SSL Client World";
                byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(message);
                await ch.BasicPublish("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null, msgBytes);

                bool autoAck = false;
                BasicGetResult result = await ch.BasicGet(qName, autoAck);
                byte[] body = result.Body.ToArray();
                string resultMessage = System.Text.Encoding.UTF8.GetString(body);

                Assert.AreEqual(message, resultMessage);
            }
        }

        [Test]
        public async ValueTask TestServerVerifiedIgnoringNameMismatch()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = "*";
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
            cf.Ssl.Enabled = true;
            await SendReceive(cf);
        }

        [Test]
        public async ValueTask TestServerVerified()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = System.Net.Dns.GetHostName();
            cf.Ssl.Enabled = true;
            await SendReceive(cf);
        }

        [Test]
        public async ValueTask TestClientAndServerVerified()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = System.Net.Dns.GetHostName();
            Assert.IsNotNull(sslDir);
            cf.Ssl.CertPath = $"{sslDir}/client/keycert.p12";
            string p12Password = Environment.GetEnvironmentVariable("PASSWORD");
            Assert.IsNotNull(p12Password, "missing PASSWORD env var");
            cf.Ssl.CertPassphrase = p12Password;
            cf.Ssl.Enabled = true;
            await SendReceive(cf);
        }

        // rabbitmq/rabbitmq-dotnet-client#46, also #44 and #45
        [Test]
        public async ValueTask TestNoClientCertificate()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return;
            }

            ConnectionFactory cf = new ConnectionFactory
            {
                Ssl = new SslOption()
                {
                    CertPath = null,
                    Enabled = true,
                }
            };

            cf.Ssl.Version = SslProtocols.None;
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNotAvailable |
                                        SslPolicyErrors.RemoteCertificateNameMismatch;

            await SendReceive(cf);
        }
    }
}

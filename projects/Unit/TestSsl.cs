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
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.client.impl.Channel;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestSsl
    {
        public async Task SendReceiveAsync(ConnectionFactory cf)
        {
            using (IConnection conn = cf.CreateConnection())
            await using (IChannel ch = await conn.CreateChannelAsync().ConfigureAwait(false))
            {
                await ch.DeclareExchangeAsync("Exchange_TestSslEndPoint", ExchangeType.Direct).ConfigureAwait(false);
                string qName = (await ch.DeclareQueueAsync().ConfigureAwait(false)).QueueName;
                await ch.BindQueueAsync(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint").ConfigureAwait(false);

                string message = "Hello C# SSL Client World";
                byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(message);
                await ch.PublishMessageAsync("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null, msgBytes);

                SingleMessageRetrieval? result = await ch.RetrieveSingleMessageAsync(qName, false).ConfigureAwait(false);
                string resultMessage = System.Text.Encoding.UTF8.GetString(result?.Body.ToArray() ?? Array.Empty<byte>());

                Assert.AreEqual(message, resultMessage);
            }
        }

        [Test]
        public Task TestServerVerifiedIgnoringNameMismatch()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return Task.CompletedTask;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = "*";
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
            cf.Ssl.Enabled = true;
            return SendReceiveAsync(cf);
        }

        [Test]
        public Task TestServerVerified()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return Task.CompletedTask;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = System.Net.Dns.GetHostName();
            cf.Ssl.Enabled = true;
            return SendReceiveAsync(cf);
        }

        [Test]
        public Task TestClientAndServerVerified()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return Task.CompletedTask;
            }

            ConnectionFactory cf = new ConnectionFactory();
            cf.Ssl.ServerName = System.Net.Dns.GetHostName();
            Assert.IsNotNull(sslDir);
            cf.Ssl.CertPath = $"{sslDir}/client/keycert.p12";
            string p12Password = Environment.GetEnvironmentVariable("PASSWORD");
            Assert.IsNotNull(p12Password, "missing PASSWORD env var");
            cf.Ssl.CertPassphrase = p12Password;
            cf.Ssl.Enabled = true;
            return SendReceiveAsync(cf);
        }

        // rabbitmq/rabbitmq-dotnet-client#46, also #44 and #45
        [Test]
        public Task TestNoClientCertificate()
        {
            string sslDir = IntegrationFixture.CertificatesDirectory();
            if (null == sslDir)
            {
                Console.WriteLine("SSL_CERT_DIR is not configured, skipping test");
                return Task.CompletedTask;
            }

            ConnectionFactory cf = new ConnectionFactory
            {
                Ssl = new SslOption
                {
                    CertPath = null,
                    Enabled = true,
                }
            };

            cf.Ssl.Version = SslProtocols.None;
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch;

            return SendReceiveAsync(cf);
        }
    }
}

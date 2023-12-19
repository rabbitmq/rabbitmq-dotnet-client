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

using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestSsl : IntegrationFixture
    {
        private readonly SslEnv _sslEnv;

        public TestSsl(ITestOutputHelper output) : base(output)
        {
            _sslEnv = new SslEnv();
        }

        protected override void SetUp()
        {
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
        }

        [SkippableFact]
        public void TestServerVerifiedIgnoringNameMismatch()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = "*";
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
            cf.Ssl.Enabled = true;

            SendReceive(cf);
        }

        [SkippableFact]
        public void TestServerVerified()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = _sslEnv.Hostname;
            cf.Ssl.Enabled = true;

            SendReceive(cf);
        }

        [SkippableFact]
        public void TestClientAndServerVerified()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            string certPath = _sslEnv.CertPath;
            Assert.True(File.Exists(certPath));

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = _sslEnv.Hostname;
            cf.Ssl.CertPath = certPath;
            cf.Ssl.CertPassphrase = _sslEnv.CertPassphrase;
            cf.Ssl.Enabled = true;

            SendReceive(cf);
        }

        // rabbitmq/rabbitmq-dotnet-client#46, also #44 and #45
        [SkippableFact]
        public void TestNoClientCertificate()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl = new SslOption()
            {
                CertPath = null,
                Enabled = true,
                ServerName = _sslEnv.Hostname,
                Version = SslProtocols.None,
                AcceptablePolicyErrors =
                    SslPolicyErrors.RemoteCertificateNotAvailable |
                    SslPolicyErrors.RemoteCertificateNameMismatch
            };

            SendReceive(cf);
        }

        private void SendReceive(ConnectionFactory connectionFactory)
        {
            using (IConnection conn = CreateConnectionWithRetries(connectionFactory))
            {
                using (IChannel ch = conn.CreateChannel())
                {
                    ch.ExchangeDeclare("Exchange_TestSslEndPoint", ExchangeType.Direct);
                    string qName = ch.QueueDeclare();
                    ch.QueueBind(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null);

                    string message = "Hello C# SSL Client World";
                    byte[] msgBytes = _encoding.GetBytes(message);
                    ch.BasicPublish("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", msgBytes);

                    bool autoAck = false;
                    BasicGetResult result = ch.BasicGet(qName, autoAck);
                    byte[] body = result.Body.ToArray();
                    string resultMessage = _encoding.GetString(body);

                    Assert.Equal(message, resultMessage);
                }
            }
        }
    }
}

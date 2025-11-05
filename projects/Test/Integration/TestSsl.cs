// This source code is dual-licensed under the Apache License, version
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

using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
            Assert.True(File.Exists(_sslEnv.CertDirectPath));
        }

        public override Task InitializeAsync()
        {
            Assert.Null(_connFactory);
            Assert.Null(_conn);
            Assert.Null(_channel);
            return Task.CompletedTask;
        }

        [SkippableFact]
        public async Task TestServerVerifiedIgnoringNameMismatch()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = "*";
            cf.Ssl.CertPath = _sslEnv.CertDirectPath;
            cf.Ssl.CertPassphrase = _sslEnv.CertPassphrase;
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
            cf.Ssl.Enabled = true;

            await SendReceiveAsync(cf);
        }

        [SkippableFact]
        public async Task TestServerVerified()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = _sslEnv.Hostname;
            cf.Ssl.CertPath = _sslEnv.CertDirectPath;
            cf.Ssl.CertPassphrase = _sslEnv.CertPassphrase;
            cf.Ssl.Enabled = true;

            await SendReceiveAsync(cf);
        }

        [SkippableFact]
        public async Task TestClientAndServerVerified()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.ServerName = _sslEnv.Hostname;
            cf.Ssl.CertPath = _sslEnv.CertDirectPath;
            cf.Ssl.CertPassphrase = _sslEnv.CertPassphrase;
            cf.Ssl.Enabled = true;

            await SendReceiveAsync(cf);
        }

        [SkippableFact]
        public async Task TestWithClientCertificate()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl = new SslOption()
            {
                CertPath = _sslEnv.CertDirectPath,
                CertPassphrase = _sslEnv.CertPassphrase,
                Enabled = true,
                ServerName = _sslEnv.Hostname,
                Version = SslProtocols.None,
                AcceptablePolicyErrors =
                    SslPolicyErrors.RemoteCertificateNotAvailable |
                    SslPolicyErrors.RemoteCertificateNameMismatch
            };

            await SendReceiveAsync(cf);
        }

#if NET
        [SkippableFact]
        public async Task TestWithClientCertificateSignedByIntermediate()
        {
            Skip.IfNot(_sslEnv.IsSslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            Assert.True(File.Exists(_sslEnv.CertIntermediatePath));

            X509Certificate2 clientCertificate = new(_sslEnv.CertIntermediatePath, _sslEnv.CertPassphrase);
            X509Certificate2 intermediateCaCertificate = new(_sslEnv.CertIntermediateCaPath);
            X509Certificate2Collection intermediateCertificates = new(intermediateCaCertificate);

            ConnectionFactory cf = CreateConnectionFactory();
            cf.Port = 5671;
            cf.Ssl.Enabled = true;
            cf.Ssl.ClientCertificateContext = SslStreamCertificateContext.Create(clientCertificate, intermediateCertificates);
            cf.Ssl.ServerName = _sslEnv.Hostname;
            cf.Ssl.AcceptablePolicyErrors =
                SslPolicyErrors.RemoteCertificateNotAvailable |
                SslPolicyErrors.RemoteCertificateNameMismatch;

            await SendReceiveAsync(cf);
        }
#endif

        private async Task SendReceiveAsync(ConnectionFactory connectionFactory)
        {
            await using IConnection conn = await CreateConnectionAsyncWithRetries(connectionFactory);
            await using IChannel ch = await conn.CreateChannelAsync();
            await ch.ExchangeDeclareAsync("Exchange_TestSslEndPoint", ExchangeType.Direct);

            string qName = await ch.QueueDeclareAsync();
            await ch.QueueBindAsync(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint");

            string message = "Hello C# SSL Client World";
            byte[] msgBytes = _encoding.GetBytes(message);
            await ch.BasicPublishAsync("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", msgBytes);

            bool autoAck = false;
            BasicGetResult result = await ch.BasicGetAsync(qName, autoAck);
            byte[] body = result.Body.ToArray();
            string resultMessage = _encoding.GetString(body);

            Assert.Equal(message, resultMessage);

            await ch.CloseAsync();
        }
    }
}

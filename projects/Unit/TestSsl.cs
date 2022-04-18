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
using System.IO;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;

using Xunit;
using Xunit.Abstractions;

namespace RabbitMQ.Client.Unit
{
    [Collection("IntegrationFixture")]
    public class TestSsl
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDisplayName;
        private readonly string _sslDir;
        private readonly string _certPassphrase;
        private readonly bool _sslConfigured;

        public TestSsl(ITestOutputHelper output)
        {
            _output = output;
            var type = _output.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            var test = (ITest)testMember.GetValue(output);
            _testDisplayName = test.DisplayName;

            _sslDir = IntegrationFixture.CertificatesDirectory();
            _certPassphrase = Environment.GetEnvironmentVariable("PASSWORD");

            _sslConfigured = Directory.Exists(_sslDir) &&
                (false == string.IsNullOrEmpty(_certPassphrase));
        }

        [SkippableFact]
        public void TestServerVerifiedIgnoringNameMismatch()
        {
            Skip.IfNot(_sslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = new ConnectionFactory { Port = 5671 };
            cf.Ssl.ServerName = "*";
            cf.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch;
            cf.Ssl.Enabled = true;
            SendReceive(cf);
        }

        [SkippableFact]
        public void TestServerVerified()
        {
            Skip.IfNot(_sslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = new ConnectionFactory { Port = 5671 };
            cf.Ssl.ServerName = System.Net.Dns.GetHostName();
            cf.Ssl.Enabled = true;
            SendReceive(cf);
        }

        [SkippableFact]
        public void TestClientAndServerVerified()
        {
            Skip.IfNot(_sslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            string hostName = System.Net.Dns.GetHostName();
            ConnectionFactory cf = new ConnectionFactory { Port = 5671 };
            cf.Ssl.ServerName = hostName;
            cf.Ssl.CertPath = $"{_sslDir}/client_{hostName}_key.p12";
            cf.Ssl.CertPassphrase = _certPassphrase;
            cf.Ssl.Enabled = true;
            SendReceive(cf);
        }

        // rabbitmq/rabbitmq-dotnet-client#46, also #44 and #45
        [SkippableFact]
        public void TestNoClientCertificate()
        {
            Skip.IfNot(_sslConfigured, "SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");

            ConnectionFactory cf = new ConnectionFactory
            {
                Port = 5671,
                Ssl = new SslOption()
                {
                    CertPath = null,
                    Enabled = true,
                    ServerName = "localhost",
                    Version = SslProtocols.None,
                    AcceptablePolicyErrors =
                        SslPolicyErrors.RemoteCertificateNotAvailable |
                        SslPolicyErrors.RemoteCertificateNameMismatch
                }
            };

            SendReceive(cf);
        }

        private void SendReceive(ConnectionFactory cf)
        {
            using (IConnection conn = cf.CreateConnection($"{_testDisplayName}:{Guid.NewGuid()}"))
            {
                using (IModel ch = conn.CreateModel())
                {
                    ch.ExchangeDeclare("Exchange_TestSslEndPoint", ExchangeType.Direct);
                    string qName = ch.QueueDeclare();
                    ch.QueueBind(qName, "Exchange_TestSslEndPoint", "Key_TestSslEndpoint", null);

                    string message = "Hello C# SSL Client World";
                    byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(message);
                    ch.BasicPublish("Exchange_TestSslEndPoint", "Key_TestSslEndpoint", msgBytes);

                    bool autoAck = false;
                    BasicGetResult result = ch.BasicGet(qName, autoAck);
                    byte[] body = result.Body.ToArray();
                    string resultMessage = System.Text.Encoding.UTF8.GetString(body);

                    Assert.Equal(message, resultMessage);
                }
            }
        }
    }
}

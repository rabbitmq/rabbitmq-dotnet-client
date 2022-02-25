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
using System.Collections.Generic;
using System.IO;
using System.Threading;

using NUnit.Framework;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestHeartbeats : IntegrationFixture
    {
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(2);

        public TestHeartbeats() : base()
        {
        }

        [Test, Category("LongRunning"), MaxTimeAttribute(35000)]
        public void TestThatHeartbeatWriterUsesConfigurableInterval()
        {
            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = _heartbeatTimeout,
                AutomaticRecoveryEnabled = false
            };
            RunSingleConnectionTest(cf);
        }

        [Test]
        public void TestThatHeartbeatWriterWithTLSEnabled()
        {
            if (!LongRunningTestsEnabled())
            {
                Assert.Ignore("RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");
            }

            var cf = new ConnectionFactory()
            {
                Port = 5671,
                RequestedHeartbeat = _heartbeatTimeout,
                AutomaticRecoveryEnabled = false
            };

            string sslDir = IntegrationFixture.CertificatesDirectory();
            string certPassphrase = Environment.GetEnvironmentVariable("PASSWORD");
            bool sslConfigured = Directory.Exists(sslDir) &&
                (false == string.IsNullOrEmpty(certPassphrase));
            if (false == sslConfigured)
            {
                Assert.Ignore("SSL_CERTS_DIR and/or PASSWORD are not configured, skipping test");
            }

            string hostName = System.Net.Dns.GetHostName();
            cf.Ssl.ServerName = hostName;
            cf.Ssl.CertPath = $"{sslDir}/client_{hostName}_key.p12";
            cf.Ssl.CertPassphrase = certPassphrase;
            cf.Ssl.Enabled = true;

            RunSingleConnectionTest(cf);
        }

        [Test, Category("LongRunning"), MaxTimeAttribute(90000)]
        public void TestHundredsOfConnectionsWithRandomHeartbeatInterval()
        {
            var rnd = new Random();
            List<IConnection> xs = new List<IConnection>();
            // Since we are using the ThreadPool, let's set MinThreads to a high-enough value.
            ThreadPool.SetMinThreads(200, 200);
            for (int i = 0; i < 200; i++)
            {
                ushort n = Convert.ToUInt16(rnd.Next(2, 6));
                var cf = new ConnectionFactory()
                {
                    RequestedHeartbeat = TimeSpan.FromSeconds(n),
                    AutomaticRecoveryEnabled = false
                };
                IConnection conn = cf.CreateConnection();
                xs.Add(conn);
                IModel ch = conn.CreateModel();

                conn.ConnectionShutdown += (sender, evt) =>
                    {
                        CheckInitiator(evt);
                    };
            }

            SleepFor(60);

            foreach (IConnection x in xs)
            {
                x.Close();
            }
        }

        protected void RunSingleConnectionTest(ConnectionFactory cf)
        {
            IConnection conn = cf.CreateConnection();
            IModel ch = conn.CreateModel();
            bool wasShutdown = false;

            conn.ConnectionShutdown += (sender, evt) =>
            {
                lock (conn)
                {
                    if (InitiatedByPeerOrLibrary(evt))
                    {
                        CheckInitiator(evt);
                        wasShutdown = true;
                    }
                }
            };
            SleepFor(30);

            Assert.IsFalse(wasShutdown, "shutdown event should not have been fired");
            Assert.IsTrue(conn.IsOpen, "connection should be open");

            conn.Close();
        }

        private void CheckInitiator(ShutdownEventArgs evt)
        {
            if (InitiatedByPeerOrLibrary(evt))
            {
                Console.WriteLine(((Exception)evt.Cause).StackTrace);
                string s = string.Format("Shutdown: {0}, initiated by: {1}",
                                      evt, evt.Initiator);
                Console.WriteLine(s);
                Assert.Fail(s);
            }
        }

        private bool LongRunningTestsEnabled()
        {
            string s = Environment.GetEnvironmentVariable("RABBITMQ_LONG_RUNNING_TESTS");
            if (s == null || s.Equals(""))
            {
                return false;
            }
            return true;
        }

        private void SleepFor(int t)
        {
            Console.WriteLine("Testing heartbeats, sleeping for {0} seconds", t);
            Thread.Sleep(t * 1000);
        }
    }
}

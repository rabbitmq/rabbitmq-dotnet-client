// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2014 GoPivotal, Inc.
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
//  The Initial Developer of the Original Code is GoPivotal, Inc.
//  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    internal class TestHeartbeats : IntegrationFixture
    {
        private const UInt16 heartbeatTimeout = 2;

        [Test]
        [Category("Focus")]
        public void TestThatHeartbeatWriterUsesConfigurableInterval()
        {
            if (!LongRunningTestsEnabled())
            {
                Console.WriteLine("RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");
                return;
            }

            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = heartbeatTimeout,
                AutomaticRecoveryEnabled = false
            };
            var conn = cf.CreateConnection();
            var ch = conn.CreateModel();
            bool wasShutdown = false;

            conn.ConnectionShutdown += (sender, evt) =>
            {
                lock (conn)
                {
                    if(InitiatedByPeerOrLibrary(evt))
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

        [Test]
        [Category("Focus")]
        public void TestHundredsOfConnectionsWithRandomHeartbeatInterval()
        {
            if (!LongRunningTestsEnabled())
            {
                Console.WriteLine("RABBITMQ_LONG_RUNNING_TESTS is not set, skipping test");
                return;
            }


            var rnd = new Random();
            List<IConnection> xs = new List<IConnection>();
            for(var i = 0; i < 200; i++)
            {
                var n = Convert.ToUInt16(rnd.Next(2, 6));
                var cf = new ConnectionFactory() { RequestedHeartbeat = n, AutomaticRecoveryEnabled = false };
                var conn = cf.CreateConnection();
                xs.Add(conn);
                var ch = conn.CreateModel();
                bool wasShutdown = false;

                conn.ConnectionShutdown += (sender, evt) =>
                    {
                        CheckInitiator(evt);
                    };
            }

            SleepFor(60);

            foreach(var x in xs)
            {
                x.Close();
            }

        }

        private void CheckInitiator(ShutdownEventArgs evt)
        {
            if(InitiatedByPeerOrLibrary(evt))
            {
                var s = String.Format("Shutdown: {0}, initiated by: {1}",
                                      evt, evt.Initiator);
                Console.WriteLine(s);
                Assert.Fail(s);
            }
        }

        private bool LongRunningTestsEnabled()
        {
            var s = Environment.GetEnvironmentVariable("RABBITMQ_LONG_RUNNING_TESTS");
            if(s == null || s.Equals(""))
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
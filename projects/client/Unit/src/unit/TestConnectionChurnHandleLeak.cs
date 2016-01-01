// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2016 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2016 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;
using RabbitMQ.Client.Exceptions;
using System;
using System.Diagnostics;
using System.Threading;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionChurnHandleLeak : IntegrationFixture
    {
        [Test]
        public void TestHandleLeakWithDisabledHeartbeats()
        {
            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = 0
            };
            PerformLeakTest(cf);
        }

        [Test]
        public void TestHandleLeakWithEnabledHeartbeats()
        {
            var cf = new ConnectionFactory()
            {
                RequestedHeartbeat = 16
            };
            PerformLeakTest(cf);
        }

        protected void PerformLeakTest(ConnectionFactory cf)
        {
            var me = Process.GetCurrentProcess();
            var n = me.HandleCount;
            Console.WriteLine("{0} handles before the test...", me.HandleCount);
            for (var i = 0; i < 1000; i++)
            {
                var conn = cf.CreateConnection();
                conn.Close();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(TimeSpan.FromSeconds(10));
            me = Process.GetCurrentProcess();
            Console.WriteLine("{0} handles after the test...", me.HandleCount);
            // allow for a 20% margin of error, as GC behaviour and native handle
            // release is difficult to predict
            Assert.That(me.HandleCount, Is.LessThanOrEqualTo(n + 200));
        }
    }
}

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

using System;
using System.Text;
using System.Threading;

using RabbitMQ.Client.Framing.Impl.v0_9_1;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture {
        [SetUp]
        public new void Init()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            connFactory.AutomaticRecoveryEnabled = true;
            Conn = connFactory.CreateConnection();
            Model = Conn.CreateModel();
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        //
        // Implementation
        // 

        protected void CloseAndWaitForRecovery()
        {
            CloseAndWaitForRecovery((AutorecoveringConnection)this.Conn);
        }

        protected void CloseAndWaitForRecovery(AutorecoveringConnection conn)
        {
            object latch = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(latch);
        }

        protected object PrepareForShutdown(AutorecoveringConnection conn)
        {
            object latch = new object();
            conn.ConnectionShutdown += (c, args) =>
            {
                lock (latch)
                {
                    Monitor.PulseAll(latch);
                }
            };

            return latch;
        }

        protected object PrepareForRecovery(AutorecoveringConnection conn)
        {
            object latch = new object();
            conn.Recovery += (c) =>
            {
                lock (latch)
                {
                    Monitor.PulseAll(latch);
                }
            };

            return latch;
        }

        protected void Wait(object latch)
        {
            lock (latch)
            {
                Monitor.Wait(latch, TimeSpan.FromSeconds(8));
            }
        }
            
    }
}
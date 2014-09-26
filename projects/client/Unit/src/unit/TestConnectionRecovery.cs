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

using RabbitMQ.Client.Impl;
using RabbitMQ.Client.Framing.Impl;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConnectionRecovery : IntegrationFixture {
        [SetUp]
        public new void Init()
        {
            ConnectionFactory connFactory = new ConnectionFactory();
            connFactory.AutomaticRecoveryEnabled = true;
            Conn = (AutorecoveringConnection)connFactory.CreateConnection();
            Model = Conn.CreateModel();
        }

        [Test]
        public void TestBasicConnectionRecovery()
        {
            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnConnection()
        {
            Int32 counter = 0;
            Conn.ConnectionShutdown += (c, args) =>
            {
                Interlocked.Increment(ref counter);
            };

            Assert.IsTrue(Conn.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnConnection()
        {
            Int32 counter = 0;
            ((AutorecoveringConnection)Conn).Recovery += (c) =>
            {
                Interlocked.Increment(ref counter);
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Conn.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestBlockedListenersRecovery()
        {
            var latch = new AutoResetEvent(false);
            Conn.ConnectionBlocked += (c, reason) =>
            {
                latch.Set();
            };
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Wait(latch);

            Unblock();
        }

        [Test]
        public void TestUnblockedListenersRecovery()
        {
            var latch = new AutoResetEvent(false);
            Conn.ConnectionUnblocked += (c) =>
            {
                latch.Set();
            };
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();

            Block();
            Unblock();
            Wait(latch);
        }

        [Test]
        public void TestBasicModelRecovery()
        {
            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);
        }

        [Test]
        public void TestShutdownEventHandlersRecoveryOnModel()
        {
            Int32 counter = 0;
            Model.ModelShutdown += (c, args) =>
            {
                Interlocked.Increment(ref counter);
            };

            Assert.IsTrue(Model.IsOpen);
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
        }

        [Test]
        public void TestRecoveryEventHandlersOnModel()
        {
            Int32 counter = 0;
            ((AutorecoveringModel)Model).Recovery += (m) =>
            {
                Interlocked.Increment(ref counter);
            };

            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            CloseAndWaitForRecovery();
            Assert.IsTrue(Model.IsOpen);

            Assert.IsTrue(counter >= 3);
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
            var sl = PrepareForShutdown(conn);
            var rl = PrepareForRecovery(conn);
            CloseConnection(conn);
            Wait(sl);
            Wait(rl);
        }

        protected AutoResetEvent PrepareForShutdown(AutorecoveringConnection conn)
        {
            var latch = new AutoResetEvent(false);
            conn.ConnectionShutdown += (c, args) =>
            {
                latch.Set();
            };

            return latch;
        }

        protected AutoResetEvent PrepareForRecovery(AutorecoveringConnection conn)
        {
            var latch = new AutoResetEvent(false);
            conn.Recovery += (c) =>
            {
                latch.Set();
            };

            return latch;
        }

        protected void Wait(AutoResetEvent latch)
        {
            Assert.IsTrue(latch.WaitOne(TimeSpan.FromSeconds(8)));
        }

        protected override void ReleaseResources()
        {
            Unblock();
        }
    }
}
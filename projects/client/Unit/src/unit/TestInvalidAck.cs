// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2013 GoPivotal, Inc.
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
using System.Diagnostics;

using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestInvalidAck : IntegrationFixture {

        UTF8Encoding enc = new UTF8Encoding();

        [Test]
        public void TestAckWithUnknownConsumerTagAndMultipleFalse()
        {
            object o = new Object();
            bool shutdownFired = false;
            ShutdownEventArgs shutdownArgs = null;
            Model.ModelShutdown += (s, args) =>
            {
                shutdownFired = true;
                shutdownArgs = args;
                Monitor.PulseAll(o);
            };

            Model.BasicAck(123456, false);
            WaitOn(o);
            Assert.IsTrue(shutdownFired);
            AssertPreconditionFailed(shutdownArgs);
        }

        [Test]
        public void TestDoubleAck()
        {
            object o = new Object();
            bool shutdownFired = false;
            ShutdownEventArgs shutdownArgs = null;
            Model.ModelShutdown += (s, args) =>
            {
                shutdownFired = true;
                shutdownArgs = args;
                Monitor.PulseAll(o);
            };
            string q = PrepareNonEmptyQueue(Model);

            BasicGetResult res = Model.BasicGet(q, false);
            Assert.AreEqual(res.DeliveryTag, 1);
            Model.BasicAck(res.DeliveryTag, false);
            Model.BasicAck(res.DeliveryTag, false);

            WaitOn(o);
            Assert.IsTrue(shutdownFired);
            AssertPreconditionFailed(shutdownArgs);
        }

        [Test]
        public void TestDoubleNack()
        {
            object o = new Object();
            bool shutdownFired = false;
            ShutdownEventArgs shutdownArgs = null;
            Model.ModelShutdown += (s, args) =>
            {
                shutdownFired = true;
                shutdownArgs = args;
                Monitor.PulseAll(o);
            };
            string q = PrepareNonEmptyQueue(Model);

            BasicGetResult res = Model.BasicGet(q, false);
            Assert.AreEqual(res.DeliveryTag, 1);
            Model.BasicNack(res.DeliveryTag, false, true);
            Model.BasicNack(res.DeliveryTag, false, true);

            WaitOn(o);
            Assert.IsTrue(shutdownFired);
            AssertPreconditionFailed(shutdownArgs);
        }

        protected string PrepareNonEmptyQueue(IModel m)
        {
            string q = m.QueueDeclare();
            m.BasicPublish("", q, null, enc.GetBytes("hello"));

            return q;
        }
    }
}

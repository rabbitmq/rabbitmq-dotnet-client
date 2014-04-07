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
    public class TestEventingConsumer : IntegrationFixture {

        [Test]
        public void TestEventingConsumerRegistrationEvents()
        {
            string q = Model.QueueDeclare();

            bool registeredInvoked = false;
            object registeredSender = null;
            bool unregisteredInvoked = false;
            object unregisteredSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(Model);
            ec.Registered += (s, args) =>
            {
                registeredInvoked = true;
                registeredSender = s;
            };

            ec.Unregistered += (s, args) =>
            {
                unregisteredInvoked = true;
                unregisteredSender = s;
            };

            string tag = Model.BasicConsume(q, false, ec);

            Assert.IsTrue(registeredInvoked);
            Assert.IsNotNull(registeredSender);
            Assert.AreEqual(ec, registeredSender);
            Assert.AreEqual(Model, ((EventingBasicConsumer)registeredSender).Model);

            Model.BasicCancel(tag);
            Assert.IsTrue(unregisteredInvoked);
            Assert.IsNotNull(unregisteredSender);
            Assert.AreEqual(ec, unregisteredSender);
            Assert.AreEqual(Model, ((EventingBasicConsumer)unregisteredSender).Model);
        }

        [Test]
        public void TestEventingConsumerDeliveryEvents()
        {
            string q = Model.QueueDeclare();
            object o = new Object ();

            bool receivedInvoked = false;
            object receivedSender = null;

            EventingBasicConsumer ec = new EventingBasicConsumer(Model);
            ec.Received += (s, args) =>
            {
                receivedInvoked = true;
                receivedSender = s;

                Monitor.PulseAll(o);
            };

            Model.BasicConsume(q, true, ec);
            Model.BasicPublish("", q, null, enc.GetBytes("msg"));

            WaitOn(o);
            Assert.IsTrue(receivedInvoked);
            Assert.IsNotNull(receivedSender);
            Assert.AreEqual(ec, receivedSender);
            Assert.AreEqual(Model, ((EventingBasicConsumer)receivedSender).Model);

            bool shutdownInvoked = false;
            object shutdownSender = null;

            ec.Shutdown += (s, args) =>
            {
                shutdownInvoked = true;
                shutdownSender = s;

                Monitor.PulseAll(o);
            };

            Model.Close();
            WaitOn(o);

            Assert.IsTrue(shutdownInvoked);
            Assert.IsNotNull(shutdownSender);
            Assert.AreEqual(ec, shutdownSender);
            Assert.AreEqual(Model, ((EventingBasicConsumer)shutdownSender).Model);
        }


        protected void WaitOn(object o)
        {
            lock(o)
            {
                Monitor.Wait(o, TimeSpan.FromSeconds(4));
            }
        }
    }
}

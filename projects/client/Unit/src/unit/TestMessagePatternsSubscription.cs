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
using RabbitMQ.Client.MessagePatterns;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestMessagePatternsSubscription : IntegrationFixture {
        [Test]
        public void TestChannelClosureIsObservableOnSubscription()
        {
            string q = Model.QueueDeclare();
            Subscription sub = new Subscription(Model, q, true);

            BasicDeliverEventArgs r1;
            Assert.IsFalse(sub.Next(100, out r1));

            Model.BasicPublish("", q, null, enc.GetBytes("a message"));
            Model.BasicPublish("", q, null, enc.GetBytes("a message"));

            BasicDeliverEventArgs r2;
            Assert.IsTrue(sub.Next(1000, out r2));
            Assert.IsNotNull(sub.Next());

            Model.Close();
            Assert.IsNull(sub.Next());

            BasicDeliverEventArgs r3;
            Assert.IsFalse(sub.Next(100, out r3));
        }
    }
}

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

using System;
using System.Text;
using System.Linq;
using System.Threading;

using RabbitMQ.Client;

namespace RabbitMQ.Client.Unit {
    [TestFixture]
    public class TestConcurrentAccessWithSharedConnection : IntegrationFixture {

        [Test]
        public void TestConcurrentChannelOpenWithPublishing()
        {
            var n = 32;
            var latch = new CountdownEvent(n);
            foreach (var i in Enumerable.Range(0, n))
            {
                var t = new Thread(() => {
                    // publishing on a shared channel is not supported
                    // and would missing the point of this test anyway
                    var ch = Conn.CreateModel();
                    ch.ConfirmSelect();
                    foreach (var j in Enumerable.Range(0, 10000))
                    {
                        var body = Encoding.ASCII.GetBytes(string.Empty);
                        ch.BasicPublish(exchange: "", routingKey: "_______", basicProperties: ch.CreateBasicProperties(), body: body);
                    }
                    ch.WaitForConfirms(TimeSpan.FromSeconds(40));
                    latch.Signal();
                });
                t.Start();                
            }

            Assert.IsTrue(latch.Wait(TimeSpan.FromSeconds(90)));
        }
    }
}

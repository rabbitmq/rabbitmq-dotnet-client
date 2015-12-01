// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 1.1.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (C) 2007-2015 Pivotal Software, Inc.
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
//  Copyright (c) 2007-2015 Pivotal Software, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestFloodPublishing : IntegrationFixture
    {
        [SetUp]
        public override void Init()
        {
            var connFactory = new ConnectionFactory()
            {
                RequestedHeartbeat = 60,
                AutomaticRecoveryEnabled = false
            };
            Conn = connFactory.CreateConnection();
            Model = Conn.CreateModel();
        }

        [Test, Category("LongRunning")]
        public void TestUnthrottledFloodPublishing()
        {
            Conn.ConnectionShutdown += (_, args) =>
            {
                if (args.Initiator != ShutdownInitiator.Application)
                {
                    Assert.Fail("Unexpected connection shutdown!");
                }
            };

            bool elapsed = false;
            var t = new System.Timers.Timer(1000 * 185);
            t.Elapsed += (_sender, _args) => { elapsed = true; };
            t.AutoReset = false;
            t.Start();

            while (!elapsed)
            {
                Model.BasicPublish("", "", null, new byte[2048]);
            }
            Assert.IsTrue(Conn.IsOpen);
            t.Dispose();
        }
    }
}

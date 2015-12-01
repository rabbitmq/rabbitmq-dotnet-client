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

using System;
using System.Threading;
using NUnit.Framework;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Unit
{
    [TestFixture]
    public class TestConnectionBlocked : IntegrationFixture
    {
        private readonly Object _lockObject = new Object();
        private bool _notified;

        public void HandleBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            Unblock();
        }


        public void HandleUnblocked(object sender, EventArgs ea)
        {
            lock (_lockObject)
            {
                _notified = true;
                Monitor.PulseAll(_lockObject);
            }
        }

        protected override void ReleaseResources()
        {
            Unblock();
        }

        [Test]
        public void TestConnectionBlockedNotification()
        {
            Conn.ConnectionBlocked += HandleBlocked;
            Conn.ConnectionUnblocked += HandleUnblocked;

            Block();
            lock (_lockObject)
            {
                if (!_notified)
                {
                    Monitor.Wait(_lockObject, TimeSpan.FromSeconds(8));
                }
            }
            if (!_notified)
            {
                Unblock();
                Assert.Fail("Unblock notification not received.");
            }
        }
    }
}

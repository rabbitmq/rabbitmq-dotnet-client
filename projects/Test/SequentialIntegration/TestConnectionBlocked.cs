// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.SequentialIntegration
{
    public class TestConnectionBlocked : SequentialIntegrationFixture
    {
        private readonly ManualResetEventSlim _connDisposed = new ManualResetEventSlim(false);
        private readonly object _lockObject = new object();
        private bool _notified;

        public TestConnectionBlocked(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestConnectionBlockedNotification()
        {
            _notified = false;
            _conn.ConnectionBlocked += HandleBlocked;
            _conn.ConnectionUnblocked += HandleUnblocked;

            try
            {
                Block();

                lock (_lockObject)
                {
                    if (!_notified)
                    {
                        Monitor.Wait(_lockObject, TimeSpan.FromSeconds(15));
                    }
                }

                if (!_notified)
                {
                    Assert.Fail("Unblock notification not received.");
                }
            }
            finally
            {
                Unblock();
            }
        }

        [Fact]
        public void TestDisposeOnBlockedConnectionDoesNotHang()
        {
            _notified = false;

            try
            {
                Block();

                Task.Factory.StartNew(DisposeConnection);

                if (!_connDisposed.Wait(TimeSpan.FromSeconds(20)))
                {
                    Assert.Fail("Dispose must have finished within 20 seconds after starting");
                }
            }
            finally
            {
                Unblock();
            }
        }

        protected override void TearDown()
        {
            Unblock();
        }

        private void HandleBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            Unblock();
        }

        private void HandleUnblocked(object sender, EventArgs ea)
        {
            lock (_lockObject)
            {
                _notified = true;
                Monitor.PulseAll(_lockObject);
            }
        }

        private void DisposeConnection()
        {
            _conn.Dispose();
            _connDisposed.Set();
        }
    }
}

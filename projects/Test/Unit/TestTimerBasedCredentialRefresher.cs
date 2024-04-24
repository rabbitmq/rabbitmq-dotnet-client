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
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Test.Unit
{
    public class MockCredentialsProvider : ICredentialsProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly TimeSpan? _validUntil = TimeSpan.FromSeconds(1);
        private Exception _ex = null;
        private bool _refreshCalled = false;

        public MockCredentialsProvider(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public MockCredentialsProvider(ITestOutputHelper testOutputHelper, TimeSpan validUntil)
        {
            _testOutputHelper = testOutputHelper;
            _validUntil = validUntil;
        }

        public bool RefreshCalled
        {
            get
            {
                return _refreshCalled;
            }
        }

        public string Name => this.GetType().Name;

        public string UserName => "guest";

        public string Password
        {
            get
            {
                if (_ex == null)
                {
                    return "guest";
                }
                else
                {
                    throw _ex;
                }
            }
        }

        public TimeSpan? ValidUntil => _validUntil;

        public void Refresh()
        {
            _refreshCalled = true;
        }

        public void PasswordThrows(Exception ex)
        {
            _ex = ex;
        }
    }

    public class TestTimerBasedCredentialsRefresher
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly TimerBasedCredentialRefresher _refresher = new TimerBasedCredentialRefresher();

        public TestTimerBasedCredentialsRefresher(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void TestRegister()
        {
            Task cb(bool unused0, CancellationToken unused1) => Task.CompletedTask;
            ICredentialsProvider credentialsProvider = new MockCredentialsProvider(_testOutputHelper);

            Assert.True(credentialsProvider == _refresher.Register(credentialsProvider, cb, CancellationToken.None));
            Assert.True(_refresher.Unregister(credentialsProvider));
        }

        [Fact]
        public void TestDoNotRegisterWhenHasNoExpiry()
        {
            ICredentialsProvider credentialsProvider = new MockCredentialsProvider(_testOutputHelper, TimeSpan.Zero);
            Task cb(bool unused0, CancellationToken unused1) => Task.CompletedTask;

            _refresher.Register(credentialsProvider, cb, CancellationToken.None);

            Assert.False(_refresher.Unregister(credentialsProvider));
        }

        [Fact]
        public async Task TestRefreshToken()
        {
            var cbtcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool? callbackArg = null;
            var credentialsProvider = new MockCredentialsProvider(_testOutputHelper, TimeSpan.FromSeconds(1));
            Task cb(bool arg, CancellationToken _)
            {
                callbackArg = arg;
                cbtcs.SetResult(true);
                return Task.CompletedTask;
            }

            try
            {
                _refresher.Register(credentialsProvider, cb, CancellationToken.None);

                await cbtcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(await cbtcs.Task);

                Assert.True(credentialsProvider.RefreshCalled);
                Assert.True(callbackArg);
            }
            finally
            {
                Assert.True(_refresher.Unregister(credentialsProvider));
            }
        }

        [Fact]
        public async Task TestRefreshTokenFailed()
        {
            var cbtcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool? callbackArg = null;
            var credentialsProvider = new MockCredentialsProvider(_testOutputHelper, TimeSpan.FromSeconds(1));
            Task cb(bool arg, CancellationToken unused)
            {
                callbackArg = arg;
                cbtcs.SetResult(true);
                return Task.CompletedTask;
            }

            var ex = new Exception();
            credentialsProvider.PasswordThrows(ex);

            try
            {
                _refresher.Register(credentialsProvider, cb, CancellationToken.None);
                await cbtcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(await cbtcs.Task);

                Assert.True(credentialsProvider.RefreshCalled);
                Assert.False(callbackArg);
            }
            finally
            {
                Assert.True(_refresher.Unregister(credentialsProvider));
            }
        }
    }
}

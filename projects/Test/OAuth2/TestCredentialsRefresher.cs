// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.OAuth2;
using Xunit;
using Xunit.Abstractions;

namespace OAuth2Test
{
    public class MockCredentialsProvider : ICredentialsProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly TimeSpan _validUntil;
        private readonly Exception? _maybeGetCredentialsException;

        public MockCredentialsProvider(ITestOutputHelper testOutputHelper, TimeSpan validUntil,
            Exception? maybeGetCredentialsException = null)
        {
            _testOutputHelper = testOutputHelper;
            _validUntil = validUntil;
            _maybeGetCredentialsException = maybeGetCredentialsException;
        }

        public string Name => GetType().Name;

        public Task<Credentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
        {
            if (_maybeGetCredentialsException is null)
            {
                var creds = new Credentials(this.GetType().Name, "guest", "guest", _validUntil);
                return Task.FromResult(creds);
            }
            else
            {
                throw _maybeGetCredentialsException;
            }
        }
    }

    public class TestCredentialsRefresher
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestCredentialsRefresher(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task TestRefreshToken()
        {
            var expectedValidUntil = TimeSpan.FromSeconds(1);
            var tcs = new TaskCompletionSource<Credentials>(TaskCreationOptions.RunContinuationsAsynchronously);
            var credentialsProvider = new MockCredentialsProvider(_testOutputHelper, expectedValidUntil);

            Task cb(Credentials? argCreds, Exception? ex, CancellationToken argToken)
            {
                if (argCreds is null)
                {
                    tcs.SetException(new NullReferenceException("argCreds is null, huh?"));
                }
                else
                {
                    tcs.SetResult(argCreds);
                }

                return Task.CompletedTask;
            }

            Credentials credentials;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                using (CancellationTokenRegistration ctr = cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var credentialRefresher = new CredentialsRefresher(credentialsProvider, cb, cts.Token);
                    credentials = await tcs.Task;
                }
            }

            Assert.Equal(nameof(MockCredentialsProvider), credentials.Name);
            Assert.Equal("guest", credentials.UserName);
            Assert.Equal("guest", credentials.Password);
            Assert.Equal(expectedValidUntil, credentials.ValidUntil);
        }

        [Fact]
        public async Task TestRefreshTokenFailed()
        {
            string exceptionMessage = nameof(TestCredentialsRefresher);
            var expectedException = new Exception(exceptionMessage);

            var expectedValidUntil = TimeSpan.FromSeconds(1);
            var tcs = new TaskCompletionSource<Credentials?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var credentialsProvider = new MockCredentialsProvider(_testOutputHelper, expectedValidUntil, expectedException);

            ushort callbackCount = 0;
            Task cb(Credentials? argCreds, Exception? ex, CancellationToken argToken)
            {
                callbackCount++;

                if (ex != null)
                {
                    tcs.SetException(ex);
                }

                return Task.CompletedTask;
            }

            Credentials? credentials = null;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                using (CancellationTokenRegistration ctr = cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var credentialRefresher = new CredentialsRefresher(credentialsProvider, cb, cts.Token);
                    try
                    {
                        credentials = await tcs.Task;
                    }
                    catch (Exception ex)
                    {
                        Assert.Same(expectedException, ex);
                    }
                }
            }

            Assert.Null(credentials);
            Assert.Equal(1, callbackCount);
        }
    }
}

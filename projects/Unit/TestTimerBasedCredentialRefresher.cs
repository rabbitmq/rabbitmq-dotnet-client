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
using Moq;
using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestTimerBasedCredentialsRefresher
    {
        protected TimerBasedCredentialRefresher _refresher;
        protected Mock<ICredentialsProvider> _credentialsProvider;
        protected Mock<ICredentialsRefresher.NotifyCredentialRefreshed> _callback = new Mock<ICredentialsRefresher.NotifyCredentialRefreshed>();

        public TestTimerBasedCredentialsRefresher()
        {
            _refresher = new TimerBasedCredentialRefresher();
            _credentialsProvider = new Mock<ICredentialsProvider>();
        }

        [Fact]
        public void TestRegister()
        {
            _credentialsProvider.Setup(p => p.ValidUntil).Returns(TimeSpan.FromSeconds(1));
            Assert.True(_credentialsProvider.Object == _refresher.Register(_credentialsProvider.Object, _callback.Object));
            Assert.True(_refresher.Unregister(_credentialsProvider.Object));
        }

        [Fact]
        public void TestDoNotRegisterWhenHasNoExpiry()
        {

            _credentialsProvider.Setup(p => p.ValidUntil).Returns(TimeSpan.Zero);
            _refresher.Register(_credentialsProvider.Object, _callback.Object);
            Assert.False(_refresher.Unregister(_credentialsProvider.Object));
            _credentialsProvider.Verify();
        }

        [Fact]
        public void TestRefreshToken()
        {
            _credentialsProvider.Setup(p => p.ValidUntil).Returns(TimeSpan.FromSeconds(1));
            _credentialsProvider.Setup(p => p.Password).Returns("the-token").Verifiable();
            _callback.Setup(p => p.Invoke(true));
            _refresher.Register(_credentialsProvider.Object, _callback.Object);

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _credentialsProvider.Verify();
            _callback.Verify();
        }

        [Fact]
        public void TestRefreshTokenFailed()
        {
            _credentialsProvider.Setup(p => p.ValidUntil).Returns(TimeSpan.FromSeconds(1));
            _credentialsProvider.SetupSequence(p => p.Password)
                .Returns("the-token")
                .Throws(new Exception());
            _callback.Setup(p => p.Invoke(false));
            _refresher.Register(_credentialsProvider.Object, _callback.Object);

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _credentialsProvider.Verify();
            _callback.Verify();
        }
    }
}

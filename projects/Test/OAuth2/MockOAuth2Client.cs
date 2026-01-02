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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.OAuth2;
using Xunit.Abstractions;

namespace OAuth2Test
{
    public class MockOAuth2Client : IOAuth2Client
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private IToken? _refreshToken;
        private IToken? _requestToken;

        public MockOAuth2Client(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public IToken? RefreshTokenValue
        {
            get { return _refreshToken; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _refreshToken = value;
            }
        }

        public IToken? RequestTokenValue
        {
            get { return _requestToken; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _requestToken = value;
            }
        }

        public Task<IToken> RequestTokenAsync(CancellationToken cancellationToken = default)
        {
            if (_requestToken is null)
            {
                throw new NullReferenceException();
            }

            return Task.FromResult(_requestToken);
        }

        public Task<IToken> RefreshTokenAsync(IToken initialToken, CancellationToken cancellationToken = default)
        {
            Debug.Assert(Object.ReferenceEquals(_requestToken, initialToken));

            if (_refreshToken is null)
            {
                throw new NullReferenceException();

            }
            return Task.FromResult(_refreshToken);
        }
    }

}

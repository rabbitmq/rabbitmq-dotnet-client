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

namespace RabbitMQ.Client.OAuth2
{
    public class OAuth2ClientCredentialsProvider : ICredentialsProvider, IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private readonly string _name;
        private readonly IOAuth2Client _oAuth2Client;
        private IToken? _token;

        public OAuth2ClientCredentialsProvider(string name, IOAuth2Client oAuth2Client)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _oAuth2Client = oAuth2Client ?? throw new ArgumentNullException(nameof(oAuth2Client));
        }

        public string Name => _name;

        public async Task<Credentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (_token == null || string.IsNullOrEmpty(_token.RefreshToken))
                {
                    _token = await _oAuth2Client.RequestTokenAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    _token = await _oAuth2Client.RefreshTokenAsync(_token, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            if (_token is null)
            {
                throw new InvalidOperationException("_token should not be null here");
            }
            else
            {
                return new Credentials(_name, string.Empty,
                    _token.AccessToken, _token.ExpiresIn);
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}

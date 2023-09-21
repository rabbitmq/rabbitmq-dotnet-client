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

namespace RabbitMQ.Client.OAuth2
{
    public class OAuth2ClientCredentialsProvider : ICredentialsProvider
    {
        const int TOKEN_RETRIEVAL_TIMEOUT = 5000;
        private ReaderWriterLock _lock = new ReaderWriterLock();

        private readonly string _name;
        private readonly IOAuth2Client _oAuth2Client;
        private IToken _token;

        public OAuth2ClientCredentialsProvider(string name, IOAuth2Client oAuth2Client)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _oAuth2Client = oAuth2Client ?? throw new ArgumentNullException(nameof(oAuth2Client));
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public string UserName
        {
            get
            {
                checkState();
                return string.Empty;
            }
        }

        public string Password
        {
            get
            {
                return checkState().AccessToken;
            }
        }

        public Nullable<TimeSpan> ValidUntil
        {
            get
            {
                IToken t = checkState();
                if (t is null)
                {
                    return null;
                }
                else
                {
                    return t.ExpiresIn;
                }
            }
        }

        public void Refresh()
        {
            retrieveToken();
        }

        private IToken checkState()
        {
            _lock.AcquireReaderLock(TOKEN_RETRIEVAL_TIMEOUT);
            try
            {
                if (_token != null)
                {
                    return _token;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }

            return retrieveToken();
        }

        private IToken retrieveToken()
        {
            _lock.AcquireWriterLock(TOKEN_RETRIEVAL_TIMEOUT);
            try
            {
                return requestOrRenewToken();
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
        }

        private IToken requestOrRenewToken()
        {
            if (_token == null || _token.RefreshToken == null)
            {
                _token = _oAuth2Client.RequestToken();
            }
            else
            {
                _token = _oAuth2Client.RefreshToken(_token);
            }
            return _token;
        }
    }
}

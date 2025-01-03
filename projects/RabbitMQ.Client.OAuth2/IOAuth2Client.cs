// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.OAuth2
{
    public interface IOAuth2Client
    {
        /// <summary>
        /// Request a new AccessToken from the Token Endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this request</param>
        /// <returns>Token with Access and Refresh Token</returns>
        Task<IToken> RequestTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Request a new AccessToken using the Refresh Token from the Token Endpoint.
        /// </summary>
        /// <param name="token">Token with the Refresh Token</param>
        /// <param name="cancellationToken">Cancellation token for this request</param>
        /// <returns>Token with Access and Refresh Token</returns>
        Task<IToken> RefreshTokenAsync(IToken token, CancellationToken cancellationToken = default);
    }
}

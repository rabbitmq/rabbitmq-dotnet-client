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
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.OAuth2;
using Xunit;
using Xunit.Abstractions;

namespace OAuth2Test
{
    public class TestOAuth2CredentialsProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestOAuth2CredentialsProvider(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ShouldHaveAName()
        {
            const string name = "aName";
            IOAuth2Client oAuth2Client = new MockOAuth2Client(_testOutputHelper);
            var provider = new OAuth2ClientCredentialsProvider(name, oAuth2Client);
            Assert.Equal(name, provider.Name);
        }

        [Fact]
        public async Task ShouldRequestTokenWhenAskToRefresh()
        {
            const string newTokenValue = "the_access_token";
            IToken newToken = NewToken(newTokenValue, TimeSpan.FromSeconds(60));
            var oAuth2Client = new MockOAuth2Client(_testOutputHelper);
            oAuth2Client.RequestTokenValue = newToken;
            var provider = new OAuth2ClientCredentialsProvider(nameof(ShouldRequestTokenWhenAskToRefresh), oAuth2Client);

            Credentials credentials = await provider.GetCredentialsAsync();

            Assert.Equal(newTokenValue, credentials.Password);
        }

        [Fact]
        public async Task ShouldRequestTokenWhenGettingPasswordOrValidUntilForFirstTimeAccess()
        {
            const string accessToken = "the_access_token";
            const string refreshToken = "the_refresh_token";
            IToken firstToken = NewToken(accessToken, refreshToken, TimeSpan.FromSeconds(1));

            var oAuth2Client = new MockOAuth2Client(_testOutputHelper);
            oAuth2Client.RequestTokenValue = firstToken;
            var provider = new OAuth2ClientCredentialsProvider(nameof(ShouldRequestTokenWhenGettingPasswordOrValidUntilForFirstTimeAccess), oAuth2Client);

            Credentials credentials = await provider.GetCredentialsAsync();

            Assert.Equal(firstToken.AccessToken, credentials.Password);
            Assert.NotNull(credentials.ValidUntil);
            Assert.Equal(firstToken.ExpiresIn, credentials.ValidUntil.Value);
        }

        [Fact]
        public async Task ShouldRefreshTokenUsingRefreshTokenWhenAvailable()
        {
            const string accessToken = "the_access_token";
            const string refreshToken = "the_refresh_token";
            const string accessToken2 = "the_access_token_2";
            const string refreshToken2 = "the_refresh_token_2";

            IToken firstToken = NewToken(accessToken, refreshToken, TimeSpan.FromSeconds(1));
            IToken refreshedToken = NewToken(accessToken2, refreshToken2, TimeSpan.FromSeconds(60));

            var oAuth2Client = new MockOAuth2Client(_testOutputHelper);
            oAuth2Client.RequestTokenValue = firstToken;

            var provider = new OAuth2ClientCredentialsProvider(nameof(ShouldRefreshTokenUsingRefreshTokenWhenAvailable), oAuth2Client);

            Credentials credentials = await provider.GetCredentialsAsync();

            Assert.Equal(firstToken.AccessToken, credentials.Password);
            Assert.NotNull(credentials.ValidUntil);
            Assert.Equal(firstToken.ExpiresIn, credentials.ValidUntil.Value);

            oAuth2Client.RefreshTokenValue = refreshedToken;

            while (false == firstToken.HasExpired)
            {
                await Task.Delay(100);
            }

            credentials = await provider.GetCredentialsAsync();

            Assert.Equal(refreshedToken.AccessToken, credentials.Password);
            Assert.NotNull(credentials.ValidUntil);
            Assert.Equal(refreshedToken.ExpiresIn, credentials.ValidUntil.Value);
        }

        [Fact]
        public async Task ShouldRequestTokenWhenRefreshTokenNotAvailable()
        {
            const string accessToken = "the_access_token";
            const string accessToken2 = "the_access_token_2";
            IToken firstToken = NewToken(accessToken, string.Empty, TimeSpan.FromSeconds(1));
            IToken secondToken = NewToken(accessToken2, string.Empty, TimeSpan.FromSeconds(60));

            var oAuth2Client = new MockOAuth2Client(_testOutputHelper);
            oAuth2Client.RequestTokenValue = firstToken;
            var provider = new OAuth2ClientCredentialsProvider(nameof(ShouldRequestTokenWhenRefreshTokenNotAvailable), oAuth2Client);

            Credentials credentials = await provider.GetCredentialsAsync();

            Assert.Equal(firstToken.AccessToken, credentials.Password);
            Assert.NotNull(credentials.ValidUntil);
            Assert.Equal(firstToken.ExpiresIn, credentials.ValidUntil.Value);

            oAuth2Client.RequestTokenValue = secondToken;

            while (false == firstToken.HasExpired)
            {
                await Task.Delay(100);
            }

            credentials = await provider.GetCredentialsAsync();

            Assert.Equal(secondToken.AccessToken, credentials.Password);
            Assert.NotNull(credentials.ValidUntil);
            Assert.Equal(secondToken.ExpiresIn, credentials.ValidUntil.Value);
        }

        private static Token NewToken(string access_token, TimeSpan expiresIn)
        {
            var token = new JsonToken(access_token, string.Empty, expiresIn);
            return new Token(token);
        }

        private static Token NewToken(string access_token, string refresh_token, TimeSpan expiresIn)
        {
            JsonToken token = new JsonToken(access_token, refresh_token, expiresIn);
            return new Token(token);
        }
    }
}

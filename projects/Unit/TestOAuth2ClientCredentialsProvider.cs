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

using Moq;
using RabbitMQ.Client.OAuth2;
using Xunit;

namespace RabbitMQ.Client.Unit
{
    public class TestOAuth2CredentialsProvider
    {
        protected OAuth2ClientCredentialsProvider _provider;
        protected Mock<IOAuth2Client> _oAuth2Client;

        public TestOAuth2CredentialsProvider()
        {
            _oAuth2Client = new Mock<IOAuth2Client>();
            _provider = new OAuth2ClientCredentialsProvider("aName", _oAuth2Client.Object);
        }

        [Fact]
        public void shouldHaveAName()
        {
            Assert.Equal("aName", _provider.Name);
        }

        [Fact]
        public void ShouldRequestTokenWhenAskToRefresh()
        {
            _oAuth2Client.Setup(p => p.RequestToken()).Returns(newToken("the_access_token", TimeSpan.FromSeconds(60)));
            _provider.Refresh();
            Assert.Equal("the_access_token", _provider.Password);
        }

        [Fact]
        public void ShouldRequestTokenWhenGettingPasswordOrValidUntilForFirstTimeAccess()
        {
            IToken firstToken = newToken("the_access_token", "the_refresh_token", TimeSpan.FromSeconds(1));
            _oAuth2Client.Setup(p => p.RequestToken()).Returns(firstToken);
            Assert.Equal(firstToken.AccessToken, _provider.Password);
            Assert.Equal(firstToken.ExpiresIn, _provider.ValidUntil.Value);
        }

        [Fact]
        public void ShouldRefreshTokenUsingRefreshTokenWhenAvailable()
        {
            IToken firstToken = newToken("the_access_token", "the_refresh_token", TimeSpan.FromSeconds(1));
            IToken refreshedToken = newToken("the_access_token2", "the_refresh_token_2", TimeSpan.FromSeconds(60));
            _oAuth2Client.Setup(p => p.RequestToken()).Returns(firstToken);
            _provider.Refresh();
            Assert.Equal(firstToken.AccessToken, _provider.Password);
            Assert.Equal(firstToken.ExpiresIn, _provider.ValidUntil.Value);
            _oAuth2Client.Reset();
            System.Threading.Thread.Sleep(1000);

            _oAuth2Client.Setup(p => p.RefreshToken(firstToken)).Returns(refreshedToken);
            _provider.Refresh();
            Assert.Equal(refreshedToken.AccessToken, _provider.Password);
            Assert.Equal(refreshedToken.ExpiresIn, _provider.ValidUntil.Value);

        }

        [Fact]
        public void ShouldRequestTokenWhenRefreshTokenNotAvailable()
        {
            IToken firstToken = newToken("the_access_token", null, TimeSpan.FromSeconds(1));
            IToken refreshedToken = newToken("the_access_token2", null, TimeSpan.FromSeconds(1));
            _oAuth2Client.SetupSequence(p => p.RequestToken())
                .Returns(firstToken)
                .Returns(refreshedToken);
            _provider.Refresh();
            Assert.Equal(firstToken.AccessToken, _provider.Password);
            Assert.Equal(firstToken.ExpiresIn, _provider.ValidUntil.Value);

            _provider.Refresh();
            Assert.Equal(refreshedToken.AccessToken, _provider.Password);
            Assert.Equal(refreshedToken.ExpiresIn, _provider.ValidUntil.Value);
            Mock.Verify(_oAuth2Client);
        }

        private static Token newToken(string access_token, TimeSpan expiresIn)
        {
            var token = new JsonToken(access_token, string.Empty, expiresIn);
            return new Token(token);
        }

        private static Token newToken(string access_token, string refresh_token, TimeSpan expiresIn)
        {
            JsonToken token = new JsonToken(access_token, refresh_token, expiresIn);
            return new Token(token);
        }
    }
}

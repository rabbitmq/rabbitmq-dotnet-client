// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2024 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.Client.OAuth2;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace OAuth2Test
{
    public class TestOAuth2Client : IAsyncLifetime
    {
        protected string _client_id = "producer";
        protected string _client_secret = "kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn";
        protected WireMockServer _oauthServer;

        protected IOAuth2Client? _client;

        public TestOAuth2Client()
        {
            _oauthServer = WireMockServer.Start();
        }

        public async Task InitializeAsync()
        {
            var uri = new Uri(_oauthServer.Url + "/token");
            var builder = new OAuth2ClientBuilder(_client_id, _client_secret, uri);
            _client = await builder.BuildAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestRequestToken()
        {
            Assert.NotNull(_client);

            JsonToken expectedJsonToken = new JsonToken("the_access_token", "the_refresh_token", TimeSpan.FromSeconds(10));
            ExpectTokenRequest(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "client_credentials"),
                            expectedJsonToken);

            IToken token = await _client.RequestTokenAsync();
            Assert.NotNull(token);
            Assert.Equal(expectedJsonToken.AccessToken, token.AccessToken);
            Assert.Equal(expectedJsonToken.RefreshToken, token.RefreshToken);
            Assert.Equal(TimeSpan.FromSeconds(expectedJsonToken.ExpiresIn), token.ExpiresIn);
        }

        [Fact]
        public async Task TestRefreshToken()
        {
            Assert.NotNull(_client);

            const string accessToken0 = "the_access_token";
            const string accessToken1 = "the_access_token_2";
            const string refreshToken = "the_refresh_token";

            JsonToken expectedJsonToken = new JsonToken(accessToken0, refreshToken, TimeSpan.FromSeconds(10));

            ExpectTokenRequest(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "client_credentials"),
                            expectedJsonToken);

            IToken token = await _client.RequestTokenAsync();
            _oauthServer.Reset();

            JsonToken responseJsonToken = new JsonToken(accessToken1, refreshToken, TimeSpan.FromSeconds(20));
            ExpectTokenRefresh(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "refresh_token")
                            .WithParam("refresh_token", refreshToken),
                            responseJsonToken);

            IToken refreshedToken = await _client.RefreshTokenAsync(token);
            Assert.NotNull(refreshedToken);
            Assert.False(Object.ReferenceEquals(refreshedToken, token));
            Assert.Equal(responseJsonToken.AccessToken, refreshedToken.AccessToken);
            Assert.Equal(responseJsonToken.RefreshToken, refreshedToken.RefreshToken);
            Assert.Equal(TimeSpan.FromSeconds(responseJsonToken.ExpiresIn), refreshedToken.ExpiresIn);
        }

        [Fact]
        public async Task TestInvalidCredentials()
        {
            Assert.NotNull(_client);

            _oauthServer
                .Given(
                    Request.Create()
                        .WithPath("/token")
                        .WithBody(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "client_credentials").Matcher())
                        .UsingPost()
                )
                .RespondWith(
                    Response.Create()
                    .WithStatusCode(401)
                );

            try
            {
                IToken token = await _client.RequestTokenAsync();
                Assert.Fail("Should have thrown Exception");
            }
            catch (HttpRequestException)
            {
            }
        }

        private void ExpectTokenRequest(RequestFormMatcher expectedRequestBody, JsonToken response)
        {
            _oauthServer
                .Given(
                    Request.Create()
                        .WithPath("/token")
                        .WithBody(expectedRequestBody.Matcher())
                        .UsingPost()
                )
                .RespondWith(
                    Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json;charset=UTF-8")
                    .WithBody(JsonSerializer.Serialize(response))
                );
        }

        private void ExpectTokenRefresh(RequestFormMatcher expectedRequestBody, JsonToken expectedResponse)
        {
            _oauthServer
                .Given(
                    Request.Create()
                        .WithPath("/token")
                        .WithBody(expectedRequestBody.Matcher())
                        .UsingPost()
                )
                .RespondWith(
                    Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json;charset=UTF-8")
                    .WithBody(JsonSerializer.Serialize(expectedResponse))
                );
        }
    }
}

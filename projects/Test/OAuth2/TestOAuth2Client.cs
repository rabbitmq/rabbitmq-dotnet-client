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
using RabbitMQ.Client.OAuth2;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace OAuth2Test
{
    public class TestOAuth2Client
    {
        protected string _client_id = "producer";
        protected string _client_secret = "kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn";
        protected WireMockServer _oauthServer;

        protected IOAuth2Client _client;

        public TestOAuth2Client()
        {
            _oauthServer = WireMockServer.Start();

            _client = new OAuth2ClientBuilder(_client_id, _client_secret, new System.Uri(_oauthServer.Url + "/token")).Build();
        }

        private void expectTokenRequest(RequestFormMatcher expectedRequestBody, JsonToken expectedResponse)
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

        [Fact]
        public void TestRequestToken()
        {
            JsonToken expectedJsonToken = new JsonToken("the_access_token", "the_refresh_token", TimeSpan.FromSeconds(10));
            expectTokenRequest(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "client_credentials"),
                            expectedJsonToken);

            IToken token = _client.RequestToken();
            Assert.NotNull(token);
            Assert.Equal(expectedJsonToken.access_token, token.AccessToken);
            Assert.Equal(expectedJsonToken.refresh_token, token.RefreshToken);
            Assert.Equal(TimeSpan.FromSeconds(expectedJsonToken.expires_in), token.ExpiresIn);
        }

        private void expectTokenRefresh(JsonToken expectedResponse)
        {
            _oauthServer
                .Given(
                    Request.Create()
                        .WithPath("/token")
                        .WithParam("client_id", _client_id)
                        .WithParam("client_secret", _client_secret)
                        .WithParam("grant_type", "refresh_token")
                        .WithParam("refresh_token", expectedResponse.refresh_token)
                        .WithHeader("content_type", "application/x-www-form-urlencoded")
                        .UsingPost()
                )
                .RespondWith(
                    Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json;charset=UTF-8")
                    .WithBody(JsonSerializer.Serialize(expectedResponse))
                );
        }

        [Fact]
        public void TestRefreshToken()
        {
            JsonToken expectedJsonToken = new JsonToken("the_access_token", "the_refresh_token", TimeSpan.FromSeconds(10));
            expectTokenRequest(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "client_credentials"),
                            expectedJsonToken);

            IToken token = _client.RequestToken();
            _oauthServer.Reset();

            expectedJsonToken = new JsonToken("the_access_token2", "the_refresh_token", TimeSpan.FromSeconds(20));
            expectTokenRequest(new RequestFormMatcher()
                            .WithParam("client_id", _client_id)
                            .WithParam("client_secret", _client_secret)
                            .WithParam("grant_type", "refresh_token")
                            .WithParam("refresh_token", "the_refresh_token"),
                            expectedJsonToken);

            IToken refreshedToken = _client.RefreshToken(token);
            Assert.False(refreshedToken == token);
            Assert.NotNull(refreshedToken);
            Assert.Equal(expectedJsonToken.access_token, refreshedToken.AccessToken);
            Assert.Equal(expectedJsonToken.refresh_token, refreshedToken.RefreshToken);
            Assert.Equal(TimeSpan.FromSeconds(expectedJsonToken.expires_in), refreshedToken.ExpiresIn);
        }

        [Fact]
        public void TestInvalidCredentials()
        {
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
                IToken token = _client.RequestToken();
                Assert.Fail("Should have thrown Exception");
            }
            catch (HttpRequestException) { }
        }
    }
}

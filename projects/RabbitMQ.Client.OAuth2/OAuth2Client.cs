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
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RabbitMQ.Client.OAuth2
{
    public interface IOAuth2Client
    {
        public IToken RequestToken();
        public IToken RefreshToken(IToken token);
    }

    public interface IToken
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public TimeSpan ExpiresIn { get; }
        public bool hasExpired { get; }
    }

    public class Token : IToken
    {
        private readonly JsonToken _source;
        private readonly DateTime _lastTokenRenewal;

        public Token(JsonToken json)
        {
            this._source = json;
            this._lastTokenRenewal = DateTime.Now;
        }

        public string AccessToken
        {
            get
            {
                return _source.access_token;
            }
        }

        public string RefreshToken
        {
            get
            {
                return _source.refresh_token;
            }
        }

        public TimeSpan ExpiresIn
        {
            get
            {
                return TimeSpan.FromSeconds(_source.expires_in);
            }
        }

        bool IToken.hasExpired
        {
            get
            {
                TimeSpan age = DateTime.Now - _lastTokenRenewal;
                return age > ExpiresIn;
            }
        }
    }

    public class OAuth2ClientBuilder
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly Uri _tokenEndpoint;
        private string _scope;
        private IDictionary<string, string> _additionalRequestParameters;
        private HttpClientHandler _httpClientHandler;

        public OAuth2ClientBuilder(string clientId, string clientSecret, Uri tokenEndpoint)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _tokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));

        }

        public OAuth2ClientBuilder SetScope(string scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            return this;
        }

        public OAuth2ClientBuilder SetHttpClientHandler(HttpClientHandler handler)
        {
            _httpClientHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public OAuth2ClientBuilder AddRequestParameter(string param, string paramValue)
        {
            if (param == null)
            {
                throw new ArgumentNullException("param is null");
            }
            if (paramValue == null)
            {
                throw new ArgumentNullException("paramValue is null");
            }
            if (_additionalRequestParameters == null)
            {
                _additionalRequestParameters = new Dictionary<string, string>();
            }
            _additionalRequestParameters[param] = paramValue;
            return this;
        }

        public IOAuth2Client Build()
        {
            return new OAuth2Client(_clientId, _clientSecret, _tokenEndpoint,
                _scope, _additionalRequestParameters, _httpClientHandler);
        }
    }

    /**
    * Default implementation of IOAuth2Client. It uses Client_Credentials OAuth2 flow to request a
    * token. The basic constructor assumes no scopes are needed only the OAuth2 Client credentiuals.
    * The additional constructor accepts a Dictionary with all the request parameters passed onto the
    * OAuth2 request token.
    */
    internal class OAuth2Client : IOAuth2Client, IDisposable
    {
        const string GRANT_TYPE = "grant_type";
        const string CLIENT_ID = "client_id";
        const string SCOPE = "scope";
        const string CLIENT_SECRET = "client_secret";
        const string REFRESH_TOKEN = "refresh_token";
        const string GRANT_TYPE_CLIENT_CREDENTIALS = "client_credentials";

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly Uri _tokenEndpoint;
        private readonly string _scope;
        private readonly IDictionary<string, string> _additionalRequestParameters;

        public static readonly IDictionary<string, string> EMPTY = new Dictionary<string, string>();

        private HttpClient _httpClient;

        public OAuth2Client(string clientId, string clientSecret, Uri tokenEndpoint, string scope,
                IDictionary<string, string> additionalRequestParameters,
                HttpClientHandler httpClientHandler)
        {
            this._clientId = clientId;
            this._clientSecret = clientSecret;
            this._scope = scope;
            this._additionalRequestParameters = additionalRequestParameters == null ? EMPTY : additionalRequestParameters;
            this._tokenEndpoint = tokenEndpoint;

            _httpClient = httpClientHandler == null ? new HttpClient() :
                new HttpClient(httpClientHandler);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public IToken RequestToken()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            req.Content = new FormUrlEncodedContent(buildRequestParameters());

            Task<HttpResponseMessage> response = _httpClient.SendAsync(req);
            response.Wait();
            response.Result.EnsureSuccessStatusCode();
            Task<JsonToken> token = response.Result.Content.ReadFromJsonAsync<JsonToken>();
            token.Wait();
            return new Token(token.Result);
        }

        public IToken RefreshToken(IToken token)
        {
            if (token.RefreshToken == null)
            {
                throw new InvalidOperationException("Token has no Refresh Token");
            }

            var req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(buildRefreshParameters(token))
            };

            Task<HttpResponseMessage> response = _httpClient.SendAsync(req);
            response.Wait();
            response.Result.EnsureSuccessStatusCode();
            Task<JsonToken> refreshedToken = response.Result.Content.ReadFromJsonAsync<JsonToken>();
            refreshedToken.Wait();
            return new Token(refreshedToken.Result);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private Dictionary<string, string> buildRequestParameters()
        {
            var dict = new Dictionary<string, string>(_additionalRequestParameters);
            dict.Add(CLIENT_ID, _clientId);
            dict.Add(CLIENT_SECRET, _clientSecret);
            if (_scope != null && _scope.Length > 0)
            {
                dict.Add(SCOPE, _scope);
            }
            dict.Add(GRANT_TYPE, GRANT_TYPE_CLIENT_CREDENTIALS);
            return dict;
        }

        private Dictionary<string, string> buildRefreshParameters(IToken token)
        {
            var dict = buildRequestParameters();
            dict.Remove(GRANT_TYPE);
            dict.Add(GRANT_TYPE, REFRESH_TOKEN);
            if (_scope != null)
            {
                dict.Add(SCOPE, _scope);
            }
            dict.Add(REFRESH_TOKEN, token.RefreshToken);
            return dict;
        }
    }

    public class JsonToken
    {
        public JsonToken()
        {
        }

        public JsonToken(string access_token, string refresh_token, TimeSpan expires_in_span)
        {
            this.access_token = access_token;
            this.refresh_token = refresh_token;
            this.expires_in = (long)expires_in_span.TotalSeconds;
        }

        public JsonToken(string access_token, string refresh_token, long expires_in)
        {
            this.access_token = access_token;
            this.refresh_token = refresh_token;
            this.expires_in = expires_in;
        }

        public string access_token
        {
            get; set;
        }

        public string refresh_token
        {
            get; set;
        }

        public long expires_in
        {
            get; set;
        }
    }
}

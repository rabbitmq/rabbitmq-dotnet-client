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
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.OAuth2
{
    public class OAuth2ClientBuilder
    {
        /// <summary>
        /// Discovery endpoint subpath for all OpenID Connect issuers.
        /// </summary>
        const string DISCOVERY_ENDPOINT = ".well-known/openid-configuration";

        private readonly string _clientId;
        private readonly string _clientSecret;

        // At least one of the following Uris is not null
        private readonly Uri? _tokenEndpoint;
        private readonly Uri? _issuer;

        private string? _scope;
        private IDictionary<string, string>? _additionalRequestParameters;
        private HttpClientHandler? _httpClientHandler;

        /// <summary>
        /// Create a new builder for creating <see cref="OAuth2Client"/>s.
        /// </summary>
        /// <param name="clientId">Id of the client</param>
        /// <param name="clientSecret">Secret of the client</param>
        /// <param name="tokenEndpoint">Endpoint to receive the Access Token</param>
        /// <param name="issuer">Issuer of the Access Token. Used to automatically receive the Token Endpoint while building</param>
        /// <remarks>
        /// Either <paramref name="tokenEndpoint"/> or <paramref name="issuer"/> must be provided.
        /// </remarks>
        public OAuth2ClientBuilder(string clientId, string clientSecret, Uri? tokenEndpoint = null, Uri? issuer = null)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));

            if (tokenEndpoint is null && issuer is null)
            {
                throw new ArgumentException("Either tokenEndpoint or issuer is required");
            }

            _tokenEndpoint = tokenEndpoint;
            _issuer = issuer;
        }

        /// <summary>
        /// Set the requested scopes for the client.
        /// </summary>
        /// <param name="scope">OAuth scopes to request from the Issuer</param>
        public OAuth2ClientBuilder SetScope(string scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            return this;
        }

        /// <summary>
        /// Set custom HTTP Client handler for requests of the OAuth2 client.
        /// </summary>
        /// <param name="handler">Custom handler for HTTP requests</param>
        public OAuth2ClientBuilder SetHttpClientHandler(HttpClientHandler handler)
        {
            _httpClientHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>
        /// Add a additional request parameter to each HTTP request.
        /// </summary>
        /// <param name="param">Name of the parameter</param>
        /// <param name="paramValue">Value of the parameter</param>
        public OAuth2ClientBuilder AddRequestParameter(string param, string paramValue)
        {
            if (param is null)
            {
                throw new ArgumentNullException(nameof(param));
            }

            if (paramValue is null)
            {
                throw new ArgumentNullException(nameof(paramValue));
            }

            _additionalRequestParameters ??= new Dictionary<string, string>();
            _additionalRequestParameters[param] = paramValue;

            return this;
        }

        /// <summary>
        /// Build the <see cref="OAuth2Client"/> with the provided properties of the builder.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this method</param>
        /// <returns>Configured OAuth2Client</returns>
        public async ValueTask<IOAuth2Client> BuildAsync(CancellationToken cancellationToken = default)
        {
            // Check if Token Endpoint is missing -> Use Issuer to receive Token Endpoint
            if (_tokenEndpoint is null)
            {
                Uri tokenEndpoint = await GetTokenEndpointFromIssuerAsync(cancellationToken).ConfigureAwait(false);
                return new OAuth2Client(_clientId, _clientSecret, tokenEndpoint,
                    _scope, _additionalRequestParameters, _httpClientHandler);
            }

            return new OAuth2Client(_clientId, _clientSecret, _tokenEndpoint,
                _scope, _additionalRequestParameters, _httpClientHandler);
        }

        /// <summary>
        /// Receive Token Endpoint from discovery page of the Issuer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for this request</param>
        /// <returns>Uri of the Token Endpoint</returns>
        private async Task<Uri> GetTokenEndpointFromIssuerAsync(CancellationToken cancellationToken = default)
        {
            if (_issuer is null)
            {
                throw new InvalidOperationException("The issuer is required");
            }

            using HttpClient httpClient = _httpClientHandler is null
                ? new HttpClient()
                : new HttpClient(_httpClientHandler, false);

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Build endpoint from Issuer and discovery endpoint, we can't use the Uri overload because the Issuer Uri may not have a trailing '/'
            string tempIssuer = _issuer.AbsoluteUri.EndsWith("/") ? _issuer.AbsoluteUri : _issuer.AbsoluteUri + "/";
            Uri discoveryEndpoint = new Uri(tempIssuer + DISCOVERY_ENDPOINT);

            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, discoveryEndpoint);
            using HttpResponseMessage response = await httpClient.SendAsync(req, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            OpenIDConnectDiscovery? discovery = await response.Content.ReadFromJsonAsync<OpenIDConnectDiscovery>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (discovery is null || string.IsNullOrEmpty(discovery.TokenEndpoint))
            {
                throw new InvalidOperationException("No token endpoint was found");
            }

            return new Uri(discovery.TokenEndpoint);
        }
    }

    /**
    * Default implementation of IOAuth2Client. It uses Client_Credentials OAuth2 flow to request a
    * token. The basic constructor assumes no scopes are needed only the OAuth2 Client credentials.
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
        private readonly string? _scope;
        private readonly IDictionary<string, string> _additionalRequestParameters;

        public static readonly IDictionary<string, string> EMPTY = new Dictionary<string, string>();

        private readonly HttpClient _httpClient;

        public OAuth2Client(string clientId, string clientSecret, Uri tokenEndpoint,
            string? scope,
            IDictionary<string, string>? additionalRequestParameters,
            HttpClientHandler? httpClientHandler)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;
            _additionalRequestParameters = additionalRequestParameters ?? EMPTY;
            _tokenEndpoint = tokenEndpoint;

            _httpClient = httpClientHandler is null
                ? new HttpClient()
                : new HttpClient(httpClientHandler, false);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc />
        public async Task<IToken> RequestTokenAsync(CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            req.Content = new FormUrlEncodedContent(BuildRequestParameters());

            using HttpResponseMessage response = await _httpClient.SendAsync(req, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            JsonToken? token = await response.Content.ReadFromJsonAsync<JsonToken>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (token is null)
            {
                throw new InvalidOperationException("token is null");
            }

            return new Token(token);
        }

        /// <inheritdoc />
        public async Task<IToken> RefreshTokenAsync(IToken token,
            CancellationToken cancellationToken = default)
        {
            if (token.RefreshToken is null)
            {
                throw new InvalidOperationException("Token has no Refresh Token");
            }

            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            req.Content = new FormUrlEncodedContent(BuildRefreshParameters(token));

            using HttpResponseMessage response = await _httpClient.SendAsync(req, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            JsonToken? refreshedToken = await response.Content.ReadFromJsonAsync<JsonToken>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (refreshedToken is null)
            {
                throw new InvalidOperationException("refreshed token is null");
            }

            return new Token(refreshedToken);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private Dictionary<string, string> BuildRequestParameters()
        {
            var dict = new Dictionary<string, string>(_additionalRequestParameters)
            {
                { CLIENT_ID, _clientId },
                { CLIENT_SECRET, _clientSecret }
            };

            if (!string.IsNullOrEmpty(_scope))
            {
                dict.Add(SCOPE, _scope!);
            }

            dict.Add(GRANT_TYPE, GRANT_TYPE_CLIENT_CREDENTIALS);

            return dict;
        }

        private Dictionary<string, string> BuildRefreshParameters(IToken token)
        {
            Dictionary<string, string> dict = BuildRequestParameters();
            dict[GRANT_TYPE] = REFRESH_TOKEN;

            if (token.RefreshToken != null)
            {
                dict.Add(REFRESH_TOKEN, token.RefreshToken);
            }

            return dict;
        }
    }

    internal class JsonToken
    {
        public JsonToken()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
        }

        public JsonToken(string access_token, string refresh_token, TimeSpan expires_in_span)
        {
            AccessToken = access_token;
            RefreshToken = refresh_token;
            ExpiresIn = (long)expires_in_span.TotalSeconds;
        }

        public JsonToken(string access_token, string refresh_token, long expires_in)
        {
            AccessToken = access_token;
            RefreshToken = refresh_token;
            ExpiresIn = expires_in;
        }

        [JsonPropertyName("access_token")]
        public string AccessToken
        {
            get; set;
        }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken
        {
            get; set;
        }

        [JsonPropertyName("expires_in")]
        public long ExpiresIn
        {
            get; set;
        }
    }

    /// <summary>
    /// Minimal version of the properties of the discovery endpoint.
    /// </summary>
    internal class OpenIDConnectDiscovery
    {
        public OpenIDConnectDiscovery()
        {
            TokenEndpoint = string.Empty;
        }

        public OpenIDConnectDiscovery(string tokenEndpoint)
        {
            TokenEndpoint = tokenEndpoint;
        }

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint
        {
            get; set;
        }
    }
}

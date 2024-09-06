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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.OAuth2;
using Test;
using Xunit;
using Xunit.Abstractions;

namespace OAuth2Test
{
    public class TestOAuth2 : IAsyncLifetime
    {
        private const string Exchange = "test_direct";

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _doneEvent = new SemaphoreSlim(0, 1);
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly int _tokenExpiresInSeconds;

        private OAuth2ClientCredentialsProvider? _producerCredentialsProvider;
        private OAuth2ClientCredentialsProvider? _httpApiCredentialsProvider;
        private IConnectionFactory? _connectionFactory;
        private IConnection? _connection;
        private CredentialsRefresher? _credentialsRefresher;

        public TestOAuth2(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _tokenExpiresInSeconds = OAuth2OptionsBase.TokenExpiresInSeconds;
        }

        public async Task InitializeAsync()
        {
            string modeStr = Environment.GetEnvironmentVariable("OAUTH2_MODE") ?? "uaa";
            Mode mode = (Mode)Enum.Parse(typeof(Mode), modeStr.ToLowerInvariant());

            var producerOptions = new OAuth2ProducerOptions(mode);
            _producerCredentialsProvider = await GetCredentialsProviderAsync(producerOptions);

            var httpApiOptions = new OAuth2HttpApiOptions(mode);
            _httpApiCredentialsProvider = await GetCredentialsProviderAsync(httpApiOptions);

            _connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                CredentialsProvider = _producerCredentialsProvider,
                ClientProvidedName = nameof(TestOAuth2)
            };

            _connection = await _connectionFactory.CreateConnectionAsync(_cancellationTokenSource.Token);

            _connection.ConnectionShutdown += (sender, ea) =>
            {
                _testOutputHelper.WriteLine("{0} [WARNING] connection shutdown!", DateTime.Now);
            };

            _connection.ConnectionRecoveryError += (sender, ea) =>
            {
                _testOutputHelper.WriteLine("{0} [ERROR] connection recovery error: {1}",
                    DateTime.Now, ea.Exception);
            };

            _connection.RecoverySucceeded += (sender, ea) =>
            {
                _testOutputHelper.WriteLine("{0} [INFO] connection recovery succeeded", DateTime.Now);
            };

            _credentialsRefresher = new CredentialsRefresher(_producerCredentialsProvider,
                OnCredentialsRefreshedAsync,
                _cancellationTokenSource.Token);
        }

        public async Task DisposeAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();

                if (_connection != null)
                {
                    await _connection.CloseAsync();
                }
            }
            finally
            {
                _doneEvent.Dispose();
                _producerCredentialsProvider?.Dispose();
                _connection?.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }

        private Task OnCredentialsRefreshedAsync(Credentials? credentials, Exception? exception,
            CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                _testOutputHelper.WriteLine("connection is unexpectedly null!");
                Assert.Fail("_connection is unexpectedly null!");
            }

            if (exception != null)
            {
                _testOutputHelper.WriteLine("exception is unexpectedly not-null: {0}", exception);
                Assert.Fail($"exception is unexpectedly not-null: {exception}");
            }

            if (credentials is null)
            {
                _testOutputHelper.WriteLine("credentials arg is unexpectedly null!");
                Assert.Fail("credentials arg is unexpectedly null!");
            }

            return _connection.UpdateSecretAsync(credentials.Password, "Token refresh", cancellationToken);
        }

        [Fact]
        public async Task IntegrationTest()
        {
            Util? closeConnectionUtil = null;
            Task? closeConnectionTask = null;

            using (IChannel publishChannel = await DeclarePublishChannelAsync())
            {
                using (IChannel consumeChannel = await DeclareConsumeChannelAsync())
                {
                    await PublishAsync(publishChannel);
                    await ConsumeAsync(consumeChannel);

                    if (_tokenExpiresInSeconds > 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var delaySpan = TimeSpan.FromSeconds(_tokenExpiresInSeconds + 10);
                            _testOutputHelper.WriteLine("{0} [INFO] wait '{1}' until Token expires. Attempt #{1}",
                                DateTime.Now, delaySpan, (i + 1));

                            if (i == 1)
                            {
                                async Task CloseConnection()
                                {
                                    Assert.NotNull(_connection);
                                    Assert.NotNull(_httpApiCredentialsProvider);
                                    Credentials httpApiCredentials = await _httpApiCredentialsProvider.GetCredentialsAsync();
                                    closeConnectionUtil = new Util(_testOutputHelper, "mgt_api_client", httpApiCredentials.Password);
                                    await closeConnectionUtil.CloseConnectionAsync(_connection.ClientProvidedName);
                                }

                                closeConnectionTask = Task.Run(CloseConnection);
                            }

                            await Task.Delay(delaySpan);

                            if (closeConnectionTask != null)
                            {
                                await closeConnectionTask;
                                closeConnectionTask = null;

                                if (closeConnectionUtil != null)
                                {
                                    closeConnectionUtil.Dispose();
                                    closeConnectionUtil = null;
                                }
                            }

                            _testOutputHelper.WriteLine("{0} [INFO] Resuming ...", DateTime.Now);

                            await PublishAsync(publishChannel);
                            await ConsumeAsync(consumeChannel);
                        }
                    }
                    else
                    {
                        Assert.Fail("_tokenExpiresInSeconds is NOT greater than 0");
                    }

                    await consumeChannel.CloseAsync();
                }

                await publishChannel.CloseAsync();
            }
        }

        [Fact]
        public async Task SecondConnectionCrashes_GH1429()
        {
            Assert.NotNull(_connectionFactory);
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1429
            IConnection secondConnection = await _connectionFactory.CreateConnectionAsync(CancellationToken.None);
            secondConnection.Dispose();
        }

        private async Task<IChannel> DeclarePublishChannelAsync()
        {
            Assert.NotNull(_connection);
            IChannel publishChannel = await _connection.CreateChannelAsync();
            await publishChannel.ConfirmSelectAsync();
            await publishChannel.ExchangeDeclareAsync("test_direct", ExchangeType.Direct, true, false);
            return publishChannel;
        }

        private async Task PublishAsync(IChannel publishChannel)
        {
            const string message = "Hello World!";

            var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message));
            var properties = new BasicProperties
            {
                AppId = "oauth2",
            };

            await publishChannel.BasicPublishAsync(exchange: Exchange, routingKey: "hello", false, basicProperties: properties, body: body);
            _testOutputHelper.WriteLine("Sent message");

            await publishChannel.WaitForConfirmsOrDieAsync();
            _testOutputHelper.WriteLine("Confirmed Sent message");
        }

        private async ValueTask<IChannel> DeclareConsumeChannelAsync()
        {
            Assert.NotNull(_connection);
            IChannel consumeChannel = await _connection.CreateChannelAsync();
            await consumeChannel.QueueDeclareAsync(queue: "testqueue", true, false, false);
            await consumeChannel.QueueBindAsync("testqueue", Exchange, "hello");
            return consumeChannel;
        }

        private async Task ConsumeAsync(IChannel consumeChannel)
        {
            var asyncListener = new AsyncEventingBasicConsumer(consumeChannel);
            asyncListener.Received += AsyncListener_Received;
            string consumerTag = await consumeChannel.BasicConsumeAsync("testqueue", true, "testconsumer", asyncListener);
            await _doneEvent.WaitAsync(TimeSpan.FromSeconds(5));
            _testOutputHelper.WriteLine("Received message");
            await consumeChannel.BasicCancelAsync(consumerTag);
        }

        private async Task<OAuth2ClientCredentialsProvider> GetCredentialsProviderAsync(OAuth2OptionsBase opts)
        {
            _testOutputHelper.WriteLine("OAuth2Client ");
            _testOutputHelper.WriteLine($"- ClientId: {opts.ClientId}");
            _testOutputHelper.WriteLine($"- ClientSecret: {opts.ClientSecret}");
            _testOutputHelper.WriteLine($"- TokenEndpoint: {opts.TokenEndpoint}");
            _testOutputHelper.WriteLine($"- Scope: {opts.Scope}");

            var tokenEndpointUri = new Uri(opts.TokenEndpoint);
            var builder = new OAuth2ClientBuilder(opts.ClientId, opts.ClientSecret, tokenEndpointUri);
            IOAuth2Client oAuth2Client = await builder.BuildAsync();
            return new OAuth2ClientCredentialsProvider(opts.Name, oAuth2Client);
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            _doneEvent.Release();
            return Task.CompletedTask;
        }
    }
}

﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.OAuth2;
using Xunit;
using Xunit.Abstractions;

namespace OAuth2Test
{
    public enum Mode
    {
        uaa,
        keycloak
    }

    public class OAuth2Options
    {
        private readonly Mode _mode;

        public OAuth2Options(Mode mode)
        {
            _mode = mode;
        }

        public string Name => _mode switch
        {
            Mode.uaa => "uaa",
            Mode.keycloak => "keycloak",
            _ => throw new InvalidOperationException(),
        };

        public string ClientId => "producer";

        public string ClientSecret => _mode switch
        {
            Mode.uaa => "producer_secret",
            Mode.keycloak => "kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn",
            _ => throw new InvalidOperationException(),
        };

        public string Scope => _mode switch
        {
            Mode.uaa => "",
            Mode.keycloak => "rabbitmq:configure:*/* rabbitmq:read:*/* rabbitmq:write:*/*",
            _ => throw new InvalidOperationException(),
        };

        public string TokenEndpoint => _mode switch
        {
            Mode.uaa => "http://localhost:8080/oauth/token",
            Mode.keycloak => "http://localhost:8080/realms/test/protocol/openid-connect/token",
            _ => throw new InvalidOperationException(),
        };

        public int TokenExpiresInSeconds => 60;
    }

    public class TestOAuth2
    {
        private const string Exchange = "test_direct";

        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IConnection _connection;
        private readonly int _tokenExpiresInSeconds;

        public TestOAuth2(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            string modeStr = Environment.GetEnvironmentVariable("OAUTH2_MODE") ?? "uaa";
            Mode mode = (Mode)Enum.Parse(typeof(Mode), modeStr.ToLowerInvariant());
            var options = new OAuth2Options(mode);

            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                CredentialsProvider = GetCredentialsProvider(options),
                CredentialsRefresher = GetCredentialsRefresher(),
                ClientProvidedName = nameof(TestOAuth2)
            };

            _connection = connectionFactory.CreateConnection();
            _tokenExpiresInSeconds = options.TokenExpiresInSeconds;
        }

        [Fact]
        public async void IntegrationTest()
        {
            using (_connection)
            {
                using (IChannel publisher = declarePublisher())
                using (IChannel subscriber = await declareConsumer())
                {
                    await Publish(publisher);
                    Consume(subscriber);

                    if (_tokenExpiresInSeconds > 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            _testOutputHelper.WriteLine("Wait until Token expires. Attempt #" + (i + 1));

                            await Task.Delay(TimeSpan.FromSeconds(_tokenExpiresInSeconds + 10));
                            _testOutputHelper.WriteLine("Resuming ..");

                            await Publish(publisher);
                            _doneEvent.Reset();

                            Consume(subscriber);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        private IChannel declarePublisher()
        {
            IChannel publisher = _connection.CreateChannel();
            publisher.ConfirmSelect();
            publisher.ExchangeDeclare("test_direct", ExchangeType.Direct, true, false);
            return publisher;
        }

        private async Task Publish(IChannel publisher)
        {
            const string message = "Hello World!";

            var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message));
            var properties = new BasicProperties
            {
                AppId = "oauth2",
            };

            await publisher.BasicPublishAsync(exchange: Exchange, routingKey: "hello", basicProperties: properties, body: body);
            _testOutputHelper.WriteLine("Sent message");

            await publisher.WaitForConfirmsOrDieAsync();
            _testOutputHelper.WriteLine("Confirmed Sent message");
        }

        private async ValueTask<IChannel> declareConsumer()
        {
            IChannel subscriber = _connection.CreateChannel();
            await subscriber.QueueDeclareAsync(queue: "testqueue", passive: false, true, false, false, arguments: null);
            subscriber.QueueBind("testqueue", Exchange, "hello");
            return subscriber;
        }

        private void Consume(IChannel subscriber)
        {
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            string consumerTag = subscriber.BasicConsume("testqueue", true, "testconsumer", asyncListener);
            _doneEvent.WaitOne(1);
            _testOutputHelper.WriteLine("Received message");
            subscriber.BasicCancel(consumerTag);
        }

        private OAuth2ClientCredentialsProvider GetCredentialsProvider(OAuth2Options opts)
        {
            _testOutputHelper.WriteLine("OAuth2Client ");
            _testOutputHelper.WriteLine($"- ClientId: {opts.ClientId}");
            _testOutputHelper.WriteLine($"- ClientSecret: {opts.ClientSecret}");
            _testOutputHelper.WriteLine($"- TokenEndpoint: {opts.TokenEndpoint}");
            _testOutputHelper.WriteLine($"- Scope: {opts.Scope}");

            var tokenEndpointUri = new Uri(opts.TokenEndpoint);
            IOAuth2Client oAuth2Client = new OAuth2ClientBuilder(opts.ClientId, opts.ClientSecret, tokenEndpointUri).Build();
            return new OAuth2ClientCredentialsProvider(opts.Name, oAuth2Client);
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            _doneEvent.Set();
            return Task.CompletedTask;
        }

        private static ICredentialsRefresher GetCredentialsRefresher()
        {
            return new TimerBasedCredentialRefresher();
        }
    }
}

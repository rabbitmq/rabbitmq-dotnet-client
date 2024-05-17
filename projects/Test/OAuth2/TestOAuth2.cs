using System;
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

        public string Name
        {
            get
            {
                switch (_mode)
                {
                    case Mode.uaa:
                        return "uaa";
                    case Mode.keycloak:
                        return "keycloak";
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public string ClientId => "producer";

        public string ClientSecret
        {
            get
            {
                switch (_mode)
                {
                    case Mode.uaa:
                        return "producer_secret";
                    case Mode.keycloak:
                        return "kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn";
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public string Scope
        {
            get
            {
                switch (_mode)
                {
                    case Mode.uaa:
                        return string.Empty;
                    case Mode.keycloak:
                        return "rabbitmq:configure:*/* rabbitmq:read:*/* rabbitmq:write:*/*";
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public string TokenEndpoint // => _mode switch
        {
            get
            {
                switch (_mode)
                {
                    case Mode.uaa:
                        return "http://localhost:8080/oauth/token";
                    case Mode.keycloak:
                        return "http://localhost:8080/realms/test/protocol/openid-connect/token";
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public int TokenExpiresInSeconds => 60;
    }

    public class TestOAuth2 : IAsyncLifetime
    {
        private static readonly ExchangeName s_exchange = new ExchangeName("test_direct");

        private readonly SemaphoreSlim _doneEvent = new SemaphoreSlim(0, 1);
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private readonly int _tokenExpiresInSeconds;

        public TestOAuth2(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            string modeStr = Environment.GetEnvironmentVariable("OAUTH2_MODE") ?? "uaa";
            Mode mode = (Mode)Enum.Parse(typeof(Mode), modeStr.ToLowerInvariant());
            var options = new OAuth2Options(mode);

            _connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true,
                CredentialsProvider = GetCredentialsProvider(options),
                CredentialsRefresher = GetCredentialsRefresher(),
                ClientProvidedName = nameof(TestOAuth2)
            };

            _tokenExpiresInSeconds = options.TokenExpiresInSeconds;
        }

        public async Task InitializeAsync()
        {
            _connection = await _connectionFactory.CreateConnectionAsync(CancellationToken.None);
        }

        public async Task DisposeAsync()
        {
            try
            {
                await _connection.CloseAsync();
            }
            finally
            {
                _doneEvent.Dispose();
                _connection.Dispose();
            }
        }

        [Fact]
        public async void IntegrationTest()
        {
            using (IChannel publishChannel = await DeclarePublisherAsync())
            {
                using (IChannel consumeChannel = await DeclareConsumerAsync())
                {
                    await PublishAsync(publishChannel);
                    await ConsumeAsync(consumeChannel);

                    if (_tokenExpiresInSeconds > 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            _testOutputHelper.WriteLine("Wait until Token expires. Attempt #" + (i + 1));

                            await Task.Delay(TimeSpan.FromSeconds(_tokenExpiresInSeconds + 10));
                            _testOutputHelper.WriteLine("Resuming ..");

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
        public async void SecondConnectionCrashes_GH1429()
        {
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1429
            IConnection secondConnection = await _connectionFactory.CreateConnectionAsync(CancellationToken.None);
            secondConnection.Dispose();
        }

        private async Task<IChannel> DeclarePublisherAsync()
        {
            IChannel publisher = await _connection.CreateChannelAsync();
            await publisher.ConfirmSelectAsync();
            await publisher.ExchangeDeclareAsync((ExchangeName)"test_direct", ExchangeType.Direct, true, false);
            return publisher;
        }

        private async Task PublishAsync(IChannel publisher)
        {
            const string message = "Hello World!";

            var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message));
            var properties = new BasicProperties
            {
                AppId = "oauth2",
            };

            await publisher.BasicPublishAsync(exchange: s_exchange, routingKey: "hello", basicProperties: properties, body: body);
            _testOutputHelper.WriteLine("Sent message");

            await publisher.WaitForConfirmsOrDieAsync();
            _testOutputHelper.WriteLine("Confirmed Sent message");
        }

        private async ValueTask<IChannel> DeclareConsumerAsync()
        {
            IChannel subscriber = await _connection.CreateChannelAsync();
            await subscriber.QueueDeclareAsync(queue: "testqueue", true, false, false);
            await subscriber.QueueBindAsync("testqueue", s_exchange, "hello");
            return subscriber;
        }

        private async Task ConsumeAsync(IChannel subscriber)
        {
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            string consumerTag = await subscriber.BasicConsumeAsync("testqueue", true, "testconsumer", asyncListener);
            await _doneEvent.WaitAsync(TimeSpan.FromMilliseconds(500));
            _testOutputHelper.WriteLine("Received message");
            await subscriber.BasicCancelAsync(consumerTag);
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
            _doneEvent.Release();
            return Task.CompletedTask;
        }

        private static ICredentialsRefresher GetCredentialsRefresher()
        {
            return new TimerBasedCredentialRefresher();
        }
    }
}

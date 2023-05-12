using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.OAuth2;

namespace OAuth2
{
    public static class Program
    {
        private static string _exchange = "test_direct";
        private static AutoResetEvent? _doneEvent;

        public class OAuth2Options
        {
            public string? Name { get; set; }
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? Scope { get; set; }
            public string? TokenEndpoint { get; set; }
            public int TokenExpiresInSeconds { get; set; }
        }

        public static void Main(string[] args)
        {

            IConfiguration configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            _doneEvent = new AutoResetEvent(false);

            OAuth2Options? oauth2Options = configuration.Get<OAuth2Options>();
            if (oauth2Options == null)
            {
                throw new ArgumentException("There are no OAuth2 Options");
            }

            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                CredentialsProvider = GetCredentialsProvider(oauth2Options),
                CredentialsRefresher = GetCredentialsRefresher()
            };

            using (IConnection connection = connectionFactory.CreateConnection())
            {
                using (IChannel publisher = declarePublisher(connection))
                using (IChannel subscriber = declareConsumer(connection))
                {
                    Publish(publisher);
                    Consume(subscriber);

                    if (oauth2Options.TokenExpiresInSeconds > 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            Console.WriteLine("Wait until Token expires. Attempt #" + (i + 1));
                            Thread.Sleep((oauth2Options.TokenExpiresInSeconds + 10) * 1000);
                            Console.WriteLine("Resuming ..");
                            Publish(publisher);
                            _doneEvent.Reset();
                            Consume(subscriber);
                        }
                    }
                }
            }
        }

        private static ICredentialsRefresher GetCredentialsRefresher()
        {
            return new TimerBasedCredentialRefresher();
        }

        public static IChannel declarePublisher(IConnection connection)
        {
            var publisher = connection.CreateChannel();
            publisher.ConfirmSelect();
            publisher.ExchangeDeclare("test_direct", ExchangeType.Direct, true, false);
            return publisher;
        }

        public static void Publish(IChannel publisher)
        {
            const string message = "Hello World!";

            var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(message));
            var properties = new BasicProperties
            {
                AppId = "oauth2",
            };

            publisher.BasicPublish(exchange: _exchange, routingKey: "hello", basicProperties: properties, body: body);

            Console.WriteLine("Sent message");
            publisher.WaitForConfirmsOrDieAsync().Wait();
            Console.WriteLine("Confirmed Sent message");
        }

        public static IChannel declareConsumer(IConnection connection)
        {
            var subscriber = connection.CreateChannel();
            subscriber.QueueDeclare("testqueue", true, false, false);
            subscriber.QueueBind("testqueue", _exchange, "hello");
            return subscriber;
        }

        public static void Consume(IChannel subscriber)
        {
            var asyncListener = new AsyncEventingBasicConsumer(subscriber);
            asyncListener.Received += AsyncListener_Received;
            string consumerTag = subscriber.BasicConsume("testqueue", true, "testconsumer", asyncListener);
            _doneEvent?.WaitOne(1);
            Console.WriteLine("Received message");
            subscriber.BasicCancel(consumerTag);
        }

        private static Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            _doneEvent?.Set();
            return Task.CompletedTask;
        }

        public static OAuth2ClientCredentialsProvider GetCredentialsProvider(OAuth2Options? options)
        {
            if (options == null)
            {
                throw new ArgumentException("There are no OAuth2 Options");
            }

            if (options.ClientId == null)
            {
                throw new ArgumentNullException("oauth2Options.ClientId is null");
            }

            if (options.TokenEndpoint == null)
            {
                throw new ArgumentException("oauth2Options.TokenEndpoint is null");
            }

            Console.WriteLine("OAuth2Client ");
            Console.WriteLine("- ClientId: " + options.ClientId);
            Console.WriteLine("- ClientSecret: " + options.ClientSecret);
            Console.WriteLine("- TokenEndpoint: " + options.TokenEndpoint);
            Console.WriteLine("- Scope: " + options.Scope);

            var tokenEndpointUri = new Uri(options.TokenEndpoint);
            var oAuth2Client = new OAuth2ClientBuilder(options.ClientId, options.ClientSecret, tokenEndpointUri).Build();
            return new OAuth2ClientCredentialsProvider(options.Name, oAuth2Client);
        }
    }
}

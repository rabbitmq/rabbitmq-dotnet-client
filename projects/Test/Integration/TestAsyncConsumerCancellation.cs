using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncConsumerCancellation : IntegrationFixture
    {
        public TestAsyncConsumerCancellation(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestConsumerCancellation()
        {
            string exchangeName = GenerateExchangeName();
            string queueName = GenerateQueueName();
            string routingKey = string.Empty;

            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct);
            await _channel.QueueDeclareAsync(queueName, false, false, true, null);
            await _channel.QueueBindAsync(queueName, exchangeName, routingKey, null);

            var tcsMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsReceivedCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsShutdownCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ShutdownAsync += async (model, ea) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), ea.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcsShutdownCancelled.SetResult(true);
                }
            };

            consumer.ReceivedAsync += async (model, ea) =>
            {
                tcsMessageReceived.SetResult(true);
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), ea.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    tcsReceivedCancelled.SetResult(true);
                }
            };

            await _channel.BasicConsumeAsync(queueName, false, consumer);

            //publisher
            await using IChannel publisherChannel = await _conn.CreateChannelAsync(_createChannelOptions);
            byte[] messageBodyBytes = "Hello, world!"u8.ToArray();
            var props = new BasicProperties();
            await publisherChannel.BasicPublishAsync(exchange: exchangeName, routingKey: string.Empty,
                mandatory: false, basicProperties: props, body: messageBodyBytes);

            await WaitAsync(tcsMessageReceived, TimeSpan.FromSeconds(5), "Consumer received message");

            await _channel.CloseAsync();

            await WaitAsync(tcsReceivedCancelled, TimeSpan.FromSeconds(5), "ReceivedAsync cancelled");
            await WaitAsync(tcsShutdownCancelled, TimeSpan.FromSeconds(5), "ShutdownAsync cancellation");
        }
    }
}

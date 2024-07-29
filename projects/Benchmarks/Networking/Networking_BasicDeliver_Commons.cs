using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Client;

namespace Benchmarks.Networking
{
    [MemoryDiagnoser]
    public class Networking_BasicDeliver_Commons
    {
        public static async Task Publish_Hello_World(IConnection connection, uint messageCount, byte[] body)
        {
            using (IChannel channel = await connection.CreateChannelAsync())
            {
                QueueDeclareOk queue = await channel.QueueDeclareAsync();
                var consumer = new CountingConsumer(messageCount);
                await channel.BasicConsumeAsync(queue.QueueName, true, consumer);

                for (int i = 0; i < messageCount; i++)
                {
                    await channel.BasicPublishAsync("", queue.QueueName, body);
                }

                await consumer.CompletedTask.ConfigureAwait(false);
                await channel.CloseAsync();
            }
        }
    }

    internal sealed class CountingConsumer : AsyncDefaultBasicConsumer
    {
        private int _remainingCount;
        private readonly TaskCompletionSource<bool> _tcs;

        public Task CompletedTask => _tcs.Task;

        public CountingConsumer(uint messageCount)
        {
            _remainingCount = (int)messageCount;
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public override Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            if (Interlocked.Decrement(ref _remainingCount) == 0)
            {
                _tcs.SetResult(true);
            }

            return Task.CompletedTask;
        }
    }
}

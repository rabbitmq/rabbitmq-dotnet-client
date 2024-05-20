using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MassPublish
{
    static class Program
    {
        const string RmqHost = "localhost";

        const string AppId = "MassPublish";
        static readonly ExchangeName ExchangeName = new("MassPublish-ex");
        static readonly QueueName QueueName = new("MassPublish-queue");
        static readonly RoutingKey RoutingKey = new("MassPublish-queue");
        static readonly ConsumerTag ConsumerTag = new("MassPublish-consumer");
        static readonly int ConnectionCount = Environment.ProcessorCount;

        const int BatchesToSend = 64;
        const int ItemsPerBatch = 8192;
        const int TotalMessages = BatchesToSend * ItemsPerBatch;

        static int s_messagesSent;
        static int s_messagesReceived;

        static readonly TaskCompletionSource<bool> s_consumeDoneEvent = new();

        static readonly BasicProperties s_properties = new() { AppId = AppId };

        static readonly Func<AddressFamily, ITcpClient> s_socketFactory = (AddressFamily af) =>
        {
            var socket = new Socket(af, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            return new TcpClientAdapter(socket);
        };

        static readonly ConnectionFactory s_publishConnectionFactory = new()
        {
            HostName = RmqHost,
            ClientProvidedName = AppId + "-PUBLISH",
        };

        static readonly ConnectionFactory s_consumeConnectionFactory = new()
        {
            HostName = RmqHost,
            ClientProvidedName = AppId + "-CONSUME",
            DispatchConsumersAsync = true
        };

        static readonly Random s_random;
        static readonly byte[] s_payload;
        static readonly bool s_debug = false;

        static Program()
        {
            s_random = new Random();
            s_payload = GetRandomBody(1024 * 4);

            s_publishConnectionFactory.SocketFactory = s_socketFactory;
            s_consumeConnectionFactory.SocketFactory = s_socketFactory;
        }

        static async Task Main()
        {
            using IConnection consumeConnection = await s_consumeConnectionFactory.CreateConnectionAsync();
            consumeConnection.ConnectionShutdown += Connection_ConnectionShutdown;

            using IChannel consumeChannel = await consumeConnection.CreateChannelAsync();
            consumeChannel.ChannelShutdown += Channel_ChannelShutdown;
            await consumeChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 128, global: false);

            await consumeChannel.ExchangeDeclareAsync(exchange: ExchangeName,
                type: ExchangeType.Direct, passive: false, durable: false, autoDelete: false, noWait: false, arguments: null);

            await consumeChannel.QueueDeclareAsync(queue: QueueName,
                passive: false, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var asyncListener = new AsyncEventingBasicConsumer(consumeChannel);
            asyncListener.Received += AsyncListener_Received;

            await consumeChannel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey, arguments: null);

            await consumeChannel.BasicConsumeAsync(queue: QueueName, autoAck: true, consumerTag: ConsumerTag,
               noLocal: false, exclusive: false, arguments: null, asyncListener);

            var publishConnections = new List<IConnection>();
            for (int i = 0; i < ConnectionCount; i++)
            {
                IConnection publishConnection = await s_publishConnectionFactory.CreateConnectionAsync($"{AppId}-PUBLISH-{i}");
                publishConnection.ConnectionShutdown += Connection_ConnectionShutdown;
                publishConnections.Add(publishConnection);
            }

            var publishTasks = new List<Task>();
            var watch = Stopwatch.StartNew();

            for (int batchIdx = 0; batchIdx < BatchesToSend; batchIdx++)
            {
                int idx = s_random.Next(publishConnections.Count);
                IConnection publishConnection = publishConnections[idx];

                publishTasks.Add(Task.Run(async () =>
                {
                    using IChannel publishChannel = await publishConnection.CreateChannelAsync();
                    publishChannel.ChannelShutdown += Channel_ChannelShutdown;

                    await publishChannel.ConfirmSelectAsync();

                    for (int i = 0; i < ItemsPerBatch; i++)
                    {
                        await publishChannel.BasicPublishAsync(exchange: ExchangeName, routingKey: RoutingKey,
                            basicProperties: s_properties, body: s_payload, mandatory: true);
                        Interlocked.Increment(ref s_messagesSent);
                    }

                    await publishChannel.WaitForConfirmsOrDieAsync();

                    if (s_debug)
                    {
                        Console.WriteLine("[DEBUG] channel {0} done publishing and waiting for confirms", publishChannel.ChannelNumber);
                    }

                    await publishChannel.CloseAsync();
                }));
            }

            Console.WriteLine($"Sending {BatchesToSend} batches for {ItemsPerBatch} items per batch => Total messages: {TotalMessages}");
            Console.WriteLine();
            Console.WriteLine(" Sent | Received");

            while (false == s_consumeDoneEvent.Task.Wait(500))
            {
                Console.WriteLine($"{s_messagesSent,5} | {s_messagesReceived,5}");
            }
            watch.Stop();
            await Task.WhenAll(publishTasks.ToArray());

            Console.WriteLine($"{s_messagesSent,5} | {s_messagesReceived,5}");
            Console.WriteLine();
            Console.WriteLine($"Took {watch.Elapsed.TotalMilliseconds} ms");

            foreach (IConnection c in publishConnections)
            {
                if (s_debug)
                {
                    Console.WriteLine("[DEBUG] closing connection: {0}", c.ClientProvidedName);
                }

                await c.CloseAsync();
            }

            await consumeChannel.CloseAsync();
        }

        private static void PublishChannel_BasicNacks(object sender, BasicNackEventArgs e)
        {
            Console.Error.WriteLine("[ERROR] unexpected nack on publish: {0}", e);
        }

        private static void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                Console.Error.WriteLine("[ERROR] unexpected connection shutdown: {0}", e);
                s_consumeDoneEvent.TrySetResult(false);
            }
        }

        private static void Channel_ChannelShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                Console.Error.WriteLine("[ERROR] unexpected channel shutdown: {0}", e);
                s_consumeDoneEvent.TrySetResult(false);
            }
        }

        private static Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            if (Interlocked.Increment(ref s_messagesReceived) == TotalMessages)
            {
                s_consumeDoneEvent.SetResult(true);
            }

            return Task.CompletedTask;
        }

        private static byte[] GetRandomBody(int size)
        {
            var body = new byte[size];
            s_random.NextBytes(body);
            return body;
        }
    }
}

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
        const string ExchangeName = "MassPublish-ex";
        const string QueueName = "MassPublish-queue";
        const string RoutingKey = "MassPublish-queue";
        const string ConsumerTag = "MassPublish-consumer";
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
            consumeConnection.ConnectionShutdownAsync += ConnectionShutdownAsync;

            using IChannel consumeChannel = await consumeConnection.CreateChannelAsync();
            consumeChannel.ChannelShutdownAsync += Channel_ChannelShutdownAsync;
            await consumeChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 128, global: false);

            await consumeChannel.ExchangeDeclareAsync(exchange: ExchangeName,
                type: ExchangeType.Direct, passive: false, durable: false, autoDelete: false, noWait: false, arguments: null);

            await consumeChannel.QueueDeclareAsync(queue: QueueName,
                passive: false, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var asyncListener = new AsyncEventingBasicConsumer(consumeChannel);
            asyncListener.ReceivedAsync += AsyncListener_Received;

            await consumeChannel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey, arguments: null);

            await consumeChannel.BasicConsumeAsync(queue: QueueName, autoAck: true, consumerTag: ConsumerTag,
               noLocal: false, exclusive: false, arguments: null, asyncListener);

            var publishConnections = new List<IConnection>();
            for (int i = 0; i < ConnectionCount; i++)
            {
                IConnection publishConnection = await s_publishConnectionFactory.CreateConnectionAsync($"{AppId}-PUBLISH-{i}");
                publishConnection.ConnectionShutdownAsync += ConnectionShutdownAsync;
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
                    publishChannel.ChannelShutdownAsync += Channel_ChannelShutdownAsync;

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
            await consumeConnection.CloseAsync();
        }

        private static void PublishChannel_BasicNacks(object sender, BasicNackEventArgs e)
        {
            Console.Error.WriteLine("[ERROR] unexpected nack on publish: {0}", e);
        }

        private static Task ConnectionShutdownAsync(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                Console.Error.WriteLine("[ERROR] unexpected connection shutdown: {0}", e);
                s_consumeDoneEvent.TrySetResult(false);
            }
            return Task.CompletedTask;
        }

        private static Task Channel_ChannelShutdownAsync(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                Console.Error.WriteLine("[ERROR] unexpected channel shutdown: {0}", e);
                s_consumeDoneEvent.TrySetResult(false);
            }

            return Task.CompletedTask;
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

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

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Runtime.ExceptionServices;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace GH_1749
{
    class GH1749Consumer : AsyncDefaultBasicConsumer
    {
        public GH1749Consumer(IChannel channel) : base(channel)
        {
        }

        protected override Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("{0} [INFO] OnCancelAsync, tags[0]: {1}", DateTime.Now.ToString("s"), consumerTags[0]);
            return base.OnCancelAsync(consumerTags, cancellationToken);
        }
    }

    static class Program
    {
        const string DefaultHostName = "localhost";
        const string ConnectionClientProvidedName = "GH_1749";
        static readonly CancellationTokenSource s_cancellationTokenSource = new();
        static readonly CancellationToken s_cancellationToken = s_cancellationTokenSource.Token;

        static async Task Main(string[] args)
        {
            string hostname = DefaultHostName;
            if (args.Length > 0)
            {
                hostname = args[0];
            }

            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            ConnectionFactory connectionFactory = new()
            {
                HostName = hostname,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(600),
                RequestedHeartbeat = TimeSpan.FromSeconds(600),
                UserName = "guest",
                Password = "guest",
                ClientProvidedName = ConnectionClientProvidedName
            };

            var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
            await using var connection = await connectionFactory.CreateConnectionAsync();

            connection.RecoverySucceededAsync += (object sender, AsyncEventArgs ea) =>
            {
                Console.WriteLine("{0} [INFO] saw RecoverySucceededAsync, event: {1}", Now, ea);
                return Task.CompletedTask;
            };

            connection.CallbackExceptionAsync += Connection_CallbackExceptionAsync;

            connection.ConnectionBlockedAsync += Connection_ConnectionBlockedAsync;
            connection.ConnectionUnblockedAsync += Connection_ConnectionUnblockedAsync;

            connection.ConnectionRecoveryErrorAsync += Connection_ConnectionRecoveryErrorAsync;

            connection.ConnectionShutdownAsync += (object sender, ShutdownEventArgs ea) =>
            {
                Console.WriteLine("{0} [INFO] saw ConnectionShutdownAsync, event: {1}", Now, ea);
                return Task.CompletedTask;
            };

            connection.ConsumerTagChangeAfterRecoveryAsync += Connection_ConsumerTagChangeAfterRecoveryAsync;
            connection.QueueNameChangedAfterRecoveryAsync += Connection_QueueNameChangedAfterRecoveryAsync;

            connection.RecoveringConsumerAsync += Connection_RecoveringConsumerAsync;

            await using var channel = await connection.CreateChannelAsync(options: channelOptions);

            channel.CallbackExceptionAsync += Channel_CallbackExceptionAsync;
            channel.ChannelShutdownAsync += Channel_ChannelShutdownAsync;

            try
            {
                await channel.QueueDeclarePassiveAsync(Guid.NewGuid().ToString());
            }
            catch (OperationInterruptedException)
            {
                await channel.DisposeAsync();
                // rabbitmq-dotnet-client-1749
                // await Task.Delay(2000);
            }
        }

        private static string Now => DateTime.Now.ToString("s");

        private static Task Channel_CallbackExceptionAsync(object sender, CallbackExceptionEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] channel saw CallbackExceptionAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] channel CallbackExceptionAsync, exception: {1}", Now, ea.Exception);
            return Task.CompletedTask;
        }

        private static Task Channel_ChannelShutdownAsync(object sender, ShutdownEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ChannelShutdownAsync, event: {1}", Now, ea);
            return Task.CompletedTask;
            // rabbitmq-dotnet-client-1749
            // return Task.Delay(1000);
        }

        private static void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is ObjectDisposedException)
            {
                Console.WriteLine("{0} [INFO] saw FirstChanceException, exception: {1}", Now, e.Exception);
            }
        }

        private static Task Connection_CallbackExceptionAsync(object sender, CallbackExceptionEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] connection saw CallbackExceptionAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] connection CallbackExceptionAsync, exception: {1}", Now, ea.Exception);
            return Task.CompletedTask;
        }

        private static Task Connection_ConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ConnectionBlockedAsync, event: {1}", Now, ea);
            return Task.CompletedTask;
        }

        private static Task Connection_ConnectionUnblockedAsync(object sender, AsyncEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ConnectionUnlockedAsync, event: {1}", Now, ea);
            return Task.CompletedTask;
        }

        private static Task Connection_ConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ConnectionRecoveryErrorAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] ConnectionRecoveryErrorAsync, exception: {1}", Now, ea.Exception);
            return Task.CompletedTask;
        }

        private static Task Connection_ConsumerTagChangeAfterRecoveryAsync(object sender, ConsumerTagChangedAfterRecoveryEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ConsumerTagChangeAfterRecoveryAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] ConsumerTagChangeAfterRecoveryAsync, tags: {1} {2}", Now, ea.TagBefore, ea.TagAfter);
            return Task.CompletedTask;
        }

        private static Task Connection_QueueNameChangedAfterRecoveryAsync(object sender, QueueNameChangedAfterRecoveryEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw QueueNameChangedAfterRecoveryAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] QueueNameChangedAfterRecoveryAsync, queue names: {1} {2}", Now, ea.NameBefore, ea.NameAfter);
            return Task.CompletedTask;
        }

        private static Task Connection_RecoveringConsumerAsync(object sender, RecoveringConsumerEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw RecoveringConsumerAsync, event: {1}, tag: {2}", Now, ea, ea.ConsumerTag);
            return Task.CompletedTask;
        }
    }
}


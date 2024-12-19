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

namespace GH_1749
{
    class GH1749Consumer : AsyncDefaultBasicConsumer
    {
        public GH1749Consumer(IChannel channel) : base(channel)
        {
        }

        protected override Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("{0} [INFO] OnCancelAsync, tags[0]: {1}", Now, consumerTags[0]);
            return base.OnCancelAsync(consumerTags, cancellationToken);
        }

        private static string Now => DateTime.Now.ToString("o");
    }

    static class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            ConnectionFactory connectionFactory = new()
            {
                AutomaticRecoveryEnabled = true,
                UserName = "guest",
                Password = "guest"
            };

            var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
            await using var connection = await connectionFactory.CreateConnectionAsync();

            connection.RecoverySucceededAsync += Connection_RecoverySucceededAsync;

            connection.CallbackExceptionAsync += Connection_CallbackExceptionAsync;

            connection.ConnectionBlockedAsync += Connection_ConnectionBlockedAsync;
            connection.ConnectionUnblockedAsync += Connection_ConnectionUnblockedAsync;

            connection.ConnectionRecoveryErrorAsync += Connection_ConnectionRecoveryErrorAsync;
            connection.ConnectionShutdownAsync += Connection_ConnectionShutdownAsync;

            connection.ConsumerTagChangeAfterRecoveryAsync += Connection_ConsumerTagChangeAfterRecoveryAsync;
            connection.QueueNameChangedAfterRecoveryAsync += Connection_QueueNameChangedAfterRecoveryAsync;

            connection.RecoveringConsumerAsync += Connection_RecoveringConsumerAsync;

            await using var channel = await connection.CreateChannelAsync(options: channelOptions);

            QueueDeclareOk queue = await channel.QueueDeclareAsync();

            var consumer = new GH1749Consumer(channel);
            await channel.BasicConsumeAsync(queue.QueueName, true, consumer);

            Console.WriteLine("{0} [INFO] consumer is running", Now);
            Console.ReadLine();
        }

        private static void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            Console.WriteLine("{0} [INFO] saw FirstChanceException, exception: {1}", Now, e.Exception);
        }

        private static string Now => DateTime.Now.ToString("o");

        private static Task Connection_RecoverySucceededAsync(object sender, AsyncEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw RecoverySucceededAsync, event: {1}", Now, ea);
            return Task.CompletedTask;
        }

        private static Task Connection_CallbackExceptionAsync(object sender, CallbackExceptionEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw CallbackExceptionAsync, event: {1}", Now, ea);
            Console.WriteLine("{0} [INFO] CallbackExceptionAsync, exception: {1}", Now, ea.Exception);
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

        private static Task Connection_ConnectionShutdownAsync(object sender, ShutdownEventArgs ea)
        {
            Console.WriteLine("{0} [INFO] saw ConnectionShutdownAsync, event: {1}", Now, ea);
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


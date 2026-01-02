// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2026 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CreateChannel
{
    public static class Program
    {
        private const int Repeats = 100;
        private const int ChannelsToOpen = 50;

        private static int channelsOpened;
        private readonly static TaskCompletionSource<bool> s_tcs = new();

        public static async Task Main()
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            var connectionFactory = new ConnectionFactory { };
            await using IConnection connection = await connectionFactory.CreateConnectionAsync();

            var watch = Stopwatch.StartNew();
            _ = Task.Run(async () =>
            {
                var channels = new IChannel[ChannelsToOpen];
                for (int i = 0; i < Repeats; i++)
                {
                    for (int j = 0; j < channels.Length; j++)
                    {
                        channels[j] = await connection.CreateChannelAsync();
                        channelsOpened++;
                    }

                    for (int j = 0; j < channels.Length; j++)
                    {
                        if (j % 2 == 0)
                        {
                            try
                            {
                                await channels[j].QueueDeclarePassiveAsync(Guid.NewGuid().ToString());
                            }
                            catch (Exception)
                            {
                            }
                        }
                        await channels[j].DisposeAsync();
                    }
                }

                s_tcs.SetResult(true);
            });

            Console.WriteLine($"{Repeats} times opening {ChannelsToOpen} channels on a connection. => Total channel open/close: {Repeats * ChannelsToOpen}");
            Console.WriteLine();
            Console.WriteLine("Opened");
            while (false == s_tcs.Task.IsCompleted)
            {
                await Task.Delay(500);
                Console.WriteLine($"{channelsOpened,5}");
            }
            watch.Stop();
            Console.WriteLine($"{channelsOpened,5}");
            Console.WriteLine();
            Console.WriteLine($"Took {watch.Elapsed.TotalMilliseconds} ms");
        }

        private static string Now => DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

        private static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is OperationInterruptedException)
            {
            }
            else
            {
                Console.Error.WriteLine("{0} [ERROR] {1}", Now, e.Exception);
            }
        }
    }
}

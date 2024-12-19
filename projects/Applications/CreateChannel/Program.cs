// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
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
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static async Task Main()
        {
            var doneTcs = new TaskCompletionSource<bool>();

            var connectionFactory = new ConnectionFactory { };
            await using IConnection connection = await connectionFactory.CreateConnectionAsync();

            var watch = Stopwatch.StartNew();
            var workTask = Task.Run(async () =>
            {
                try
                {
                    var channelOpenTasks = new List<Task<IChannel>>();
                    var channelDisposeTasks = new List<ValueTask>();
                    var channels = new List<IChannel>();
                    for (int i = 0; i < Repeats; i++)
                    {
                        for (int j = 0; j < ChannelsToOpen; j++)
                        {
                            channelOpenTasks.Add(connection.CreateChannelAsync());
                        }

                        for (int j = 0; j < channelOpenTasks.Count; j++)
                        {
                            IChannel ch = await channelOpenTasks[j];
                            if (j % 8 == 0)
                            {
                                try
                                {
                                    await ch.QueueDeclarePassiveAsync(Guid.NewGuid().ToString());
                                }
                                catch (OperationInterruptedException)
                                {
                                    await ch.DisposeAsync();
                                }
                                catch (Exception ex)
                                {
                                    _ = Console.Error.WriteLineAsync($"{DateTime.Now:s} [ERROR] {ex}");
                                }
                            }
                            else
                            {
                                channels.Add(ch);
                                channelsOpened++;
                            }
                        }
                        channelOpenTasks.Clear();

                        for (int j = 0; j < channels.Count; j++)
                        {
                            channelDisposeTasks.Add(channels[j].DisposeAsync());
                        }

                        for (int j = 0; j < channels.Count; j++)
                        {
                            await channelDisposeTasks[j];
                        }
                        channelDisposeTasks.Clear();
                    }

                    doneTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    doneTcs.SetException(ex);
                }
            });

            Console.WriteLine($"{Repeats} times opening {ChannelsToOpen} channels on a connection. => Total channel open/close: {Repeats * ChannelsToOpen}");
            Console.WriteLine();
            Console.WriteLine("Opened");
            while (false == doneTcs.Task.IsCompleted)
            {
                Console.WriteLine($"{channelsOpened,5}");
                await Task.Delay(150);
            }
            watch.Stop();
            Console.WriteLine($"{channelsOpened,5}");
            Console.WriteLine();
            Console.WriteLine($"Took {watch.Elapsed}");

            await workTask;
        }
    }
}

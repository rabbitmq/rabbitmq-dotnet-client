// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
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
//---------------------------------------------------------------------------

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Diagnostics;
using System.Globalization;
using RabbitMQ.Client;

class Program
{
    static int _channelsProcessed;
    static readonly TaskCompletionSource<bool> s_tcs = new();
    static readonly ThreadLocal<Random> s_rng = new(() => new Random());

    private static string Now => DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

    static async Task Main(string[] args)
    {
        const int Repeats = 3;
        const int ChannelsToOpen = 20;

        var connectionFactory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            RequestedConnectionTimeout = TimeSpan.FromMilliseconds(60000),
            RequestedHeartbeat = TimeSpan.FromSeconds(600),
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            ContinuationTimeout = TimeSpan.FromMilliseconds(1000)
        };
        await using IConnection connection = await connectionFactory.CreateConnectionAsync();

        var watch = Stopwatch.StartNew();
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < Repeats; i++)
            {
                try
                {
                    var tasks = new Task[ChannelsToOpen];
                    for (int j = 0; j < ChannelsToOpen; j++)
                    {
                        tasks[j] = Task.Run(async () =>
                        {
                            try
                            {
                                IChannel channel = await connection.CreateChannelAsync(
                                    new CreateChannelOptions(true, true));
                                var cts = new CancellationTokenSource();
                                int cancelAfterMs = s_rng.Value!.Next(1, 10000); // upper bound exclusive
                                cts.CancelAfter(cancelAfterMs);
                                var tcs = new TaskCompletionSource<int>();
                                channel.ChannelShutdownAsync += async (sender, args) =>
                                {
                                    await Task.Delay(100);
                                    tcs.TrySetResult(1);
                                };
                                try
                                {
                                    await channel.CloseAsync();
                                }
                                catch (TaskCanceledException ex)
                                {
                                    Console.WriteLine(
                                        $"{Now} CloseAsync canceled after {cancelAfterMs} ms " +
                                        $"{ex.Message}");
                                }
                                catch (OperationCanceledException ex)
                                {
                                    Console.WriteLine(
                                        $"{Now} CloseAsync canceled after {cancelAfterMs} ms" +
                                        $"{ex.Message}");
                                }
                                catch (Exception exClose)
                                {
                                    Console.WriteLine($"{Now} CloseAsync error: {exClose.GetType().Name} {exClose.Message}");
                                }

                                // Wait a bit for the ChannelShutdown event to fire
                                var delayTask = Task.Delay(15000);
                                await Task.WhenAny(tcs.Task, delayTask);
                                await channel.DisposeAsync();
                                cts.Dispose();
                            }
                            catch (Exception exOuter)
                            {
                                Console.WriteLine($"{Now} outer error: {exOuter.GetType().Name} {exOuter.Message}");
                            }
                            finally
                            {
                                Interlocked.Increment(ref _channelsProcessed);
                            }
                        });
                    }
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{Now} connection error: {ex.GetType().Name} {ex.Message}");
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
            Console.WriteLine($"{_channelsProcessed,5}");
        }
        watch.Stop();
        Console.WriteLine($"{_channelsProcessed,5}");
        Console.WriteLine();
        Console.WriteLine($"Took {watch.Elapsed.TotalMilliseconds} ms");
    }
}

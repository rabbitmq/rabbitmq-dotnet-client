using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;

namespace CreateChannel
{
    public static class Program
    {
        private const int Repeats = 100;
        private const int ChannelsToOpen = 50;

        private static int channelsOpened;
        private static AutoResetEvent doneEvent;

        public static async Task Main()
        {
            doneEvent = new AutoResetEvent(false);

            var connectionFactory = new ConnectionFactory { };
            IConnection connection = await connectionFactory.CreateConnectionAsync();

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
                        channels[j].Dispose();
                    }
                }

                doneEvent.Set();
            });

            Console.WriteLine($"{Repeats} times opening {ChannelsToOpen} channels on a connection. => Total channel open/close: {Repeats * ChannelsToOpen}");
            Console.WriteLine();
            Console.WriteLine("Opened");
            while (!doneEvent.WaitOne(500))
            {
                Console.WriteLine($"{channelsOpened,5}");
            }
            watch.Stop();
            Console.WriteLine($"{channelsOpened,5}");
            Console.WriteLine();
            Console.WriteLine($"Took {watch.Elapsed.TotalMilliseconds} ms");

            connection.Dispose();
            Console.ReadLine();
        }
    }
}

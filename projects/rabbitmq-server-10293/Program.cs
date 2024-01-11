using RabbitMQ.Client;

const int connectionCount = 8192;

// Console.WriteLine("[INFO] processor count: {0}", Environment.ProcessorCount);
// ThreadPool.SetMinThreads(connectionCount * 2, connectionCount * 2);

var tasks = new List<Task>();

var cf = new ConnectionFactory
{
    HostName = "shostakovich"
};

var exitCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

async void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    if (exitCompletionSource.TrySetResult())
    {
        Console.WriteLine("[INFO] waiting for connections to close");
        await Task.WhenAll(tasks);
        Console.WriteLine("[INFO] exiting");
    }
}

Console.CancelKeyPress += Console_CancelKeyPress;

for (int i = 0; i < connectionCount; i++)
{
    int connIdx = i;
    tasks.Add(Task.Run(async () =>
    {
        using IConnection conn = await cf.CreateConnectionAsync();
        {
            Console.WriteLine("[INFO] created connection: {0}", connIdx);
            using IChannel ch = await conn.CreateChannelAsync();
            {
                await exitCompletionSource.Task;
                // Console.WriteLine("[INFO] connection {0} stopping at {1}", connIdx, DateTime.Now.ToString("hh:mm:ss.fff"));
                await ch.CloseAsync();
            }

            await conn.CloseAsync();
        }
        // Console.WriteLine("[INFO] connection {0} stopped at {1}", connIdx, DateTime.Now.ToString("hh:mm:ss.fff"));
    }));
}

Console.WriteLine("[INFO] hit any key to exit");
Console.ReadLine();
exitCompletionSource.TrySetResult();
Console.WriteLine("[INFO] waiting for connections to close");
await Task.WhenAll(tasks);
Console.WriteLine("[INFO] exiting");

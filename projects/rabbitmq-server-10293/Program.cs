using RabbitMQ.Client;

int workerThreads = Environment.ProcessorCount;
int completionPortThreads = Environment.ProcessorCount;
ThreadPool.SetMinThreads(workerThreads, completionPortThreads);

const int connectionCount = 100;

var tasks = new List<Task>();

var cf = new ConnectionFactory
{
    HostName = "shostakovich"
};

var exitCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    if (exitCompletionSource.TrySetResult())
    {
        Console.WriteLine("[INFO] waiting for connections to close");
        Task.WaitAll(tasks.ToArray());
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
        Console.WriteLine("[INFO] created connection: {0}", connIdx);
        using IChannel ch = await conn.CreateChannelAsync();
        await exitCompletionSource.Task;
        Console.WriteLine("[INFO] connection {0} stopping", connIdx);
    }));
}

Console.WriteLine("[INFO] hit any key to exit");
Console.ReadLine();
exitCompletionSource.TrySetResult();
Console.WriteLine("[INFO] waiting for connections to close");
Task.WaitAll(tasks.ToArray());
Console.WriteLine("[INFO] exiting");

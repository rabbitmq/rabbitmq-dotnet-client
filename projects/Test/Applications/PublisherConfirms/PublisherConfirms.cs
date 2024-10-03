﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

const int MESSAGE_COUNT = 50_000;
bool debug = false;

await PublishMessagesIndividuallyAsync();
await PublishMessagesInBatchAsync();
await HandlePublishConfirmsAsynchronously();

static Task<IConnection> CreateConnectionAsync()
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    return factory.CreateConnectionAsync();
}

static async Task PublishMessagesIndividuallyAsync()
{
    Console.WriteLine($"{DateTime.Now} [INFO] publishing {MESSAGE_COUNT:N0} messages individually and handling confirms all at once");

    await using IConnection connection = await CreateConnectionAsync();
    await using IChannel channel = await connection.CreateChannelAsync(publisherConfirmations: true,
        publisherConfirmationTracking: true);

    // declare a server-named queue
    QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
    string queueName = queueDeclareResult.QueueName;

    var sw = new Stopwatch();
    sw.Start();

    for (int i = 0; i < MESSAGE_COUNT; i++)
    {
        byte[] body = Encoding.UTF8.GetBytes(i.ToString());
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body);
    }

    await channel.WaitForConfirmsOrDieAsync();

    sw.Stop();

    Console.WriteLine($"{DateTime.Now} [INFO] published {MESSAGE_COUNT:N0} messages individually in {sw.ElapsedMilliseconds:N0} ms");
}

static async Task PublishMessagesInBatchAsync()
{
    Console.WriteLine($"{DateTime.Now} [INFO] publishing {MESSAGE_COUNT:N0} messages and handling confirms in batches");

    await using IConnection connection = await CreateConnectionAsync();
    await using IChannel channel = await connection.CreateChannelAsync();

    // declare a server-named queue
    QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
    string queueName = queueDeclareResult.QueueName;

    int batchSize = 100;
    int outstandingMessageCount = 0;

    var sw = new Stopwatch();
    sw.Start();

    var publishTasks = new List<Task>();
    for (int i = 0; i < MESSAGE_COUNT; i++)
    {
        byte[] body = Encoding.UTF8.GetBytes(i.ToString());
        publishTasks.Add(channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body).AsTask());
        outstandingMessageCount++;

        if (outstandingMessageCount == batchSize)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.WhenAll(publishTasks).WaitAsync(cts.Token);
            publishTasks.Clear();

            await channel.WaitForConfirmsOrDieAsync(cts.Token);
            outstandingMessageCount = 0;
        }
    }

    if (outstandingMessageCount > 0)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await channel.WaitForConfirmsOrDieAsync(cts.Token);
    }

    sw.Stop();
    Console.WriteLine($"{DateTime.Now} [INFO] published {MESSAGE_COUNT:N0} messages in batch in {sw.ElapsedMilliseconds:N0} ms");
}

async Task HandlePublishConfirmsAsynchronously()
{
    Console.WriteLine($"{DateTime.Now} [INFO] publishing {MESSAGE_COUNT:N0} messages and handling confirms asynchronously");

    await using IConnection connection = await CreateConnectionAsync();

    // NOTE: setting trackConfirmations to false because this program
    // is tracking them itself.
    await using IChannel channel = await connection.CreateChannelAsync(publisherConfirmationTracking: false);

    // declare a server-named queue
    QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
    string queueName = queueDeclareResult.QueueName;

    bool publishingCompleted = false;
    var allMessagesConfirmedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var outstandingConfirms = new LinkedList<ulong>();
    var semaphore = new SemaphoreSlim(1, 1);
    async Task CleanOutstandingConfirms(ulong deliveryTag, bool multiple)
    {
        if (debug)
        {
            Console.WriteLine("{0} [DEBUG] confirming message: {1} (multiple: {2})",
                DateTime.Now, deliveryTag, multiple);
        }

        await semaphore.WaitAsync();
        try
        {
            if (multiple)
            {
                do
                {
                    LinkedListNode<ulong>? node = outstandingConfirms.First;
                    if (node is null)
                    {
                        break;
                    }
                    if (node.Value <= deliveryTag)
                    {
                        outstandingConfirms.RemoveFirst();
                    }
                    else
                    {
                        break;
                    }
                } while (true);
            }
            else
            {
                outstandingConfirms.Remove(deliveryTag);
            }
        }
        finally
        {
            semaphore.Release();
        }

        if (publishingCompleted && outstandingConfirms.Count == 0)
        {
            allMessagesConfirmedTcs.SetResult(true);
        }
    }

    channel.BasicAcksAsync += (sender, ea) => CleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
    channel.BasicNacksAsync += (sender, ea) =>
    {
        Console.WriteLine($"{DateTime.Now} [WARNING] message sequence number: {ea.DeliveryTag} has been nacked (multiple: {ea.Multiple})");
        return CleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
    };

    var sw = new Stopwatch();
    sw.Start();

    var publishTasks = new List<Task>();
    for (int i = 0; i < MESSAGE_COUNT; i++)
    {
        string msg = i.ToString();
        byte[] body = Encoding.UTF8.GetBytes(msg);
        ulong nextPublishSeqNo = await channel.GetNextPublishSequenceNumberAsync();
        if ((ulong)(i + 1) != nextPublishSeqNo)
        {
            Console.WriteLine($"{DateTime.Now} [WARNING] i {i + 1} does not equal next sequence number: {nextPublishSeqNo}");
        }
        await semaphore.WaitAsync();
        try
        {
            outstandingConfirms.AddLast(nextPublishSeqNo);
        }
        finally
        {
            semaphore.Release();
        }
        publishTasks.Add(channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body).AsTask());
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await Task.WhenAll(publishTasks).WaitAsync(cts.Token);
    publishingCompleted = true;

    try
    {
        await allMessagesConfirmedTcs.Task.WaitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("{0} [ERROR] all messages could not be published and confirmed within 10 seconds", DateTime.Now);
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine("{0} [ERROR] all messages could not be published and confirmed within 10 seconds", DateTime.Now);
    }

    sw.Stop();
    Console.WriteLine($"{DateTime.Now} [INFO] published {MESSAGE_COUNT:N0} messages and handled confirm asynchronously {sw.ElapsedMilliseconds:N0} ms");
}

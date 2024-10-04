﻿// This source code is dual-licensed under the Apache License, version
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

// const int MESSAGE_COUNT = 50_000;
const int MESSAGE_COUNT = 5;
bool debug = false;

#pragma warning disable CS8321 // Local function is declared but never used

// await PublishMessagesIndividuallyAsync();
// await PublishMessagesInBatchAsync();
await HandlePublishConfirmsAsynchronously();

static Task<IConnection> CreateConnectionAsync()
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    return factory.CreateConnectionAsync();
}

static async Task PublishMessagesIndividuallyAsync()
{
    Console.WriteLine($"{DateTime.Now} [INFO] publishing {MESSAGE_COUNT:N0} messages and handling confirms per-message");

    await using IConnection connection = await CreateConnectionAsync();
    await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions { PublisherConfirmationsEnabled = true, PublisherConfirmationTrackingEnabled = true });

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

    sw.Stop();

    Console.WriteLine($"{DateTime.Now} [INFO] published {MESSAGE_COUNT:N0} messages individually in {sw.ElapsedMilliseconds:N0} ms");
}

static async Task PublishMessagesInBatchAsync()
{
    Console.WriteLine($"{DateTime.Now} [INFO] publishing {MESSAGE_COUNT:N0} messages and handling confirms in batches");

    await using IConnection connection = await CreateConnectionAsync();
    await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions { PublisherConfirmationsEnabled = true, PublisherConfirmationTrackingEnabled = true });

    // declare a server-named queue
    QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
    string queueName = queueDeclareResult.QueueName;

    int batchSize = 500;
    int outstandingMessageCount = 0;

    var sw = new Stopwatch();
    sw.Start();

    var publishTasks = new List<ValueTask>();
    for (int i = 0; i < MESSAGE_COUNT; i++)
    {
        byte[] body = Encoding.UTF8.GetBytes(i.ToString());
        publishTasks.Add(channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body));
        outstandingMessageCount++;

        if (outstandingMessageCount == batchSize)
        {
            foreach (ValueTask vt in publishTasks)
            {
                await vt;
            }
            publishTasks.Clear();
            outstandingMessageCount = 0;
        }
    }

    if (publishTasks.Count > 0)
    {
        foreach (ValueTask vt in publishTasks)
        {
            await vt;
        }
        publishTasks.Clear();
        outstandingMessageCount = 0;
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
    await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions { PublisherConfirmationsEnabled = true, PublisherConfirmationTrackingEnabled = false });

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

    channel.BasicReturnAsync += (sender, ea) =>
    {
        long sequenceNumber = 0;

        IReadOnlyBasicProperties props = ea.BasicProperties;
        if (props.Headers is not null)
        {
            object? maybeSeqNum = props.Headers["x-sequence-number"];
            if (maybeSeqNum is not null)
            {
                sequenceNumber = (long)maybeSeqNum;
            }
        }

        Console.WriteLine($"{DateTime.Now} [WARNING] message sequence number {sequenceNumber} has been basic.return-ed");
        return CleanOutstandingConfirms((ulong)sequenceNumber, false);
    };

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

        var props = new BasicProperties
        {
            Headers = new Dictionary<string, object?>()
            {
                ["x-sequence-number"] = (long)nextPublishSeqNo
            }
        };

        // string rk = queueName;
        string rk = Guid.NewGuid().ToString();
        ValueTask pt = channel.BasicPublishAsync(exchange: string.Empty, routingKey: rk, body: body,
            mandatory: true, basicProperties: props);

        publishTasks.Add(pt.AsTask());
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

# Migrating to RabbitMQ .NET Client 7.x

This document makes note of major changes in the API of this library for
version 7. In addition to this document, please refer to the comprehensive
integration test suites
[here](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/main/projects/Test/Integration)
and
[here](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/main/projects/Test/SequentialIntegration)
that demonstrate these changes.

If you have questions about version 7 of this library, please start a new discussion here:

https://github.com/rabbitmq/rabbitmq-dotnet-client/discussions

## `async` / `await`

The entire public API and internals of this library have been modified to use
the [`Task` asynchronous programming model
(TAP)](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/).
All TAP methods end with an `Async` suffix, and can be `await`-ed.

## Connections and channels

* `IModel` has been renamed to `IChannel`

## Publishing messages

Just create a new instance of the `BasicProperties` class when publishing
messages. The `CreateBasicProperties` method on the old `IModel` interface has
been removed.

## Consuming messages

When a message is delivered to your code via the
`AsyncEventingBasicConsumer.ReceivedAsync` event or by sub-classing
`AsyncDefaultBasicConsumer`, please note that the `ReadOnlyMemory<byte>` that
represents the message body is owned by this library, and that memory is only
valid for application use within the context of the executing `ReceivedAsync`
event or `HandleBasicDeliverAsync` method.

If you wish to use this data _outside_ of these methods, you **MUST** copy the
data for your use:

```
byte[] myMessageBody = eventArgs.Body.ToArray();
```

## Timed-out protocol operations

In 6.x an operation that exceeded `ContinuationTimeout` threw `TimeoutException`. In 7.x it completes as **cancelled** instead, so the awaiter sees an `OperationCanceledException`, in practice a `TaskCanceledException`. Any `catch (TimeoutException)` around an operation such as `QueueDeclareAsync` or `BasicGetAsync` will no longer run.

Telling a timeout from your own cancellation takes a little care:

```csharp
try
{
    await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
        cancellationToken: myToken);
}
catch (OperationCanceledException ex) when (ex.CancellationToken != myToken)
{
    // The operation outran ContinuationTimeout. The request is already on the wire,
    // so the broker may still act on it.
}
```

Use the token carried by the exception rather than checking `myToken.IsCancellationRequested`. A close on an open channel or connection deliberately ignores the caller's token, so that a close already under way is not truncated, which means a cancelled token there does not tell you the request was never sent.

Two paths do not surface it as cancellation at all. `CreateConnectionAsync` wraps it in `BrokerUnreachableException`, and an abort swallows it, so `AbortAsync` can return successfully after waiting out the timeout.

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
catch (OperationCanceledException) when (false == myToken.IsCancellationRequested)
{
    // The operation outran ContinuationTimeout. The budget is armed immediately
    // before the request is sent, so unless the budget is very small the request
    // reached the wire and the broker may still act on it.
}
```

Do not compare against the token carried by the exception. It is internal in both cases - the timeout's own token on a timeout, a linked token on a caller cancel - so comparing it against yours reports a difference either way and cannot tell them apart.

**Your own token answers in one direction only.** If it is not cancelled, it was a timeout, because the client never cancels a token it does not own and nothing else could have produced the cancellation. If it is cancelled, you cannot tell: cancelling your token does not abort the wait for the reply, since nothing registers it against the continuation, so once the request is on the wire the operation runs its full budget and then completes as a timeout with your token cancelled too. Read a cancelled token as "cannot tell", not as "not a timeout".

A close on an open channel or connection is a further exception: those deliberately ignore the caller's token so that a close already under way is not truncated, so there a cancelled token of yours does not even tell you the request was never sent. Whether a timeout should be positively identifiable rather than inferred this way is tracked in [#2019](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/2019).

Some paths do not surface a timeout as cancellation at all. `CreateConnectionAsync` wraps it in `BrokerUnreachableException`; an abort swallows it, so `AbortAsync` can return successfully after waiting out the timeout; and topology recovery wraps it in a `TopologyRecoveryException` that is logged and fails the recovery attempt rather than reaching an event handler, since `ConnectionRecoveryErrorAsync` covers reconnection and not the topology phase. Waiting for a publisher confirmation is not bounded by `ContinuationTimeout` at all, so it is governed only by whatever token you pass.

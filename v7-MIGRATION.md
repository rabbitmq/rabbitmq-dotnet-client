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
`AsyncEventingBasicConsumer.Received` event or by sub-classing
`DefaultAsyncConsumer`, please note that the `ReadOnlyMemory<byte>` that
represents the message body is owned by this library, and that memory is only
valid for application use within the context of the executing `Received` event
or `HandleBasicDeliver` method.

If you wish to use this data _outside_ of these methods, you **MUST** copy the
data for your use:

```
byte[] myMessageBody = eventArgs.Body.ToArray();
```

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

## OpenTelemetry tracing configuration

Tracing configuration now belongs to the connection that performs the traced operations, rather than to process-wide statics on `RabbitMQActivitySource`. Set it on the factory:

```csharp
var factory = new ConnectionFactory();
factory.TracingOptions = new ConnectionTracingOptions
{
    UseRoutingKeyAsOperationName = false,
    // Propagation: null inherits DistributedContextPropagator.Current.
    Propagator = new OpenTelemetryPropagator(), // from RabbitMQ.Client.OpenTelemetry
};
```

Every member is nullable and `null` means "inherit", resolved **per member**. Setting one thing leaves the others alone, so a factory that only changes a span-shaping flag keeps whatever propagation is configured process-wide.

`RabbitMQActivitySource.ContextInjector` and `ContextExtractor` are now `[Obsolete]` warnings. They keep working and will be removed in a future major version. Replace them with `ConnectionTracingOptions.Propagator`, which takes a `System.Diagnostics.DistributedContextPropagator`:

```csharp
// before
RabbitMQActivitySource.ContextInjector = (activity, headers) => { /* ... */ };

// after - a propagator, which also covers extraction and the header names it owns
factory.TracingOptions = new ConnectionTracingOptions { Propagator = myPropagator };
```

To customise OpenTelemetry propagation rather than replace it, decorate the supplied bridge instead of writing a propagator from scratch:

```csharp
factory.UseOpenTelemetryTracing(options =>
    options.Propagator = new MyDecorator(options.Propagator));
```

Why a propagator and not delegates: a library propagates through `DistributedContextPropagator`, and bridging OpenTelemetry's own `TextMapPropagator` is an application-root concern. That is what keeps `RabbitMQ.Client` free of an OpenTelemetry dependency, with the bridge living in `RabbitMQ.Client.OpenTelemetry`.

Note one behaviour change that comes from a dependency rather than from this client. `RabbitMQ.Client.OpenTelemetry` requires `System.Diagnostics.DiagnosticSource` 10.x, whose default propagator is the W3C one; the core client alone resolves 9.x, whose default is the legacy propagator. Baggage is therefore emitted in the `baggage` header when the OpenTelemetry package is installed and in the non-standard `Correlation-Context` header when it is not. If you rely on the header name, set `DistributedContextPropagator.Current` explicitly at your application root.

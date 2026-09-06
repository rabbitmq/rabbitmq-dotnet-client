# RabbitMQ .NET Client - OpenTelemetry Instrumentation

## Introduction

This library makes it easy to instrument your RabbitMQ .NET Client applications with
OpenTelemetry.

There are two things to wire up, and they are separate concerns:

1. **Propagation** - how the client writes trace context into published messages and reads it
   back out of deliveries. This belongs to the connection performing the operation, so it is
   configured on the `ConnectionFactory`.
2. **Collection** - whether a `TracerProvider` observes the spans the client produces. This is
   OpenTelemetry's concern, and is configured on the `TracerProviderBuilder`.

`AddRabbitMQInstrumentation` can do both at once when the factory and the builder are in the
same place. When they are not - the usual case with dependency injection - configure each one
where it is built.

## Examples

### ASP.NET Core / generic host

The factory is registered with the container, so it does not exist yet when `WithTracing`
configures the builder. Configure propagation on the factory as it is created, and let the
builder subscribe to the client's activity sources.

```csharp
using OpenTelemetry.Trace;

var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
{
    new TraceContextPropagator(),
    new BaggagePropagator()
});

Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

builder.Services.AddSingleton<ConnectionFactory>(_ =>
    new ConnectionFactory { HostName = "localhost" }
        .UseOpenTelemetryTracing());

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddRabbitMQInstrumentation()
        .AddConsoleExporter());
```

### Console application

Both objects are on hand here, so the shortcut overload configures the factory and subscribes
the builder in one call.

```csharp
using OpenTelemetry.Trace;

var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
{
    new TraceContextPropagator(),
    new BaggagePropagator()
});

Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

var connectionFactory = new ConnectionFactory { HostName = "localhost" };

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddRabbitMQInstrumentation(connectionFactory)
    .AddConsoleExporter()
    .Build();
```

### Options

`RabbitMQTracingOptions` shapes the spans and carries the propagation delegates. Pass a
configuration action to either `UseOpenTelemetryTracing` or `AddRabbitMQInstrumentation`:

```csharp
new ConnectionFactory { HostName = "localhost" }
    .UseOpenTelemetryTracing(options =>
    {
        // Append the routing key to publish and delivery span names, for example
        // "publish my.routing.key". Default: true.
        options.UseRoutingKeyAsOperationName = true;

        // Parent a delivery span to the publisher's propagated context. When false, that
        // context is attached as a link instead. Default: true.
        options.UsePublisherAsParent = true;
    });
```

The action runs after the OpenTelemetry propagation delegates have been installed, so
`options.ContextInjector` and `options.ContextExtractor` can be wrapped or replaced.

## Scope of the configuration

Configuration set on a `ConnectionFactory` is captured by each connection when it is created,
so later changes to the factory do not affect connections that are already open, and two
factories can be configured differently.

Calling `AddRabbitMQInstrumentation` without a factory instead sets a **process-wide default**,
which applies to every connection whose factory set no `TracingOptions`. That default cannot be
scoped to a single `TracerProvider`: one `ActivitySource` produces a single `Activity` shared by
every provider, and one publish injects a single set of headers, so span shape and propagation
cannot differ per provider. Consequently the last call wins when several providers configure the
default differently, and disposing a provider does not restore the previous values. Configure
the factory when the configuration needs an owner.

The `RabbitMQActivitySource` static properties are the same process-wide default. Setting them
directly is deprecated in favour of `ConnectionFactory.TracingOptions`; see
[#1981](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1981).

## What is emitted

Three activity sources, all subscribed by `AddRabbitMQInstrumentation` via the
`RabbitMQ.Client.*` wildcard:

| Source | Spans |
|---|---|
| `RabbitMQ.Client.Publisher` | `publish` |
| `RabbitMQ.Client.Subscriber` | `deliver`, `fetch`, `fetch (empty)` |
| `RabbitMQ.Client.Connection` | `connection attempt`, `tcp connection attempt` |

Spans follow the OpenTelemetry
[messaging semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/).
A failed operation sets the span status to `Error` along with `error.type` and an exception
event.

By default a `deliver` span is parented to the message creation context propagated by the
publisher, with `UsePublisherAsParent` available to attach it as a link instead.

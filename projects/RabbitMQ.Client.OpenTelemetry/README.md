# RabbitMQ .NET Client - OpenTelemetry Instrumentation

## Introduction
This library makes it easy to instrument your RabbitMQ .NET Client applications with OpenTelemetry.

## Examples
The following examples demonstrate how to use the RabbitMQ .NET Client OpenTelemetry Instrumentation.

### Basic Usage

#### ASP.NET Core Configuration Example
```csharp
using OpenTelemetry.Trace;

// Configure the OpenTelemetry SDK to trace ASP.NET Core, the RabbitMQ .NET Client and export the traces to the console.
// Also configures context propagation to propagate the TraceContext and Baggage using the W3C specification.

var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
{
    new TraceContextPropagator(),
    new BaggagePropagator()
});

Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddRabbitMQInstrumentation()
        .AddConsoleExporter());
```

#### Console Application Configuration Example
```csharp
using OpenTelemetry.Trace;

// Configure the OpenTelemetry SDK to trace ASP.NET Core, the RabbitMQ .NET Client and export the traces to the console.
// Also configures context propagation to propagate the TraceContext and Baggage using the W3C specification.

var compositeTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
{
    new TraceContextPropagator(),
    new BaggagePropagator()
});

Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddRabbitMQInstrumentation()
    .AddConsoleExporter()
    .Build();
```

### Options

`RabbitMQTracingOptions` shapes the spans this client produces. Pass a configuration action to
`AddRabbitMQInstrumentation`:

```csharp
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddRabbitMQInstrumentation(options =>
    {
        // Append the routing key to publish and delivery span names, for example
        // "publish my.routing.key". Set this to false where a high-cardinality routing key
        // would make span names unusable as an aggregation key. Default: true.
        options.UseRoutingKeyAsOperationName = true;

        // Parent a delivery span to the trace context the publisher propagated in the
        // message, so a publish and the deliveries it causes form a single trace.
        // Default: true.
        options.UsePublisherAsParent = true;
    })
    .AddConsoleExporter()
    .Build();
```

## Scope of the configuration

**Tracing configuration is process-wide, not per-`TracerProvider`.** `AddRabbitMQInstrumentation`
writes the static members of `RabbitMQActivitySource`, which every connection in the process reads.
So when more than one `TracerProvider` configures the client, the last call wins, and disposing a
provider does not restore what it replaced.

This is a property of the model rather than an implementation shortcut: one `ActivitySource`
produces a single `Activity` shared by every listener, and one publish injects a single set of
headers, so neither span shape nor propagated context can differ per provider.

**A connection can own its configuration, though, and that is the recommended way.** Set
`ConnectionFactory.TracingOptions`, or call `UseOpenTelemetryTracing` on the factory, and the
connections it creates use that instead of the process-wide default:

```csharp
factory.UseOpenTelemetryTracing(options =>
    options.UseRoutingKeyAsOperationName = true);
```

Every member of `ConnectionTracingOptions` is nullable and `null` means inherit, resolved **per
member**, so setting one span-shaping flag leaves whatever propagation is configured process-wide
untouched. `Propagator` takes a `System.Diagnostics.DistributedContextPropagator`; this package
supplies `OpenTelemetryPropagator`, which bridges OpenTelemetry's own `TextMapPropagator` and is
what `UseOpenTelemetryTracing` installs for you. To extend it rather than replace it, decorate what
is already there:

```csharp
factory.UseOpenTelemetryTracing(options =>
    options.Propagator = new MyDecorator(options.Propagator));
```

Three consequences worth planning around:

- Call `AddRabbitMQInstrumentation` from one place. Two providers configured with different options
  will not each get their own.
- `RabbitMQActivitySource.ContextInjector`, `ContextExtractor`, `UseRoutingKeyAsOperationName` and
  `TracingOptions` are `[Obsolete]` as of 7.3.0. They still work and will be removed in a future
  major version; prefer `ConnectionFactory.TracingOptions`.
- If you do configure the process-wide default directly, save and restore the previous values
  yourself if you need them back. Assigning `null` to `ContextInjector`, `ContextExtractor`, or
  `TracingOptions` throws `ArgumentNullException`.

## What is emitted

Three activity sources, all subscribed by `AddRabbitMQInstrumentation` through the
`RabbitMQ.Client.*` wildcard:

| Source | Spans |
|---|---|
| `RabbitMQ.Client.Publisher` | `publish` |
| `RabbitMQ.Client.Subscriber` | `deliver`, `fetch`, `fetch (empty)` |
| `RabbitMQ.Client.Connection` | `connection attempt`, `tcp connection attempt` |

Spans follow the OpenTelemetry
[messaging semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/).
A failed operation sets the span status to `Error` along with `error.type` and an exception event.

By default a delivery span is parented to the message creation context the publisher propagated.
Whenever a context is extracted it is also attached to the delivery span as a link, in both modes,
so `UsePublisherAsParent = false` drops the parent and leaves the link rather than swapping one for
the other.

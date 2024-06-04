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

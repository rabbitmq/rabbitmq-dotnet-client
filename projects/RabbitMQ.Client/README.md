# RabbitMQ .NET Client

The official .NET client library for [RabbitMQ](https://www.rabbitmq.com/).

## Documentation

* [Tutorials](https://www.rabbitmq.com/tutorials)
* [Client documentation guide](https://www.rabbitmq.com/client-libraries/dotnet)
* [API reference](https://rabbitmq.github.io/rabbitmq-dotnet-client/api/RabbitMQ.Client.html)
* [Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/CHANGELOG.md)

## Diagnostics and Logging

The client emits diagnostic events through a .NET
[EventSource](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource-overview)
named `rabbitmq-client`. Events cover connection lifecycle, recovery decisions,
and errors. These are useful when diagnosing connection recovery problems or
unexpected connection closures.

### Capturing events in-process

Implement a custom `EventListener` and enable the `rabbitmq-client` source at
the desired level:

```csharp
using System.Diagnostics.Tracing;

sealed class RabbitMqEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "rabbitmq-client")
        {
            EnableEvents(eventSource, EventLevel.LogAlways);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        Console.WriteLine($"{e.TimeStamp:o} [{e.Level}] {e.Payload?[0]}");
    }
}
```

Create an instance before opening any connections and keep it alive for the
lifetime of the application:

```csharp
using var listener = new RabbitMqEventListener();

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
// ...
```

### Capturing events out-of-process

Use [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
to collect events from a running process without modifying application code:

```
dotnet-trace collect --process-id <PID> \
    --providers "rabbitmq-client:0xFFFFFFFF:5"
```

The provider argument format is `<name>:<keywords-hex>:<level>` where level `5`
is `LogAlways` (captures Informational, Warning, and Error events).

On Windows, events can also be captured with tools such as PerfView that
support ETW.

## Changes Between 4.0.x and 4.1.0

No changes yet.


## Changes Between 4.0.1 and 4.0.2 (September 1st, 2016)

### TcpClientAdapter Didn't Respect IP Address Family

GH issue: [rabbitmq-dotnet-client#244](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/244)

## Changes Between 4.0.0 and 4.0.1 (August 25th, 2016)

### `ConnectionFactory#CreateConnection` Deadlock

`ConnectionFactory#CreateConnection` could deadlock in some circumstances.

GH issue: [rabbitmq-dotnet-client#239](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/239).

### Occasional `NullReferenceException` in Endpoint Resolution

GH issue: [rabbitmq-dotnet-client#238](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/238)

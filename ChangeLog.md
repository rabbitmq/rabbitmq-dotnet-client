## Changes Between 5.1.0 and 5.1.1

### Deprecate messaging patterns

GitHub PR: [rabbitmq-dotnet-client#654](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/654)

### Fix stack overflow

GitHub PR: [rabbitmq-dotnet-client#578](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/578)

### Byte conversion support

GitHub PR: [rabbitmq-dotnet-client#579](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/579)

### Heartbeat write deadlock fix

GitHub PR: [rabbitmq-dotnet-client#636](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/636)


## Changes Between 5.0.x and 5.1.0

### Batch Publishing

GitHub PR: [rabbitmq-dotnet-client#368](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/368)

### Introduced a Static Property to AmqpTcpEndpoint to specify the default TLS Protocol Version(s)

GitHub PR: [rabbitmq-dotnet-client#389](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/389)

### All Exceptions are Collected When Selecting an Endpoint

GitHub PR: [rabbitmq-dotnet-client#377](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/377)

### Reduced Lock Contention for Frame Writes

GitHub PR: [rabbitmq-dotnet-client#354](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/354)


## Changes Between 4.1.x and 5.0.1

### Async consumers

GitHub PR: [rabbitmq-dotnet-client#307](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/307)

### Enable connection recovery by default

GitHub issue: [rabbitmq-dotnet-client#271](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/271)

### Replace Console.WriteLine logging with EventSource

GitHub issue: [rabbitmq-dotnet-client#94](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/94)

### Add events for connection recovery errors and connection success

GitHub issue: [rabbitmq-dotnet-client#156](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/156)

### noAck renamed to autoAck

GitHub issue: [rabbitmq-dotnet-client#255](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/255)

### Make uri and Uri setters in ConnectionFactory obsolete

GitHub issue: [rabbitmq-dotnet-client#264](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/264)

### Ensure connection recovery does not keep going after the connection has been closed

GitHub issue: [rabbitmq-dotnet-client#294](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/294)

### Synchronize access to the manuallyClosed field in AutorecoveringConnection.

GitHub issue: [rabbitmq-dotnet-client#291](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/291)

### Use concurrent collections inside AutorecoveringConnection

GitHub issue: [rabbitmq-dotnet-client#288](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/288)

### Add property to allow an endpoint to specify the address family

GitHub issue: [rabbitmq-dotnet-client#226](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/226)

### Fix potential EventingBasicConsumer race condition

### Re-introduce a Uri property on IConnectionFactory

GitHub issue: [rabbitmq-dotnet-client#330](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/330)

### Add CreateConnection overload to IConnectionFactory

GitHub PR: [rabbitmq-dotnet-client#325](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/325)

## Changes Between 4.1.0 and 4.1.1

### Fixed XML Documentation Generation

GitHub issue: [rabbitmq-dotnet-client#269](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/269)

Contributed by Brandon Ording.

### Fixed WinRT Project Build

GitHub issue: [rabbitmq-dotnet-client#270](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/270)

Contributed by Brandon Ording.

### `TcpClientAdapter` Nullifies Socket Field on Close

GitHub issue: [rabbitmq-dotnet-client#263](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/263)

### `HeartbeatReadTimerCallback` Handles Potential `NullReferenceException`

GitHub issue: [rabbitmq-dotnet-client#257](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/257)


## Changes Between 4.0.2 and 4.1.0

`4.1.0` was released on **September 14th, 2016**.

### No lock contention at consumer work service

Switched to a "thread-per-model" approach in the `ConsumerWorkService`.

The TaskScheduler property on `ConnectionFactory` has been obsoleted and can no
longer be used to control concurrency.

Utility class changes:

- `BatchingWorkPool` has been removed
- `ConsumerWorkService` no longer has a constructor that takes a `TaskScheduler`
- `ConsumerWorkService.MAX_THUNK_EXECUTION_BATCH_SIZE` has been removed
- `ConsumerWorkService` no longer has the `ExecuteThunk` or `RegisterKey` methods

Contributed by Brandon Ording and Szymon Kulec.

GH issue: [rabbitmq-dotnet-client#251](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/251)


## Changes Between 4.0.1 and 4.0.2 (September 1st, 2016)

### TcpClientAdapter Didn't Respect IP Address Family

GH issue: [rabbitmq-dotnet-client#244](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/244)

## Changes Between 4.0.0 and 4.0.1 (August 25th, 2016)

### `ConnectionFactory#CreateConnection` Deadlock

`ConnectionFactory#CreateConnection` could deadlock in some circumstances.

GH issue: [rabbitmq-dotnet-client#239](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/239).

### Occasional `NullReferenceException` in Endpoint Resolution

GH issue: [rabbitmq-dotnet-client#238](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/238)

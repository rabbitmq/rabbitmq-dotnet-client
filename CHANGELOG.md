## Changes Between 6.2.4 and 6.3.0

GitHub milestone: [`6.3.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/52?closed=1)

## Changes Between 6.2.3 and 6.2.4

GitHub milestone: [`6.2.4`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/55?closed=1)

This release contains some important bug fixes:

* [Fix connection leaks on auto recovery](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1145)
* [Fix buffer overflow when writing long strings](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1162)
* [Fix regression resulting in `ObjectDisposedException`](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1154)
* [Fix regression that could affect consuming after auto recovery](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1148)

## Changes Between 6.2.1 and 6.2.3

GitHub milestone: [`6.2.3`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/54?closed=1)
GitHub milestone: [`6.2.2`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/51?closed=1)

## Changes Between 6.2.0 and 6.2.1

6.2.0 was published incorrectly, resulting in version 6.2.1

GitHub milestone: [`6.2.1`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/50?closed=1).

## Changes Between 6.1.0 and 6.2.0

This releases primarily focuses on efficiency improvements and addressing
bugs introduced in `6.x` releases.

A full list of changes can be found in the GitHub milestone: [`6.2.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/49?closed=1).

Key highlights include:

 * Concurrent publishing on a shared channel is now safer. We still recommend avoiding it when possible
   but safety properties have been improved by changing how outgoing frame sequences are serialised.
   
   Contributed by @bollhals.

   GitHub issue: [#878](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/878)

 * Batch publishing using `System.ReadOnlyMemory<byte>` payloads instead of byte arrays.
 
   Contributed by @danielmarbach.

   GitHub issue: [#865](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/865), [#892](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/892)

## Changes Between 6.0.0 and 6.1.0

This release continues with improvements to memory use and object allocations.

A full list of changes can be found in the GitHub milestone: [`6.1.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/48?closed=1).

## Changes Between 5.2.0 and 6.0.0

This major release of this client introduces substantial improvements
in terms of memory footprint and throughput. They come at the cost
of minor but important **breaking API changes** covered below.

The client now requires .NET Framework 4.6.1 or .NET Standard 2.0.
Earlier versions are no longer supported by the `6.x` series.

Key improvements in this release have been the result of hard work by
our stellar community members (in no particular order): @stebet, @bording,
@Anarh2404, @danielmarbach, and others.

A full list of changes can be found in the GitHub milestone: [`6.0.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/41?closed=1).

### The Switch to System.Memory (and [Significantly Lower Memory Footprint](https://stebet.net/real-world-example-of-reducing-allocations-using-span-t-and-memory-t/) that Comes with It)

The client now uses the [`System.Memory` library](https://www.nuget.org/packages/System.Memory/) for message and command payloads. This significantly
reduces object allocation and GC pressure for heavy workloads but also
**potentially requires application changes**: consumer delivery payloads are now of instance [`System.ReadOnlyMemory<byte>`](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1?view=netcore-3.1)
instead of `byte[]`.

While there's an implicit conversion for these types,
instances of `System.ReadOnlyMemory<byte>` **must be copied or consumed/deserialised before delivery handler completes**.
Holding on to delivered payloads and referencing them at a later point **is no longer safe**.

The same applies to publishers and the `IModel.BasicPublish` method: prefer using `System.ReadOnlyMemory<byte>`
over `byte[]` and dont' assume that this memory can be retained and used outside of the scope of the publishing
function.

GitHub issue: [#732](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/732)

### Timeouts Use `System.TimeSpan`

All timeout arguments now use `System.TimeSpan` values.

GitHub issue: [#688](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/688)

### Reduced Public API Surface

No major changes here but this is potentially breaking. Only public classes that were never meant
to be publicly used have been turned internal to the client.

GitHub issue: [#714](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/714)

### Requires .NET Framework 4.6.1 or .NET Standard 2.0

The client now requires .NET Framework 4.6.1 or .NET Standard 2.0. Earlier versions are no longer
supported.

GitHub issue: [#686](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/686)

### `Microsoft.Diagnostics.Tracing.EventSource` Dependency Dropped

`Microsoft.Diagnostics.Tracing.EventSource` dependency has been removed. It was an annoying
dependency to have for some environments.

### Source Linking

The library now supports source linking.

GitHub issue: [#697](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/697)

### NuGet Source Packages

Source packages are now also distributed via NuGet.

### CRL Checks for Server x.509 (TLS) Certificates

Added a TLS option to enforce CRL checks for server certificates.

GitHub issue: [#500](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/500)



## Changes Between 5.1.2 and 5.2.0

Please see the milestone for all changes:

[GitHub `5.2.0` Milestone](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/42?closed=1)

Selected highlights:

### Add support for `netstandard2.0`

GitHub issues: [#428](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/428), [#435](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/435)

### Re-introduce lock for all socket writes

GitHub PR: [rabbitmq-dotnet-client#702](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/702)



## Changes Between 5.1.1 and 5.1.2

### Bump System.Net.Security to 4.3.2

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

# RabbitMQ .NET Client - Codebase Summary

## Overview

The RabbitMQ .NET Client is a comprehensive AMQP 0-9-1 client library for .NET, maintained by the RabbitMQ team at Broadcom. This is version 7.x, which represents a major architectural shift to fully asynchronous operations using the Task Asynchronous Pattern (TAP).

**Key Characteristics:**
- **Dual-licensed**: Apache License 2.0 and Mozilla Public License 2.0
- **Target Frameworks**: .NET 8.0 and .NET Standard 2.0
- **Language**: C# 12.0 with nullable reference types enabled
- **Current Version**: 7.2.1 (in development)
- **Latest Stable**: 7.2.0 (released November 2025)

## Major Version 7.x Changes

Version 7.x introduced breaking changes from version 6.x:

1. **Full Async/Await**: Entire public API converted to TAP (Task Asynchronous Pattern)
2. **Renamed Interfaces**: `IModel` → `IChannel` (reflecting AMQP terminology)
3. **Memory Management**: `ReadOnlyMemory<byte>` for message bodies with strict ownership semantics
4. **Removed Methods**: `CreateBasicProperties()` removed; use `new BasicProperties()` directly
5. **OpenTelemetry Integration**: Built-in distributed tracing support

## Architecture

### Core Components

#### 1. Connection Management (`IConnection` / `Connection`)

**Location**: `projects/RabbitMQ.Client/Impl/Connection.cs`

The connection layer manages the TCP connection to RabbitMQ broker:

- **Heartbeat Management**: Automatic heartbeat sending/receiving with configurable timeouts
- **Main Loop**: Dedicated task for receiving frames from the broker
- **Session Management**: Multiplexes channels over a single TCP connection
- **Frame Handling**: Uses `IFrameHandler` abstraction for network I/O
- **Shutdown Coordination**: Complex state machine for graceful and abrupt shutdowns

**Key Features:**
- Connection-level events (blocked, unblocked, shutdown)
- OAuth2 secret rotation support via `UpdateSecretAsync`
- Configurable timeouts for handshake and operations
- Thread-safe connection state management

#### 2. Automatic Recovery (`AutorecoveringConnection`)

**Location**: `projects/RabbitMQ.Client/Impl/AutorecoveringConnection.cs`

Wraps `Connection` to provide automatic recovery from network failures:

- **Topology Recovery**: Recreates exchanges, queues, bindings, and consumers
- **Configurable Filters**: `TopologyRecoveryFilter` to exclude entities from recovery
- **Exception Handling**: `TopologyRecoveryExceptionHandler` for custom error handling
- **Recovery Events**: Notifications for recovery success/failure, consumer tag changes, queue name changes
- **Endpoint Rotation**: Uses `IEndpointResolver` to try different broker nodes

**Recovery Process:**
1. Detect connection failure
2. Wait for `NetworkRecoveryInterval`
3. Attempt reconnection to next endpoint
4. Recover topology (exchanges, queues, bindings)
5. Recover consumers
6. Fire recovery events

#### 3. Channel Management (`IChannel` / `Channel`)

**Location**: `projects/RabbitMQ.Client/Impl/Channel.cs` (partial class, with `Channel.BasicPublish.cs` and `Channel.PublisherConfirms.cs`)

Channels are lightweight virtual connections multiplexed over a single TCP connection:

- **RPC Operations**: Synchronous request-response operations with timeout support
- **Publisher Confirms**: Tracks published messages and their acknowledgments
- **Consumer Management**: Registers and dispatches to `IAsyncBasicConsumer` instances
- **Flow Control**: Handles broker-initiated flow control
- **Continuation Queue**: Manages pending RPC operations

**Key Methods:**
- `BasicPublishAsync`: Publishes messages with optional mandatory flag
- `BasicConsumeAsync`: Registers a consumer for a queue
- `BasicAckAsync/BasicNackAsync/BasicRejectAsync`: Message acknowledgment
- `QueueDeclareAsync/ExchangeDeclareAsync`: Topology management
- `BasicQosAsync`: Configure prefetch settings

#### 4. Consumer Dispatching

**Location**: `projects/RabbitMQ.Client/ConsumerDispatching/`

Handles delivery of messages to consumer callbacks:

- **AsyncConsumerDispatcher**: Processes consumer work items asynchronously
- **Concurrency Control**: Configurable via `ConsumerDispatchConcurrency`
- **Work Queue**: Uses `System.Threading.Channels` for efficient work distribution
- **Exception Handling**: Catches and reports consumer exceptions via `CallbackExceptionAsync` event

**Work Types:**
- `Deliver`: Message delivery
- `Cancel`: Consumer cancellation
- `CancelOk`: Consumer cancellation confirmation
- `ConsumeOk`: Consumer registration confirmation
- `Shutdown`: Channel shutdown notification

#### 5. Frame Handling

**Location**: `projects/RabbitMQ.Client/Impl/Frame.cs`, `SocketFrameHandler.cs`

Low-level AMQP frame processing:

- **Frame Types**: Method, Header, Body, Heartbeat
- **Wire Format**: Binary serialization/deserialization of AMQP frames
- **Memory Pooling**: Uses `ArrayPool<byte>` for efficient memory management
- **Socket Management**: Wraps TCP socket with configurable timeouts

#### 6. Protocol Implementation

**Location**: `projects/RabbitMQ.Client/Framing/`

AMQP 0-9-1 protocol implementation:

- **Method Classes**: One class per AMQP method (e.g., `BasicPublish`, `QueueDeclare`)
- **Protocol Constants**: Channel IDs, frame types, reply codes
- **Wire Formatting**: Serialization of primitive types, tables, arrays

### Supporting Infrastructure

#### 1. OpenTelemetry Integration

**Location**: `projects/RabbitMQ.Client/Impl/RabbitMQActivitySource.cs`

Distributed tracing support:

- **Activity Sources**: Separate sources for publisher and subscriber
- **Semantic Conventions**: Follows OpenTelemetry messaging conventions
- **Context Propagation**: Injects/extracts trace context in message headers
- **Configurable**: `UseRoutingKeyAsOperationName` option

**Traced Operations:**
- `BasicPublish`: Producer spans
- `BasicDeliver`: Consumer spans
- `BasicGet`: Polling consumer spans

#### 2. OAuth2 Support

**Location**: `projects/RabbitMQ.Client.OAuth2/`

OAuth2 authentication mechanism:

- **Token Refresh**: Automatic token renewal via `CredentialsRefresher`
- **OAuth2Client**: HTTP client for token endpoint
- **Credentials Provider**: Implements `ICredentialsProvider` interface

#### 3. Event System

**Location**: `projects/RabbitMQ.Client/Events/`

Async event handling infrastructure:

- **AsyncEventHandler**: Delegate type for async event handlers
- **AsyncEventingWrapper**: Thread-safe event invocation with exception handling
- **Event Args**: Strongly-typed event argument classes

**Key Events:**
- Connection: `ConnectionShutdownAsync`, `ConnectionBlockedAsync`, `RecoverySucceededAsync`
- Channel: `ChannelShutdownAsync`, `BasicAcksAsync`, `BasicNacksAsync`, `BasicReturnAsync`
- Consumer: `ReceivedAsync`, `RegisteredAsync`, `UnregisteredAsync`

#### 4. Exception Hierarchy

**Location**: `projects/RabbitMQ.Client/Exceptions/`

Comprehensive exception types:

- **RabbitMQClientException**: Base exception
- **OperationInterruptedException**: Channel/connection closed during operation
- **AlreadyClosedException**: Operation on closed channel/connection
- **BrokerUnreachableException**: Connection establishment failed
- **PublishException**: Message publish failed (nack or return)
- **ProtocolException**: Protocol violations

### Configuration

#### ConnectionFactory

**Location**: `projects/RabbitMQ.Client/ConnectionFactory.cs`

Central configuration point:

```csharp
var factory = new ConnectionFactory
{
    HostName = "localhost",
    Port = 5672,
    VirtualHost = "/",
    UserName = "guest",
    Password = "guest",
    
    // Timeouts
    RequestedHeartbeat = TimeSpan.FromSeconds(60),
    ContinuationTimeout = TimeSpan.FromSeconds(20),
    HandshakeContinuationTimeout = TimeSpan.FromSeconds(10),
    RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
    
    // Recovery
    AutomaticRecoveryEnabled = true,
    TopologyRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
    
    // Performance
    RequestedChannelMax = 2047,
    RequestedFrameMax = 0, // unlimited
    MaxInboundMessageBodySize = 64 * 1024 * 1024,
    ConsumerDispatchConcurrency = 1,
    
    // TLS
    Ssl = new SslOption { Enabled = false }
};
```

#### CreateChannelOptions

**Location**: `projects/RabbitMQ.Client/CreateChannelOptions.cs`

Per-channel configuration:

```csharp
var options = new CreateChannelOptions(
    publisherConfirmationsEnabled: true,
    publisherConfirmationTrackingEnabled: true,
    outstandingPublisherConfirmationsRateLimiter: rateLimiter,
    consumerDispatchConcurrency: 1
);
```

## Key Design Patterns

### 1. Async/Await Throughout

All public APIs are async:
- Connection establishment: `CreateConnectionAsync`
- Channel operations: `BasicPublishAsync`, `QueueDeclareAsync`
- Consumer callbacks: `HandleBasicDeliverAsync`

### 2. Memory Ownership

Strict rules for `ReadOnlyMemory<byte>`:
- Valid only within callback scope
- Must copy if needed beyond callback
- Backed by pooled arrays for efficiency

### 3. RPC Continuation Pattern

Synchronous AMQP operations implemented as async RPC:
1. Send method frame
2. Register continuation in queue
3. Wait for response with timeout
4. Match response to continuation
5. Complete or fault task

### 4. Event-Driven Architecture

Extensive use of async events:
- Connection lifecycle events
- Channel lifecycle events
- Consumer events
- Recovery events

### 5. Graceful Degradation

Multiple levels of shutdown:
- Normal close: Send `connection.close`, wait for `close-ok`
- Abort: Force close with minimal cleanup
- Timeout: Enforce time limits on close operations

## Testing Infrastructure

### Test Projects

1. **Unit Tests** (`projects/Test/Unit/`)
   - Protocol serialization
   - Frame formatting
   - Utility classes

2. **Integration Tests** (`projects/Test/Integration/`)
   - Connection management
   - Channel operations
   - Consumer behavior
   - Publisher confirms
   - Topology recovery

3. **Sequential Integration Tests** (`projects/Test/SequentialIntegration/`)
   - Tests requiring sequential execution
   - Heartbeat tests
   - Connection blocking tests
   - OpenTelemetry tests

4. **OAuth2 Tests** (`projects/Test/OAuth2/`)
   - OAuth2 authentication
   - Token refresh

### Test Infrastructure

**Location**: `projects/Test/Common/`

- **IntegrationFixture**: Base class for integration tests
- **TestConnectionRecoveryBase**: Base for recovery tests
- **RabbitMQCtl**: Wrapper for `rabbitmqctl` commands
- **ToxiproxyManager**: Network failure simulation (in `projects/Test/Integration/`)

## Build and Packaging

### Project Structure

```
rabbitmq-dotnet-client/
├── projects/
│   ├── RabbitMQ.Client/              # Main client library
│   ├── RabbitMQ.Client.OAuth2/       # OAuth2 support (source)
│   ├── RabbitMQ.Client.OAuth2-NuGet/ # OAuth2 NuGet packaging
│   ├── RabbitMQ.Client.OpenTelemetry/ # OTel extensions
│   ├── Test/                         # Test projects
│   ├── Benchmarks/                   # Performance benchmarks
│   ├── Applications/                 # Sample applications
│   ├── toxiproxy-netcore/            # Toxiproxy .NET client (vendored)
│   └── specs/                        # AMQP 0-9-1 spec files
├── .ci/                              # CI configuration
├── .github/workflows/                # GitHub Actions
└── packages/                         # NuGet output
```

### NuGet Packages

1. **RabbitMQ.Client**: Main package
2. **RabbitMQ.Client.OAuth2**: OAuth2 authentication
3. **RabbitMQ.Client.OpenTelemetry**: OpenTelemetry extensions

### Build System

- **SDK**: .NET SDK
- **Versioning**: MinVer (Git-based semantic versioning)
- **Signing**: Strong-named assembly
- **Documentation**: XML documentation generation
- **Trimming**: Trim-compatible for .NET 8.0

## Performance Considerations

### Memory Management

1. **Array Pooling**: Extensive use of `ArrayPool<byte>`
2. **Span/Memory**: Zero-copy operations where possible
3. **RentedMemory**: Custom struct for pooled memory management

### Concurrency

1. **Channel Multiplexing**: Multiple channels over one connection
2. **Consumer Concurrency**: Configurable parallel consumer processing
3. **Lock-Free Operations**: Minimal locking in hot paths

### Network Optimization

1. **Frame Batching**: Multiple frames in single write
2. **Heartbeat Optimization**: Separate heartbeat timer
3. **Socket Options**: Configurable read/write timeouts

## Observability

### Logging

**Location**: `projects/RabbitMQ.Client/Logging/`

- **EventSource**: `RabbitMqClientEventSource` for ETW/EventPipe
- **Counters**: Connection count, channel count, published messages
- **Structured Logging**: Exception details with context

### Metrics

Available via EventSource counters:
- Active connections
- Active channels
- Messages published
- Messages consumed
- Bytes sent/received

### Tracing

OpenTelemetry integration:
- Automatic span creation
- Context propagation
- Semantic conventions compliance

## Security

### Authentication

1. **PLAIN**: Username/password
2. **EXTERNAL**: TLS client certificates
3. **OAuth2**: Token-based authentication

### TLS/SSL

**Location**: `projects/RabbitMQ.Client/SslOption.cs`

Configurable TLS options:
- Protocol version
- Certificate validation
- Server name indication
- Client certificates

### Credentials Management

- **ICredentialsProvider**: Abstraction for credential sources
- **BasicCredentialsProvider**: Static credentials
- **OAuth2CredentialsProvider**: Dynamic token refresh

## Known Issues and Limitations

### Current Issues (as of 7.2.0)

1. **Heartbeat Crashes**: Unhandled exceptions in heartbeat timer callbacks (addressed in 7.2.1)
2. **Publisher Confirm Semaphore**: Unconditional semaphore release on cancellation (addressed in 7.2.1)
3. **Channel Shutdown**: `TryComplete` needed instead of `Complete` during channel shutdown (addressed in 7.2.1)
4. **Auto-delete Entity Recovery**: Recorded bindings not removed for auto-delete entities (addressed in 7.2.1)

### Design Limitations

1. **Single Connection**: No built-in connection pooling
2. **Channel Limit**: Maximum 2047 channels per connection
3. **Frame Size**: Maximum frame size negotiated at connection time
4. **Synchronous RPC**: Only one RPC operation per channel at a time

## Future Directions (7.2.1)

Based on the changelog and issue tracker:

1. **Performance Improvements**: Further optimization of hot paths
2. **Enhanced Observability**: More detailed metrics and tracing
3. **API Refinements**: Minor API improvements based on feedback
4. **Bug Fixes**: Addressing remaining edge cases in shutdown logic

## Development Guidelines

### Code Style

- C# 12.0 features
- Nullable reference types enabled
- Async/await throughout
- XML documentation required
- EditorConfig enforced

### Testing Requirements

- Unit tests for new functionality
- Integration tests for protocol operations
- Sequential tests for timing-sensitive scenarios
- Benchmark tests for performance-critical paths

### Contribution Process

1. Fork repository
2. Create feature branch
3. Write tests
4. Implement feature
5. Update documentation
6. Submit pull request

## Dependencies

### Runtime Dependencies

- **System.IO.Pipelines**: High-performance I/O
- **System.Threading.RateLimiting**: Publisher confirms rate limiting
- **System.Threading.Channels**: Consumer work queue

### .NET Standard 2.0 Additional Dependencies

- **System.Diagnostics.DiagnosticSource**: Activity support
- **System.Memory**: Span/Memory types
- **System.Threading.Channels**: Async channels
- **Microsoft.Bcl.AsyncInterfaces**: IAsyncDisposable

### Development Dependencies

- **xUnit**: Test framework
- **BenchmarkDotNet**: Performance benchmarking
- **Toxiproxy.Net**: Network failure simulation

## Conclusion

The RabbitMQ .NET Client 7.x is a mature, high-performance AMQP client with:

- **Modern async/await API**: Full TAP support
- **Robust recovery**: Automatic topology and connection recovery
- **Production-ready**: Extensive testing and battle-tested in production
- **Observable**: Built-in OpenTelemetry and EventSource support
- **Flexible**: Highly configurable for various use cases
- **Maintained**: Active development by RabbitMQ team at Broadcom

The codebase demonstrates excellent software engineering practices with clear separation of concerns, comprehensive error handling, and extensive test coverage. The version 7.x rewrite successfully modernized the API while maintaining backward compatibility where possible and providing clear migration guidance.

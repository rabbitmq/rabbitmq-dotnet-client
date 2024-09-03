## RabbitMQ .NET Client

[![GitHub Actions rabbitmq-dotnet-client](https://github.com/rabbitmq/rabbitmq-dotnet-client/actions/workflows/main.yaml/badge.svg)](https://github.com/rabbitmq/rabbitmq-dotnet-client/actions/workflows/main.yaml)
[![CodeQL](https://github.com/rabbitmq/rabbitmq-dotnet-client/workflows/CodeQL/badge.svg)](https://github.com/rabbitmq/rabbitmq-dotnet-client/actions/workflows/codeql.yml)

This repository contains source code of the [RabbitMQ .NET client](https://www.rabbitmq.com/dotnet.html).
The client is maintained by the [RabbitMQ team at Broadcom](https://github.com/rabbitmq/).


## Dependency (NuGet Artifact)

The client is [distributed via NuGet](https://www.nuget.org/packages/RabbitMQ.Client/).


## Tutorials and Documentation

 * [Tutorials](https://www.rabbitmq.com/tutorials)
 * [Documentation guide](https://www.rabbitmq.com/client-libraries/dotnet)
 * [API Documentation](https://rabbitmq.github.io/rabbitmq-dotnet-client/api/RabbitMQ.Client.html)


## Supported Platforms and .NET Releases

### 7.x

`7.x` versions of the library require .NET framework 4.6.1 or any supported
.NET version. They also introduce potentially breaking public API changes
covered in the [changelog](CHANGELOG.md) and [migration
guide](v7-MIGRATION.md).

### 6.x

`6.x` versions of the library require .NET framework 4.6.1 or any supported
.NET version. They also introduce potentially breaking public API changes
covered in the [changelog](CHANGELOG.md).

### 5.x and 4.x

`4.x` and `5.x` versions of the library require .NET 4.5.1 or later or .NET
Core. For .NET Core users, 2.0 is the minimum supported version for `5.x`
series.


## Change Log

See [CHANGELOG.md](CHANGELOG.md).


## Building from Source

Please see [How to Run Tests](RUNNING_TESTS.md) for the build and test process overview.


## Contributing

See [Contributing](CONTRIBUTING.md) and [How to Run Tests](RUNNING_TESTS.md).


## License

This package, the RabbitMQ .NET client library, is double-licensed under
the Mozilla Public License 2.0 ("MPL") and the Apache License version 2 ("ASL").

This means that the user can consider the library to be licensed under **any of the licenses from the list** above.
For example, you may choose the Apache Public License 2.0 and include this client into a commercial product.

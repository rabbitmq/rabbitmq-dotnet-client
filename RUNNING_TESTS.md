## Overview

.NET client's test suite assumes there's a RabbitMQ node listening on `localhost:5672`
(the default settings). SSL tests require a broker listening on the default
SSL port. Connection recovery tests assume `rabbitmqctl` at `../rabbitmq-server/scripts/rabbitmqctl`
can control the running node: this is the case when all repositories are cloned using
the [umbrella repository](https://github.com/rabbitmq/rabbitmq-public-umbrella).

It is possible to use Visual Studio Community Edition .NET Core, and
`dotnet.exe` in `PATH`, to build the client and run the test suite.


## Building

Before this project can be opened in Visual Studio, it's necessary to pull down dependencies
and perform protocol encoder/decoder code generation.

On Windows run:

``` powershell
build.bat
```

On MacOS and linux run:

``` shell
build.sh
```

This will complete the code AMQP 0-9-1 protocol code generation and build all projects. After this open the solution in Visual Studio.


## Test Environment Requirements

Tests can be run from Visual Studio using the NUnit Test Adapter.  Note that it
may take some time for the adapter to discover tests in the assemblies.

The test suite assumes there's a RabbitMQ node running locally with all
defaults, and the tests will need to be able to run commands against the
[`rabbitmqctl`](https://www.rabbitmq.com/rabbitmqctl.8.html) tool for that node.
Two options to accomplish this are covered below.

### Option One: Using a RabbitMQ Release

It is possible to install and run a node using any [binary build](https://www.rabbitmq.com/download.html) suitable
for the platform. Its [CLI tools]() then must be added to `PATH` so that `rabbitmqctl` (`rabbitmqctl.bat` on Windows)
can be invoked directly without using an absolute file path.


### Option Two: Using RabbitMQ Umbrella Repository

Team RabbitMQ uses [rabbitmq-public-umbrella](https://github.com/rabbitmq/rabbitmq-public-umbrella),
which makes it easy to run a RabbitMQ node [built from source](https://www.rabbitmq.com/build-server.html):

```
git clone https://github.com/rabbitmq/rabbitmq-public-umbrella umbrella
cd umbrella
make co
cd deps/rabbit
make
make run-broker
```

`rabbitmqctl` location will be computed using a relative path in the umbrella.
It is possible to override the location using `RABBITMQ_RABBITMQCTL_PATH`:

```
RABBITMQ_RABBITMQCTL_PATH=/path/to/rabbitmqctl dotnet test projects/Unit
```

### Option Three: Using a Docker Container

It is also possible to run a RabbitMQ node in a [Docker](https://www.docker.com/) container.  Set the environment variable `RABBITMQ_RABBITMQCTL_PATH` to `DOCKER:<container_name>` (for example `DOCKER:rabbitmq01`). This tells the unit tests to run the `rabbitmqctl` commands through Docker, in the format `docker exec rabbitmq01 rabbitmqctl <args>`:

``` shell
docker run -d --hostname rabbitmq01 --name rabbitmq01 -p 15672:15672 -p 5672:5672 rabbitmq:3-management
```

## Running All Tests

Then, to run the tests use:

``` powershell
run-test.bat
```

On MacOS, Linux, BSD use:

``` shell
run-test.sh
```

## Running Individual Suites or Test Casess

Running individual tests and fixtures on Windows is trivial using the Visual Studio test runner.
To run a specific tests fixture on MacOS or Linux, use the NUnit filter expressions to select the tests to be run:

``` shell
dotnet test projects/Unit --filter "Name~TestAmqpUriParseFail"

dotnet test projects/Unit --filter "FullyQualifiedName~RabbitMQ.Client.Unit.TestHeartbeats"
```

## Running Tests for a Specific .NET Target

To only run tests on .NET Core:

``` shell
dotnet test projects/Unit -f netcoreapp3.1 projects/Unit
```

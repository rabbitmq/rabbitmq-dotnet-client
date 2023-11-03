# Overview

The RabbitMQ .NET client's test suite assumes there's a RabbitMQ node listening
on `localhost:5672` (the default settings). TLS tests require a node listening
on the default [TLS port](https://rabbitmq.com/ssl.html).

It is possible to use Visual Studio Community Edition and `dotnet.exe` in
`PATH`, to build the client and run the test suite.


## Building


### Windows

```powershell
build.ps1
```

### MacOS and Linux

```shell
dotnet build ./Build.csproj
```

This will build all projects. After this open the solution in Visual Studio.


## Test Environment Requirements

Tests can be run from Visual Studio using the XUnit Test Adapter.  Note that it
may take some time for the adapter to discover tests in the assemblies.

The test suite assumes there's a RabbitMQ node running locally with all
defaults, and the tests will need to be able to run commands against the
[`rabbitmqctl`](https://www.rabbitmq.com/rabbitmqctl.8.html) tool for that node.
Two options to accomplish this are covered below.


### Option One: Using a RabbitMQ Release

It is possible to install and run a node using any [binary build](https://www.rabbitmq.com/download.html)
suitable for the platform. Its [CLI tools](https://rabbitmq.com/cli.html) then must be added to `PATH` so that `rabbitmqctl` can be
invoked directly without using an absolute file path. Note that this method does *not* work on Windows.

On Windows, you must run unit tests as follows (replace `X.Y.Z` with your RabbitMQ version):

```powershell
$env:RABBITMQ_RABBITMQCTL_PATH='C:\Program Files\RabbitMQ Server\rabbitmq_server-X.Y.Z\sbin\rabbitmqctl.bat'
.\build.ps1 -RunTests
```

### Option Two: Building a RabbitMQ Node from Source

T run a RabbitMQ node [built from source](https://www.rabbitmq.com/build-server.html):

```shell
git clone https://github.com/rabbitmq/rabbitmq-server.git rabbitmq-server
cd rabbitmq-server

# assumes Make is available
make co
cd deps/rabbit
make
make run-broker
```

`rabbitmqctl` location will be computed using a relative path under the source repository,
in this example, it should be `./rabbitmq-server/deps/rabbit/sbin/rabbitmqctl`.

It is possible to override the location using `RABBITMQ_RABBITMQCTL_PATH`:

```
RABBITMQ_RABBITMQCTL_PATH=/path/to/rabbitmqctl dotnet test projects/Test/Unit.csproj
```

### Option Three: Using a Docker Container

It is also possible to run a RabbitMQ node in a
[Docker](https://www.docker.com/) container.  Set the environment variable
`RABBITMQ_RABBITMQCTL_PATH` to `DOCKER:<container_name>` (for example
`DOCKER:rabbitmq01`). This tells the unit tests to run the `rabbitmqctl`
commands through Docker, in the format `docker exec rabbitmq01 rabbitmqctl
<args>`:

```shell
docker run -d --hostname rabbitmq01 --name rabbitmq01 -p 15672:15672 -p 5672:5672 rabbitmq:3-management
```

## Running All Tests

Then, to run the tests use:


### Windows

```powershell
build.ps1 -RunTests
```

### MacOS, Linux, BSD:

```shell
dotnet test ./Build.csproj
```

## Running Individual Suites or Test Cases

Running individual tests and fixtures on Windows is trivial using the Visual Studio test runner.
To run a specific tests fixture on MacOS or Linux, use the NUnit filter expressions to select the tests to be run:

``` shell
dotnet test projects/Test/Unit.csproj --filter "Name~TestAmqpUriParseFail"

dotnet test projects/Test/Unit.csproj --filter "FullyQualifiedName~RabbitMQ.Client.Unit.TestHeartbeats"
```

## Running Tests for a Specific .NET Target

To run tests targeting .NET 6.0:

``` shell
dotnet test --framework net6.0 projects/Unit
```

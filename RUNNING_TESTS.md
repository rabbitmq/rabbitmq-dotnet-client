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

    build.bat

On osx/linux run:

    build.sh

This will complete the code AMQP 0-9-1 protocol code generation and build all projects. After this open the solution in Visual Studio.


## Running Tests

Tests can be run from Visual Studio using the NUnit Test Adapter.  Note that it
may take some time for the adapter to discover tests in the assemblies.

The test suite assumes there's a RabbitMQ node running locally with all
defaults, and the tests will need to be able to run commands against the
[`rabbitmqctl`](https://www.rabbitmq.com/rabbitmqctl.8.html) tool for that node.
Two options to accomplish this are:

1. Team RabbitMQ uses [rabbitmq-public-umbrella](https://github.com/rabbitmq/rabbitmq-public-umbrella), which sets up a local RabbitMQ server [built from source](https://www.rabbitmq.com/build-server.html):
    ```
    git clone https://github.com/rabbitmq/rabbitmq-public-umbrella umbrella
    cd umbrella
    make co
    cd deps/rabbit
    make
    make run-broker
    ```

2. You can load a RabbitMQ node in a [docker](https://www.docker.com/) container. You will need to create an environment variable `RABBITMQ_RABBITMQCTL_PATH` and set it to `DOCKER:<container_name>` (for example `DOCKER:rabbitmq01`). This tells the unit tests to run the `rabbitmqctl` commands through docker, in the format `docker exec rabbitmq01 rabbitmqctl <args>`:
    ```
    docker run -d --hostname rabbitmq01 --name rabbitmq01 -p 15672:15672 -p 5672:5672 rabbitmq:3-management
    ```

    Then, to run the tests on Windows use:

    ```
    run-test.bat
    ```

    On MacOS, Linux, BSD use:

    ```
    run-test.sh
    ```

Running individual tests and fixtures on Windows is trivial using the Visual Studio test runner.
To run a specific tests fixture on MacOS or Linux, use the NUnit filter expressions to select the tests
to be run:

```
dotnet test projects/Unit --filter "Name~TestAmqpUriParseFail"

dotnet test projects/Unit --filter "FullyQualifiedName~RabbitMQ.Client.Unit.TestHeartbeats"
```

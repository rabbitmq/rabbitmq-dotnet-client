## Overview

.NET client's test suite assumes there's a RabbitMQ node listening on localhost:5672
(the default settings). SSL tests require a broker listening on the default
SSL port. Connection recovery tests assume `rabbitmqctl` at `../rabbitmq-server/scripts/rabbitmqctl`
can control the running node: this is the case when all repositories are cloned using
the [umbrella repository](https://github.com/rabbitmq/rabbitmq-public-umbrella).

It is possible to use [Visual Studio 2015 Community Edition](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx),
.NET Core, and `dotnet.exe` in `PATH`, to build the client and run the test suite.

## Building

Before this project can be opened in Visual Studio, it's necessary to pull down dependencies
and perform protocol encoder/decoder code generation.

On Windows run:

    build.bat

On osx/linux run:

    build.sh

This will complete the code AMQP 0-9-1 protocol code generation and build all projects. After this open the solution in Visual Studio.


## Running Tests

Tests can be run from Visual Studio using [NUnit Test Adapter](https://visualstudiogallery.msdn.microsoft.com/6ab922d0-21c0-4f06-ab5f-4ecd1fe7175d).
Note that it may take some time for the adapter to discover tests in the assemblies.

The test suite assumes there's a RabbitMQ node [built from source](http://www.rabbitmq.com/build-server.html) running locally
with all defaults. Team RabbitMQ uses [rabbitmq-public-umbrella](https://github.com/rabbitmq/rabbitmq-public-umbrella) to
do that:

    git clone https://github.com/rabbitmq/rabbitmq-public-umbrella umbrella
    cd umbrella
    make co
    cd deps/rabbit
    make
    make run-broker

Then, to run the tests on Windows use:

    run-test.bat

On MacOS, Linux, BSD use:

    run-test.sh

Running individual tests and fixtures on Windows is trivial using the Visual Studio test runner.
To run a specific tests fixture on MacOS or Linux, use the NUnit filter expressions to select the tests
to be run:

    dotnet test projects/client/Unit --filter "Name~TestAmqpUriParseFail"


    dotnet test projects/client/Unit --filter "FullyQualifiedName~RabbitMQ.Client.Unit.TestHeartbeats"

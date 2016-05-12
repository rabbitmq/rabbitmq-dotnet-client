## Overview

.NET client's test suite assumes there's a RabbitMQ node listening on localhost:5672
(the default settings). SSL tests require a broker listening on the default
SSL port. Connection recovery tests assume `rabbitmqctl` at `../rabbitmq-server/scripts/rabbitmqctl`
can control the running node: this is the case when all repositories are cloned using
the [umbrella repository](https://github.com/rabbitmq/rabbitmq-public-umbrella).

It is possible to use [Visual Studio Community edition](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx),
.NET 4.5 or later, and `msbuild.exe` in `PATH`, to build the client and run the test suite.


## Building

On Windows run:

    build.bat

On osx/linux run:
    
    build.sh

This will complete the code AMQP spec generation and build all projects. After this open the solution in Visual Studio.


## Running Tests

Tests can be run from Visual Studio using [NUnit Test Adapter](https://visualstudiogallery.msdn.microsoft.com/6ab922d0-21c0-4f06-ab5f-4ecd1fe7175d).
Note that it may take some time for the adapter to discover tests in the assemblies.

Running the tests from the command line is also straightforward you don't need to build first to do this: use

On Windows run:

    run-test.bat

On osx/linux run:
    
    run-test.sh

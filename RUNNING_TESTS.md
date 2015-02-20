## Overview

.NET client's test suite assumes there's a RabbitMQ node listening on localhost:5672
(the default settings). SSL tests require a broker listening on the default
SSL port. Connection recovery tests assume `rabbitmqctl` at `../rabbitmq-server/scripts/rabbitmqctl`
can control the running node: this is the case when all repositories are cloned using
the [umbrella repository](https://github.com/rabbitmq/rabbitmq-public-umbrella).

It is possible to use [Visual Studio 2013 Community](http://www.visualstudio.com/en-us/news/vs2013-community-vs.aspx),
`msbuild.exe`, or `xbuild` (on Mono) to build the client and run the test suite.


## Building

First copy `Local.props.example` to `Local.props` and edit it if needed, specifying whether
you intend to build the client on Mono and what .NET version should be targeted.

Then, build in Visual Studio or build from the command line. To do the latter,
simply run

    msbuild.exe

on Windows and

    xbuild

if you use Mono.


## Running Tests

Tests can be run from Visual Studio using [NUnit Test Adapter](https://visualstudiogallery.msdn.microsoft.com/6ab922d0-21c0-4f06-ab5f-4ecd1fe7175d).
Note that it may take some time for the adapter to discover tests in the assemblies.

Running the tests from the command line is also straightforward: use

    msbuild.exe /t:RunUnitTests projects/client/Unit/RabbitMQ.Client.Unit.csproj

on Windows with .NET and

    xbuild /nologo /t:RunUnitTests projects/client/Unit/RabbitMQ.Client.Unit.csproj | grep -v "warning CS2002"

on Mono.

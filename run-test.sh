#!/bin/sh
export DOTNET_CLI_TELEMETRY_OPTOUT=1
set -e
dotnet test --no-build --logger 'console;verbosity=detailed' ./RabbitMQDotNetClient.sln

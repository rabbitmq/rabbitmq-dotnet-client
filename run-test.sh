#!/bin/sh
set -e
dotnet test --no-build --logger 'console;verbosity=detailed' ./RabbitMQDotNetClient.sln

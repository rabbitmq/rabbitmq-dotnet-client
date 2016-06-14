#!/bin/sh

set -e

dotnet restore ./projects/client/RabbitMQ.Client
dotnet build ./projects/client/RabbitMQ.Client
dotnet restore ./projects/client/Unit
dotnet build ./projects/client/Unit
dotnet test -f netcoreapp1.0 ./projects/client/Unit --where='cat != RequireSMP & cat != LongRunning & cat != GCTest'



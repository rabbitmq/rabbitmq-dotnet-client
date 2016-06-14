#!/bin/sh

set -e

dotnet restore ./projects/client/RabbitMQ.Client
dotnet build ./projects/client/RabbitMQ.Client
dotnet restore ./projects/client/Unit
dotnet build ./projects/client/Unit
cd ./projects/client/Unit
dotnet test -f netcoreapp1.0 --where='cat != RequireSMP & cat != LongRunning & cat != GCTest'



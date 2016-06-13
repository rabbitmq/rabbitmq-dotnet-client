#!/bin/sh

set -e

dotnet restore ./projects/client/RabbitMQ.Client
dotnet build ./projects/client/RabbitMQ.Client
dotnet restore ./projects/client/Unit
dotnet build ./projects/client/Unit
dotnet restore ./projects/client/Unit.Runner
cd ./projects/client/Unit.Runner
dotnet run 



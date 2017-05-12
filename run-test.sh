#!/bin/sh

set -e

dotnet restore ./projects/client/RabbitMQ.Client
dotnet build -f netstandard1.5 ./projects/client/RabbitMQ.Client
dotnet restore ./projects/client/Unit
dotnet build ./projects/client/Unit
# export RABBITMQ_RABBITMQCTL_PATH=$(pwd)/../rabbit/scripts/rabbitmqctl
cd ./projects/client/Unit
# dotnet test -f netcoreapp1.0 -- --where='cat != RequireSMP & cat != LongRunning & cat != GCTest'

dotnet test -f netcoreapp1.0



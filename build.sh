#!/bin/sh

set -e

SCRIPT=$0
SCRIPT_DIR=$(cd $(dirname "$SCRIPT") && pwd)

dotnet restore $SCRIPT_DIR/projects/client/RabbitMQ.Client
dotnet build $SCRIPT_DIR/projects/client/RabbitMQ.Client
dotnet restore $SCRIPT_DIR/projects/client/Unit
dotnet build $SCRIPT_DIR/projects/client/Unit

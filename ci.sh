#!/bin/sh

set -e

SCRIPT=$0
SCRIPT_DIR=$(cd $(dirname "$SCRIPT") && pwd)
RABBIT_DIR=$SCRIPT_DIR/../rabbit

CLIENT_DIR=$SCRIPT_DIR/projects/client/RabbitMQ.Client
UNIT_DIR=$SCRIPT_DIR/projects/client/Unit

dotnet restore $CLIENT_DIR
dotnet build -f netstandard1.5 $CLIENT_DIR
dotnet restore $UNIT_DIR
dotnet build -f netcoreapp1.0 $UNIT_DIR

cd $RABBIT_DIR
make start-background-broker

set +e

cd $UNIT_DIR
dotnet test -f netcoreapp1.0 --where='cat != RequireSMP & cat != GCTest'
EXIT_CODE=$?

set -e
$RABBIT_DIR/scripts/rabbitmqctl stop

exit $EXIT_CODE

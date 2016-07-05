#!/bin/sh

set -e

SCRIPT=$0
SCRIPT_DIR=$(cd $(dirname "$SCRIPT") && pwd)
RABBIT_DIR=$SCRIPT_DIR/../rabbit

cd $RABBIT_DIR
make start-background-broker
cd $SCRIPT_DIR

CLIENT_DIR=$SCRIPT_DIR/projects/client/RabbitMQ.Client
UNIT_DIR=$SCRIPT_DIR/projects/client/Unit

dotnet restore $CLIENT_DIR
dotnet build $CLIENT_DIR
dotnet restore $UNIT_DIR
dotnet build $UNIT_DIR
cd $UNIT_DIR

set +e
dotnet test -f netcoreapp1.0 --where='cat != RequireSMP & cat != LongRunning & cat != GCTest'
EXIT_CODE=$?

set -e
$RABBIT_DIR/scripts/rabbitmqctl stop

exit $EXIT_CODE

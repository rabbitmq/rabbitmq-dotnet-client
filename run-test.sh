#!/bin/sh

set -e

# export RABBITMQ_RABBITMQCTL_PATH=$(pwd)/../rabbit/scripts/rabbitmqctl
cd ./projects/client/Unit

dotnet test -f netcoreapp2.1

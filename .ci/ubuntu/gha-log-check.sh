#!/bin/bash

set -o errexit
set -o pipefail
set -o xtrace
set -o nounset

readonly docker_name_prefix='rabbitmq-dotnet-client'

declare -r rabbitmq_docker_name="$docker_name_prefix-rabbitmq"

if docker logs "$rabbitmq_docker_name" | grep -iF inet_error
then
    echo '[WARNING] found inet_error in RabbitMQ logs' 1>&2
    exit 0
fi

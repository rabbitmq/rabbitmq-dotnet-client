#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

readonly docker_name_prefix='rabbitmq-dotnet-client-oauth2'

function docker_stop
{
    local -r name="$1"
    local -r docker_name="$docker_name_prefix-$name"

    set +o errexit
    if docker stop "$docker_name"
    then
        docker rm "$docker_name"
    fi
    set -o errexit
}

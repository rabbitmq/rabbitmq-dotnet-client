#!/bin/bash

set -o errexit
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir
echo "[INFO] script_dir: '$script_dir'"

source "$script_dir/common.sh"

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

docker_stop rabbitmq
docker_stop uaa
docker_stop keycloak

declare -r docker_network="$docker_name_prefix-net"
docker network rm --force "$docker_network"

#!/bin/bash

set -o errexit
set -o pipefail
set -o xtrace

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir
echo "[INFO] script_dir: '$script_dir'"

readonly docker_name_prefix='rabbitmq-dotnet-client'
readonly docker_network_name="$docker_name_prefix-network"

if [[ ! -v GITHUB_ACTIONS ]]
then
    GITHUB_ACTIONS='false'
fi

if [[ -d $GITHUB_WORKSPACE ]]
then
    echo "[INFO] GITHUB_WORKSPACE is set: '$GITHUB_WORKSPACE'"
else
    GITHUB_WORKSPACE="$(readlink --canonicalize-existing "$script_dir/../..")"
    echo "[INFO] set GITHUB_WORKSPACE to: '$GITHUB_WORKSPACE'"
fi

readonly run_toxiproxy='true'

set -o nounset

declare -r rabbitmq_docker_name="$docker_name_prefix-rabbitmq"
declare -r toxiproxy_docker_name="$docker_name_prefix-toxiproxy"

function start_toxiproxy
{
    if [[ $run_toxiproxy == 'true' ]]
    then
        # sudo ss -4nlp
        echo "[INFO] starting Toxiproxy server docker container"
        docker rm --force "$toxiproxy_docker_name" 2>/dev/null || echo "[INFO] $toxiproxy_docker_name was not running"
        docker run --pull always --detach \
            --name "$toxiproxy_docker_name" \
            --hostname "$toxiproxy_docker_name" \
            --publish 8474:8474 \
            --publish 55670-55680:55670-55680 \
            --network "$docker_network_name" \
            'ghcr.io/shopify/toxiproxy:2.7.0'
    fi
}

function start_rabbitmq
{
    echo "[INFO] starting RabbitMQ server docker container"
    chmod 0777 "$GITHUB_WORKSPACE/.ci/ubuntu/log"
    docker rm --force "$rabbitmq_docker_name" 2>/dev/null || echo "[INFO] $rabbitmq_docker_name was not running"
    docker run --pull always --detach \
        --name "$rabbitmq_docker_name" \
        --hostname "$rabbitmq_docker_name" \
        --publish 5671:5671 \
        --publish 5672:5672 \
        --network "$docker_network_name" \
        --volume "$GITHUB_WORKSPACE/.ci/ubuntu/enabled_plugins:/etc/rabbitmq/enabled_plugins" \
        --volume "$GITHUB_WORKSPACE/.ci/ubuntu/rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro" \
        --volume "$GITHUB_WORKSPACE/.ci/certs:/etc/rabbitmq/certs:ro" \
        --volume "$GITHUB_WORKSPACE/.ci/ubuntu/log:/var/log/rabbitmq" \
        rabbitmq:latest
}

function wait_rabbitmq
{
    set +o errexit
    set +o xtrace

    declare -i count=12
    while (( count > 0 )) && [[ "$(docker inspect --format='{{.State.Running}}' "$rabbitmq_docker_name")" != 'true' ]]
    do
        echo '[WARNING] RabbitMQ container is not yet running...'
        sleep 5
        (( count-- ))
    done

    declare -i count=12
    while (( count > 0 )) && ! docker exec "$rabbitmq_docker_name" epmd -names | grep -F 'name rabbit'
    do
        echo '[WARNING] epmd is not reporting rabbit name just yet...'
        sleep 5
        (( count-- ))
    done

    docker exec "$rabbitmq_docker_name" rabbitmqctl await_startup

    set -o errexit
    set -o xtrace
}

function get_rabbitmq_id
{
    local rabbitmq_docker_id="$(docker inspect --format='{{.Id}}' "$rabbitmq_docker_name")"
    echo "[INFO] '$rabbitmq_docker_name' docker id is '$rabbitmq_docker_id'"
    if [[ -v GITHUB_OUTPUT ]]
    then
        if [[ -f $GITHUB_OUTPUT ]]
        then
            echo "[INFO] GITHUB_OUTPUT file: '$GITHUB_OUTPUT'"
        fi
        echo "id=$rabbitmq_docker_id" >> "$GITHUB_OUTPUT"
    fi
}

function install_ca_certificate
{
    set +o errexit
    hostname
    hostname -s
    hostname -f
    openssl version
    openssl version -d
    set -o errexit

    if [[ $GITHUB_ACTIONS == 'true' ]]
    then
        readonly openssl_store_dir='/usr/lib/ssl/certs'
        sudo cp -vf "$GITHUB_WORKSPACE/.ci/certs/ca_certificate.pem" "$openssl_store_dir"
        sudo ln -vsf "$openssl_store_dir/ca_certificate.pem" "$openssl_store_dir/$(openssl x509 -hash -noout -in $openssl_store_dir/ca_certificate.pem).0"
    else
        echo "[WARNING] you must install '$GITHUB_WORKSPACE/.ci/certs/ca_certificate.pem' manually into your trusted root store"
    fi

    openssl s_client -connect localhost:5671 \
        -CAfile "$GITHUB_WORKSPACE/.ci/certs/ca_certificate.pem" \
        -cert "$GITHUB_WORKSPACE/.ci/certs/client_localhost_certificate.pem" \
        -key "$GITHUB_WORKSPACE/.ci/certs/client_localhost_key.pem" \
        -pass pass:grapefruit < /dev/null
}

docker network create "$docker_network_name" || echo "[INFO] network '$docker_network_name' is already created"

start_toxiproxy

start_rabbitmq

wait_rabbitmq

get_rabbitmq_id

install_ca_certificate

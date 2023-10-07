#!/bin/bash

set -o errexit
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir
echo "[INFO] script_dir: '$script_dir'"

source "$script_dir/common.sh"

declare -r uaa_image_version='75.21.0'
declare -r keycloak_image_version='20.0'
declare -r docker_network="$docker_name_prefix-net"
declare -r rabbitmq_docker_name="$docker_name_prefix-rabbitmq"

function err_todo
{
    echo '[ERROR] TODO' 1>&2
    exit 69
}

function mode_is_uaa
{
    [[ $mode == 'uaa' ]]
}

function mode_is_keycloak
{
    [[ $mode == 'keycloak' ]]
}

function create_network
{
    docker network inspect "$docker_network" >/dev/null 2>&1 || docker network create "$docker_network"
}

function oauth_service_is_not_running
{
    if mode_is_uaa
    then
        [[ "$(curl -s localhost:8080/info | jq -r '.app.version')" != "$uaa_image_version" ]]
    else
        [[ "$(curl -s localhost:8080/health | jq -r '.status')" != 'UP' ]]
    fi
}

function start_rabbitmq
{
    docker rm --force "$rabbitmq_docker_name" 2>/dev/null || echo "[INFO] $rabbitmq_docker_name was not running"
    docker run --detach --name "$rabbitmq_docker_name" \
        --network "$docker_network" \
        --publish 5672:5672 \
        --publish 15672:15672 \
        --volume "$GITHUB_WORKSPACE/projects/OAuth2Test/enabled_plugins:/etc/rabbitmq/enabled_plugins" \
        --volume "$GITHUB_WORKSPACE/projects/OAuth2Test/$mode/rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro" \
        --volume "$GITHUB_WORKSPACE/projects/OAuth2Test/$mode/signing-key/signing-key.pem:/etc/rabbitmq/signing-key.pem:ro" \
        rabbitmq:3-management
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

function start_oauth_service
{
    if mode_is_uaa
    then
        readonly uaa_docker_name="$docker_name_prefix-uaa"
        docker rm --force "$uaa_docker_name" 2>/dev/null || echo "[INFO] $uaa_docker_name was not running"
        docker run --detach --name "$uaa_docker_name" \
            --network "$docker_network" \
            --publish 8080:8080 \
            --env 'UAA_CONFIG_PATH=/uaa' \
            --env 'JAVA_OPTS=-Djava.security.egd=file:/dev/./urandom' \
            --volume "$GITHUB_WORKSPACE/projects/OAuth2Test/uaa:/uaa" \
            "cloudfoundry/uaa:$uaa_image_version"
    else
        readonly keycloak_docker_name="$docker_name_prefix-keycloak"
        docker rm --force "$keycloak_docker_name" 2>/dev/null || echo "[INFO] $keycloak_docker_name was not running"
        docker run --detach --name "$keycloak_docker_name" \
            --network "$docker_network" \
            --publish 8080:8080 \
            --env 'KEYCLOAK_ADMIN=admin' \
            --env 'KEYCLOAK_ADMIN_PASSWORD=admin' \
            --env KC_HEALTH_ENABLED=true \
            --volume "$GITHUB_WORKSPACE/projects/OAuth2Test/keycloak/import:/opt/keycloak/data/import" \
            "quay.io/keycloak/keycloak:$keycloak_image_version" start-dev --metrics-enabled=true --import-realm
    fi
}

function wait_oauth_service
{
    echo '[INFO] start waiting for OAuth service...'
    sleep 10

    set +o errexit
    set +o xtrace

    declare -i count=24
    while ((count > 0)) && oauth_service_is_not_running
    do
        echo '[WARNING] OAuth service is not yet running...'
        sleep 10
        (( count-- ))
    done

    if (( count == 0 ))
    then
        if oauth_service_is_not_running
        then
            echo '[ERROR] OAUTH SERVICE DID NOT START' 1>&2
            docker ps --all
            exit 1
        fi
    fi

    set -o errexit
    set -o xtrace
}

create_network

start_rabbitmq &
start_oauth_service &
wait

wait_rabbitmq
wait_oauth_service

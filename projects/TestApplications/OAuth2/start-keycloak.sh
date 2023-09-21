#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir

source "$script_dir/common.sh"

readonly docker_name="$docker_name_prefix-keycloak"

docker network inspect rabbitmq_net >/dev/null 2>&1 || docker network create rabbitmq_net
docker rm -f "$docker_name" 2>/dev/null || echo "[INFO] keycloak was not running"
echo "[INFO] running $docker_name docker image ..."

readonly signing_key_file="$script_dir/keycloak/signing-key/signing-key.pem"
if [[ -f $signing_key_file ]]
then
    OAUTH2_RABBITMQ_EXTRA_MOUNTS="${OAUTH2_RABBITMQ_EXTRA_MOUNTS:-} --volume $signing_key_file:/etc/rabbitmq/signing-key.pem"
fi

# Note:
# Variables left un-quoted so that words are actually split as args
# shellcheck disable=SC2086
docker run --detach --name "$docker_name" --net rabbitmq_net \
    --publish 8080:8080 \
    --env KEYCLOAK_ADMIN=admin \
    --env KEYCLOAK_ADMIN_PASSWORD=admin \
    --mount type=bind,source="$script_dir/keycloak/import/,target=/opt/keycloak/data/import/" \
    $OAUTH2_RABBITMQ_EXTRA_MOUNTS \
    quay.io/keycloak/keycloak:20.0 start-dev --import-realm

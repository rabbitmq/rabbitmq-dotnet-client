#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir

source "$script_dir/common.sh"

readonly docker_name="$docker_name_prefix-uaa"

readonly uaa_image_tag=${UAA_IMAGE_TAG:-75.21.0}
readonly uaa_image_name=${UAA_IMAGE_NAME:-cloudfoundry/uaa}

docker network inspect rabbitmq_net >/dev/null 2>&1 || docker network create rabbitmq_net
docker rm --force "$docker_name" 2>/dev/null || echo "[INFO] $docker_name was not running"

echo "[INFO] running ${uaa_image_name}:${uaa_image_tag} docker image"

docker run --detach --name "$docker_name" --net rabbitmq_net \
    --publish 8080:8080 \
    --mount "type=bind,source=${script_dir}/uaa,target=/uaa" \
    --env UAA_CONFIG_PATH="/uaa" \
    --env JAVA_OPTS="-Djava.security.egd=file:/dev/./urandom" \
    "${uaa_image_name}:${uaa_image_tag}"

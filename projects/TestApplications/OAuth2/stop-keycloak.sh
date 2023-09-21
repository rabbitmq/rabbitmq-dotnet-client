#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir

source "$script_dir/common.sh"

docker_stop keycloak

#!/bin/bash

readonly docker_name_prefix='rabbitmq-dotnet-client-oauth2'

if [[ -d $GITHUB_WORKSPACE ]]
then
    echo "[INFO] GITHUB_WORKSPACE is set: '$GITHUB_WORKSPACE'"
else
    GITHUB_WORKSPACE="$(readlink --canonicalize-existing "$script_dir/../..")"
    echo "[INFO] set GITHUB_WORKSPACE to: '$GITHUB_WORKSPACE'"
fi

set -o xtrace
set -o nounset

readonly mode="${1:-uaa}"
case $mode in
    keycloak|uaa)
        echo "[INFO] test mode is '$mode'";;
    *)
        echo "[ERROR] invalid test mode: '$mode'" 1>&2
        exit 64
        ;;
esac


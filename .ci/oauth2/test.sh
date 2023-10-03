#!/bin/bash

set -o errexit
set -o pipefail
set -o xtrace

script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly script_dir
echo "[INFO] script_dir: '$script_dir'"

source "$script_dir/common.sh"

export OAUTH2_MODE="$mode"

dotnet test --environment OAUTH2_MODE="$mode" "$GITHUB_WORKSPACE/projects/OAuth2Test/OAuth2Test.csproj" --logger "console;verbosity=detailed" --framework "net6.0"

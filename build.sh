#!/bin/sh

export DOTNET_CLI_TELEMETRY_OPTOUT=1

set -e

if command -v realpath >/dev/null 2>&1
then
    readonly script_dir="$(dirname "$(realpath "$0")")"
else
    readonly script_dir="$(cd "$(dirname "$0")" && pwd)"
fi

cd "$script_dir"

dotnet restore ./RabbitMQDotNetClient.sln
dotnet build ./RabbitMQDotNetClient.sln

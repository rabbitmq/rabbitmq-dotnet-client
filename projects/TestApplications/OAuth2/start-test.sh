#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

readonly mode=${MODE:-keycloak}
readonly client_id=producer

if [[ $mode == keycloak ]]
then
  readonly client_secret=kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn
  readonly token_endpoint="http://localhost:8080/realms/test/protocol/openid-connect/token"
  readonly scope="rabbitmq:configure:*/* rabbitmq:read:*/* rabbitmq:write:*/*"
else
  readonly client_secret=producer_secret
  readonly token_endpoint="http://localhost:8080/oauth/token"
  readonly scope=""
fi

dotnet run --Name "$mode" \
    --ClientId "$client_id" \
    --ClientSecret "$client_secret" \
    --Scope "$scope" \
    --TokenEndpoint "$token_endpoint" \
    --TokenExpiresInSeconds 60

#!/usr/bin/env bash

set -o errexit
set -o nounset
set -o pipefail

declare -i update_count=0

tmp="$(mktemp)"
readonly tmp

function cleanup {
    rm -f "$tmp"
}

trap cleanup EXIT

set +o errexit
npx actions-up --dry-run > "$tmp" 2>&1
set -o errexit

if grep -Fq 'would be updated' "$tmp"
then
    update_count="$(awk '/would be updated/ { print $1 }' "$tmp")"
    declare -ri update_count
fi

if (( update_count > 0 ))
then
    echo "has-updates=true" >> "$GITHUB_OUTPUT"
    echo "update-count=$update_count" >> "$GITHUB_OUTPUT"
else
    echo "has-updates=false" >> "$GITHUB_OUTPUT"
    echo "update-count=0" >> "$GITHUB_OUTPUT"
fi

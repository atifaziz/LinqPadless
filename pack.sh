#!/usr/bin/env bash
[[ -e pack.sh ]] || { echo >&2 "Please cd into the script location before running it."; exit 1; }
set -e
if [ -n "$1" ]; then
    VERSION_SUFFIX="--version-suffix $1"
else
    VERSION_SUFFIX=
fi
./build.sh
dotnet pack -c Release $VERSION_SUFFIX

#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
if [ -n "$1" ]; then
    VERSION_SUFFIX="--version-suffix $1"
else
    VERSION_SUFFIX=
fi
./build.sh
dotnet pack --no-restore --no-build -c Release $VERSION_SUFFIX

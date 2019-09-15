#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
dotnet restore
for c in Debug Release; do
    dotnet build --no-restore -c $c
done

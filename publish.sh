#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
./build.sh
dotnet publish --no-restore --no-build -c Release -o ../dist/bin src

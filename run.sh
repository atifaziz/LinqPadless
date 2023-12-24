#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
echo>&2 "WARNING! Working directory is: $(pwd)"
dotnet run --no-launch-profile -f net8.0 --project src -- "$@"

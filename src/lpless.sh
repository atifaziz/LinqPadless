#!/usr/bin/env bash
DIR="$(dirname "$0")"
dotnet exec $DIR/lpless.dll "$@"

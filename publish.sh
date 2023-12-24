#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
./build.sh
for f in 8.0 6.0; do
    dotnet publish --no-restore --no-build -c Release -f net$f -o dist/bin/$f src
done

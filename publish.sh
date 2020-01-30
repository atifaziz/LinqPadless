#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
./build.sh
for f in 3.1 2.1; do
    dotnet publish --no-restore --no-build -c Release -f netcoreapp$f -o dist/bin/$f src
done

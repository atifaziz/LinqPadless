#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
./build.sh
for f in netcoreapp2.1 \
         netcoreapp2.2 \
         netcoreapp3.0; do
    dotnet publish --no-restore --no-build -c Release -f $f -o dist/bin/$f src
done

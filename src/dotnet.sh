#!/usr/bin/env bash
set -e
which dotnet > /dev/null || (
    >&2 echo dotnet CLI does not appear to be installed, which is needed to build
    >&2 echo and run the script. Visit https://dot.net/ for instruction on how to
    >&2 echo download and install.
    exit 1
)
LINQPADLESS=__LINQPADLESS__
SCRIPT_BASE_NAME=$(basename "$0")
dotnet run -v quiet -p "$(dirname "$0")/${SCRIPT_BASE_NAME%.*}" -- $*

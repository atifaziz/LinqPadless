#!/usr/bin/env bash
set -e
SCRIPT_PATH=$(dirname "$0")
DOTNET_SCRIPT_PATH=$SCRIPT_PATH/$(dirname "$0")
if [ ! -x "$DOTNET_SCRIPT_PATH" ]; then
    DOTNET_SCRIPT_PATH=dotnet-script
    which $DOTNET_SCRIPT_PATH > /dev/null || (
        >&2 echo dotnet-script does not appear to be installed, which is needed to
        >&2 echo run this script. For instructions on how to download and install,
        >&2 echo visit: https://github.com/filipw/dotnet-script
        exit 1
    )
fi
LINQPADLESS=__LINQPADLESS__
SCRIPT_BASE_NAME=$(basename "$0")
"$DOTNET_SCRIPT_PATH" "$(dirname "$0")/${SCRIPT_BASE_NAME%.*}.csx" -- $*

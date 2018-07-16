#!/bin/bash

cd "$(dirname "$(readlink -f "$0")")"
SCRIPT_DIR="$(pwd)"
BINARY_FILE="${SCRIPT_DIR}/BakaChan.dll"

export LD_LIBRARY_PATH="${SCRIPT_DIR}/lib/linux_x64"

exec dotnet "$BINARY_FILE" "$@"

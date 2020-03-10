#!/bin/bash
dotnet "$(dirname "$(readlink -f "$0")")/tap.dll" "$@"

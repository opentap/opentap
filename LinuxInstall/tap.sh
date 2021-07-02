#!/bin/bash
if [[ $(uname) == Darwin ]]; then
    # Mac uses BSD readlink which supports different flags
    relativePath="$(readlink "$0")"
    # relativePath is empty if this is a regular file
    # Otherwise it is a relative path from the link to the real file
    if [[ "$relativePath" ]]; then
        # File is a link; follow it
        pushd "$(dirname $0)" >/dev/null
        pushd "$(dirname "$relativePath")" >/dev/null
        realPath="$(pwd)/tap.dll"
        popd >/dev/null
        popd >/dev/null
        dotnet "$realPath"
    else
        # File is not a link; simply execute the dll file in this location.
        dotnet "$0.dll"
    fi
# We are on linux -- Use GNU Readline normally
else
    dotnet "$(dirname "$(readlink -f "$0")")/tap.dll" "$@"
fi

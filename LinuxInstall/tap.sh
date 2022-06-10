#!/bin/bash

# umask sets the calling process' file mode creation mask
# 002 is the default mask for users. The default mask for root is 0022.

# set umask to 002 which is equivalent to &'ing the permission flags of files created by OpenTAP with -rw-rw-r--
# the default umask for the 'root' user is 022, which would & with -rw-r--r--
# This would cause files created by sudo to be non-writable by the OpenTAP group,
# which can brick the functionality of the installation.
umask 002
if [[ $(uname) == Darwin ]]; then
    # Mac uses BSD readlink which supports different flags

    # relativePath is empty if this is a regular file
    # Otherwise it is a relative path from the link to the real file
    path="$0"
    relativePath="$(readlink "$path")"
    # Keep looping until the file is resolved to a regular file
    while [[ "$relativePath" ]]; do
        # File is a link; follow it
        pushd "$(dirname "$path")" >/dev/null
        pushd "$(dirname "$relativePath")" >/dev/null
        path="$(pwd)/$(basename "$0")"
        popd >/dev/null
        popd >/dev/null
        relativePath="$(readlink "$path")"
    done
    exec dotnet "$path.dll" "$@"
# We are on linux -- Use GNU Readline normally
else
    # use exec to replace the current process instead of starting a child process
    exec dotnet "$(dirname "$(readlink -f "$0")")/tap.dll" "$@" 
fi

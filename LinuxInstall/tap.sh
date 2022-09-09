#!/bin/bash

# umask sets the calling process' file mode creation mask
# 002 is the default mask for users. The default mask for root is 0022.

# set umask to 002 which is equivalent to &'ing the permission flags of files created by OpenTAP with -rw-rw-r--
# the default umask for the 'root' user is 022, which would & with -rw-r--r--
# This would cause files created when running as root to be non-writable by the OpenTAP group,
# which can cause e.g. package installs and logfiles to behave unexpectedly.
umask 002

TapDllPath="";
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

    TapDllPath="$path.dll";
  else
    # We are on linux -- Use GNU Readline normally
    TapDllPath="$(dirname "$(readlink -f "$0")")/tap.dll";
fi

TapDllDir="$(dirname "$TapDllPath")"
# -x checks if TapDllPath exists and is executable
# -w checks if TapDllDir exists and is writable by the current user.
if [ -w "$TapDllDir" ]; then
  # If the user cannot write to the installation, OpenTAP will not work correctly.
  # Instead, we should give a hint about how to resolve the issue.
  # use exec to replace the current process instead of starting a child process
  exec dotnet "$TapDllPath" "$@"
else
  TapDllGroupOwner="$(stat -c "%G" "$TapDllDir")"
  echo "User $USER does not have write access in the OpenTAP installation at '$TapDllDir'."
  echo "This installation belongs to the group '$TapDllGroupOwner'. The user can be added to this group with the command 'usermod -a -G $TapDllGroupOwner $USER'."
fi

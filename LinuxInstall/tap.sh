#!/bin/sh

# umask sets the calling process' file mode creation mask
# 002 is the default mask for users. The default mask for root is 0022.

# set umask to 002 which is equivalent to &'ing the permission flags of files created by OpenTAP with -rw-rw-r--
# the default umask for the 'root' user is 022, which would & with -rw-r--r--
# This would cause files created when running as root to be non-writable by the OpenTAP group,
# which can cause e.g. package installs and logfiles to behave unexpectedly.
umask 002

# realpath requires coreutils on osx which we can not necessarily rely on
realpath() {
  local path="$1"
  local relativePath="$(readlink "$path")"
  # Keep looping until the file is resolved to a regular file
  while [ "$relativePath" ]; do
    # File is a link; follow it
    local here="$(pwd)"
    cd "$(dirname "$path")"
    cd "$(dirname "$relativePath")"
    path="$(pwd)/$(basename "$relativePath")"
    cd "$here"
    relativePath="$(readlink "$path")"
    if [ "$path" = "$relativePath" ]; then
      # infinite loop
      echo "Error resolving link." >&2
      return 1
    fi
  done
  echo "$path"
}

TapPath="$(realpath "$0")"
TapDllDir="$(dirname "$TapPath")"
TapDllPath="$TapDllDir/tap.dll"

if ! [ -f "$TapDllPath" ]; then
  echo "File does not exist: '$TapDllPath'"
  echo "This could mean:"
  echo "  1) OpenTAP is broken due to a partial update or uninstall"
  echo "  2) The OpenTAP installation was moved"
  echo "Please reinstall OpenTAP"
  exit 1
fi

# -w checks if TapDllDir exists and is writable by the current user.
if [ -w "$TapDllDir" ]; then
  # use exec to replace the current process instead of starting a child process
  exec dotnet "$TapDllDir/tap.dll" "$@"
else
  # If the user cannot write to the installation, OpenTAP will not work correctly.
  # Instead, we should give a hint about how to resolve the issue.
  TapDllGroupOwner="$(stat -c "%G" "$TapDllDir")"
  echo "User $USER does not have write access in the OpenTAP installation at '$TapDllDir'."
  echo "This installation belongs to the group '$TapDllGroupOwner'. The user can be added to this group with the command 'usermod -a -G $TapDllGroupOwner $USER'."
fi

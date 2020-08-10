#!/bin/sh
#
# Script to install OpenTAP. Run this after untar.
#
# Copyright Keysight Technologies 2012-2019
#
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at http://mozilla.org/MPL/2.0/.

NETCORE_INSTALLED="$(dotnet --list-runtimes | grep -E "NETCore\.App.2")"
if [ -z "$NETCORE_INSTALLED" ]; then
    printf 'OpenTAP depends on dotnet runtime 2.1 which is not installed.
Please see https://docs.microsoft.com/en-us/dotnet/core/install/runtime for installation instructions
'
fi

DEST_DIR="$HOME/.tap"
BIN_DIR="$HOME/bin"

echo "TAP will be installed in $DEST_DIR, and shortcuts in $BIN_DIR"

if [ -e "$DEST_DIR" ] && [ -n "$(ls -A "$DEST_DIR")" ]; then
    echo "Warning: $DEST_DIR is not empty. This script is intended for clean installs."
    echo "If you are upgrading OpenTAP, the preferred method is 'tap package install OpenTAP'."
    echo "Upgrading with this script may lead to a broken install."
fi

while true; do
    read -r -p 'Do you wish to install OpenTAP?
' yn

    case $yn in
    [Yy]*) break ;;
    [Nn]*) exit ;;
    *) echo "Please answer yes or no." ;;
    esac
done

mkdir -p "$DEST_DIR"
mkdir -p "$BIN_DIR"

echo "Unzipping tap"
unzip "$(pwd)/*.TapPackage" -d "$DEST_DIR" # *: match OpenTAPLinux and just TAPLinux.

cd "$DEST_DIR" || exit 1
chmod -R +w .

chmod +x tap

cd "$BIN_DIR" || exit 1

echo "Creating shortcut"
ln -s -f "$DEST_DIR/tap" tap

echo "Installer finished"

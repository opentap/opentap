#!/bin/sh
#
# Script to install OpenTAP. Run this after untar.
#
# Copyright Keysight Technologies 2012-2019
#
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at http://mozilla.org/MPL/2.0/.
DEST_DIR=$HOME/.tap
BIN_DIR=$HOME/bin

echo "TAP will be installed in $DEST_DIR, and shortcuts in $BIN_DIR"
while true; do
    read -p "Do you wish to install?" yn
    case $yn in
        [Yy]* ) break;;
        [Nn]* ) exit;;
        * ) echo "Please answer yes or no.";;
    esac
done

CUR_DIR=`pwd`

mkdir -p $DEST_DIR
mkdir -p $BIN_DIR

cd $DEST_DIR
unzip $CUR_DIR/*.TapPackage -d $DEST_DIR # *: match OpenTAPLinux and just TAPLinux.
chmod -R +w .

chmod +x tap

cd $BIN_DIR
ln -s -f $DEST_DIR/tap tap

cd "$CUR_DIR"

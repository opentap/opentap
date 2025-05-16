#!/usr/bin/env bash

set -euo pipefail

DEST="./opentap"
OPENTAP_VERSION="9.29.0-rc.3"
EDITOR_VERSION="9.29.0-rc.3"

OS="MacOS"
ARCH="arm64"

if uname | grep -qi darwin ; then
  OS="MacOS"
else
  OS="Linux"
fi

if uname -m | grep -qi arm ; then
  ARCH="arm64"
else
  ARCH="x64"
fi

echo "Detected OS $OS $ARCH"

has() {
	which "$1" 2>/dev/null >/dev/null
}

EDITOR_APP="$HOME/Applications/Keysight Test Automation.app"
PACKAGE_MANAGER_APP="$HOME/Applications/Keysight Package Manager.app"

if [ "$OS" == "MacOS" ]; then
  if [ -d "$EDITOR_APP" ]; then
    echo "$EDITOR_APP already exists."
    exit 0
  fi
  DEST="$HOME/Library/opentap"
fi


if ! (has dotnet && dotnet --list-runtimes | grep -q 9.0) ; then
  echo "OpenTAP requires .NET 9.0 to run. Please install .NET 9.0: https://dotnet.microsoft.com/en-us/download/dotnet/9.0"
  exit 1
fi

if ! (has unzip && has curl) ; then
  echo "This script requires unzip and curl to run. Please install unzip and curl, and try again."
  exit 1
fi

if [ -d "$DEST" ]; then
  echo "OpenTAP install already exists at $DEST."
  exit 0
fi

mkdir -p "$DEST"

echo "Downloading OpenTAP..."
curl -s "http://packages.opentap.io/4.0/Objects/Packages/OpenTAP?version=$OPENTAP_VERSION&os=$OS&architecture=$ARCH" -o /tmp/opentap.zip > /dev/null
echo "Extracting OpenTAP to $DEST ..."
unzip /tmp/opentap.zip -d "$DEST" > /dev/null </dev/tty
rm /tmp/opentap.zip

pushd "$DEST" > /dev/null
chmod +x tap
if ! ./tap image install "Editor:$EDITOR_VERSION,XPF Controls:$EDITOR_VERSION" --merge ; then
  echo "Installation failed. See the session log for details: $DEST/SessionLogs/Latest.txt"
  exit 1;
fi

popd > /dev/null

# Create a MacOS app bundle
if [ "$OS" == "MacOS" ]; then
  ############# CREATE EDITOR APP ######################3
  mkdir -p "$EDITOR_APP/Contents/MacOS"
  mkdir -p "$EDITOR_APP/Contents/Resources"
  curl -s 'https://raw.githubusercontent.com/opentap/opentap/refs/heads/add-editor-install-script/doc/User%20Guide/Editors/Editor.ico' -o "$EDITOR_APP/Contents/Resources/Editor.ico"
  cat > "$EDITOR_APP/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Editor</string>
    <key>CFBundleDisplayName</key>
    <string>Keysight Test Automation</string>
    <key>CFBundleIdentifier</key>
    <string>com.keysight.editor</string>
    <key>CFBundleVersion</key>
    <string>$EDITOR_VERSION</string>
    <key>CFBundlePackageType</key>
    <string>KEYS</string>
    <key>CFBundleExecutable</key>
    <string>editor</string>
    <key>CFBundleIconFile</key>
    <string>Editor.ico</string>
</dict>
</plist>
EOF
  
  cat > "$EDITOR_APP/Contents/MacOS/editor" << EOF
#!/usr/bin/env sh
cd ~/Library/opentap
/usr/local/share/dotnet/dotnet ./Editor.dll
EOF
  chmod +x "$EDITOR_APP/Contents/MacOS/editor"
  printf '\nCreated %s\n' "$EDITOR_APP"

  ############# CREATE PACKAGE MANAGER APP ######################3
  mkdir -p "$PACKAGE_MANAGER_APP/Contents/MacOS"
  mkdir -p "$PACKAGE_MANAGER_APP/Contents/Resources"
  curl -s 'https://raw.githubusercontent.com/opentap/opentap/refs/heads/add-editor-install-script/doc/User%20Guide/Editors/Editor.ico' -o "$PACKAGE_MANAGER_APP/Contents/Resources/PackageManager.ico"
  cat > "$PACKAGE_MANAGER_APP/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>PackageManager</string>
    <key>CFBundleDisplayName</key>
    <string>Keysight Package Manager</string>
    <key>CFBundleIdentifier</key>
    <string>com.keysight.packagemanager</string>
    <key>CFBundleVersion</key>
    <string>$EDITOR_VERSION</string>
    <key>CFBundlePackageType</key>
    <string>KEYS</string>
    <key>CFBundleExecutable</key>
    <string>packagemanager</string>
    <key>CFBundleIconFile</key>
    <string>PackageManager.ico</string>
</dict>
</plist>
EOF
  
  cat > "$PACKAGE_MANAGER_APP/Contents/MacOS/packagemanager" << EOF
#!/usr/bin/env sh
cd ~/Library/opentap
/usr/local/share/dotnet/dotnet ./PackageManager.dll
EOF
  chmod +x "$PACKAGE_MANAGER_APP/Contents/MacOS/packagemanager"
  printf "Created %s\n" "$PACKAGE_MANAGER_APP"
fi

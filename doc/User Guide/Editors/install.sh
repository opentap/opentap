#!/usr/bin/env sh

set -euo pipefail

DEST="./opentap"
OPENTAP_VERSION="9.29.0-rc.3"
EDITOR_VERSION="9.29.0-rc.2"

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

if [ "$OS" == "MacOS" ]; then
  if [ -d Editor.app ]; then
    echo "./Editor.app already exists."
    exit 0
  fi
  DEST="./Editor.app/Contents/opentap"
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
  while true; do
    read -p "Directory '$DEST' exists. Do you wish to install OpenTAP in this directory anyway? [y]es, [n]o: " yn < /dev/tty
    case $yn in
        [Yy]* ) break;;
        [Nn]* ) exit 1;;
        * ) echo "Please answer yes or no.";;
    esac
done
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
  mkdir -p Editor.app/Contents/MacOS
  mkdir -p Editor.app/Contents/Resources
  curl -s 'https://raw.githubusercontent.com/opentap/opentap/refs/heads/add-editor-install-script/doc/User%20Guide/Editors/Editor.ico' -o ./Editor.app/Contents/Resources/Editor.ico
  cat > Editor.app/Contents/Info.plist << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Editor</string>
    <key>CFBundleDisplayName</key>
    <string>Editor</string>
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
  
  cat > Editor.app/Contents/MacOS/editor << EOF
#!/usr/bin/env bash
cd "\$(dirname "\$0")"
cd ../opentap
/usr/local/share/dotnet/dotnet ./Editor.dll
EOF
  chmod +x Editor.app/Contents/MacOS/editor
  printf '\nCreated Editor App Bundle at ./Editor.app\n'
  open ./Editor.app
fi

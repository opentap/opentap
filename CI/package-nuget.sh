#!/usr/bin/env bash

set -e

if [[ "$(uname)" == "Darwin" ]]; then
  HOST_PLATFORM="osx"
  HOST_TAPPLATFORM="MacOS"
  HOST_ARCH="arm64"
else
  HOST_PLATFORM="linux"
  HOST_TAPPLATFORM="Linux"
  if [[ "$(uname -p)" == "aarch64" ]]; then
    HOST_ARCH="arm64"
  else
    HOST_ARCH="x64"
  fi
fi

gitversion() {
  if [[ -z "${GitVersion}" ]]; then
    GitVersion="$(OPENTAP_COLOR=never OPENTAP_WARNINGS_TO_STDERR=true tap sdk gitversion)"
  fi
  echo "$GitVersion"
}

info() {
  printf "%s\n" "$@"
}

error() {
  printf "\x1b[1;31mERROR: %s\x1b[0m\n" "$@" >&2
  return 1
}

warn() {
  printf "\x1b[1;33mWARN: %s\x1b[0m\n" "$@" >&2
}
HERE="$(realpath "$(dirname "$0")")"
REPO_ROOT="$(dirname "$HERE")"

package_nuget() {
  info "Building nuget package"

  BUILD="${REPO_ROOT}/bin/Release"
  NUGET="${HERE}/nuget"
  cp -r "${REPO_ROOT}/nuget" "${HERE}/."
  mkdir -p "${NUGET}/build/docs/Packages/OpenTAP"
  mkdir -p "${NUGET}/build/runtimes"

  cp "${BUILD}/Keysight.OpenTap.Sdk.MSBuild.dll" "${NUGET}/build/"
  cp "${BUILD}/DotNet.Glob.NetStandard1.1.dll" "${NUGET}/build/DotNet.Glob.dll"
  sed -i "" 's/$(GitVersion)/'"$(gitversion)/" "${NUGET}/OpenTAP.nuspec"

  local nuget_targets=(
    # OpenTAP Package           Runtime destination
    Windows.x64                 win-x64
    Windows.x86                 win-x86
    MacOS.arm64                 macos-arm64
    MacOS.x64                   macos-x64
    Linux.x64                   linux-x64
    Linux.arm64                 linux-arm64
  )

  for ((i=0; i < ${#nuget_targets[@]}; i+=2 )); do
    local pkg="${nuget_targets[i]}"
    local runtime="${nuget_targets[i+1]}"
    unzip "${REPO_ROOT}/OpenTAP.$(gitversion).${pkg}.TapPackage" "${NUGET}/build/runtimes/${runtime}"
  done


  info "Finished building nuget package"
}

package_nuget

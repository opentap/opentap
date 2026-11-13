#!/usr/bin/env sh

# The runner's local Directory.Build.props isolates it from repository build settings.
BASEPATH="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec dotnet run "$BASEPATH/runner/build.cs" -- "$@"

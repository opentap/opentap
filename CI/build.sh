#!/usr/bin/env sh

# We force `dotnet run` to read the script from stdin in order to run it in isolated mode.
# When *not* running in isolated mode, `dotnet run` will implicitly scan parent directories
# for Directory.Build.props, and child directories for C# implementation files, which
# introduces unpredictable and undesirable behavior.

BASEPATH="$(dirname "$0")"
cat "${BASEPATH}/build.cs" | dotnet run - "$@"

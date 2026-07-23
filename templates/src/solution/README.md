# ProjectName

An [OpenTAP](https://opentap.io) plugin solution.

## Layout

| Path | Description |
| --- | --- |
| `ProjectName/` | The plugin project. Builds into a `.TapPackage`. |
| `ProjectName.Tests/` | NUnit tests for the plugin. |
| `Directory.Build.props` | Shared build settings, including the OpenTAP version. |
| `.gitversion` | Version number and versioning rules used by `$(GitVersion)`. |
| `.github/workflows/ci.yml` | Builds, tests, and publishes the package. |

## Getting started

This solution uses `$(GitVersion)` to derive the package version from git, so it
must be built inside a git repository. If you did not create it in one already:

```
git init
git add .
git commit -m "Initial commit"
```

The default branch must be named `main` (or match the `beta branch` set in
`.gitversion`), otherwise the version cannot be calculated.

## Build

```
dotnet build -c Release
```

The `.TapPackage` is created under `ProjectName/bin/Release`.

## Test

```
dotnet test
```

## Publish

The included GitHub Actions workflow publishes the package to an OpenTAP
repository when commits land on `main`, a `release` branch, or a `v*` tag.
Set the `PUBLIC_REPO_PASS` secret in your repository, and adjust
`PUBLISH_REPOSITORY` in `.github/workflows/ci.yml` to point at your repository.

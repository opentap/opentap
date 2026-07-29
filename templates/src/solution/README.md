# ProjectName

An [OpenTAP](https://opentap.io) plugin solution.

## Layout

| Path | Description |
| --- | --- |
| `ProjectName/` | The plugin project. Builds into a `.TapPackage`. |
| `ProjectName/package.xml` | Package metadata (name, description, files, supported OS). |
| `ProjectName.Tests/` | NUnit tests for the plugin. |
| `Directory.Build.props` | Shared build settings, including the OpenTAP version. |
| `.gitversion` | Version number and versioning rules used by `$(GitVersion)`. |
| `.github/workflows/ci.yml` | Builds, tests, and publishes the package. |

## Release checklist

Follow these steps to go from a freshly created solution to a published,
release-ready `.TapPackage`.

1. **Initialize a git repository.**
   The version is derived from git via `$(GitVersion)`, so the solution must
   live in a git repository with a branch named `main`:

   ```
   git init -b main
   git add .
   git commit -m "Initial commit"
   ```

   The branch name must match `beta branch` in `.gitversion` (default `main`),
   otherwise the version cannot be calculated.

2. **Implement the plugin.**
   Add your functionality to the plugin project under `ProjectName/`. The
   solution ships with example plugin components (for example a test step,
   DUT, instrument, result listener, component settings, or CLI action,
   depending on how it was generated). Rename or replace these with your own
   implementation and add NUnit tests for it in `ProjectName.Tests/`.

3. **Update `.gitversion`.**
   Set the starting `version` (default `0.1.0`). Prerelease numbers are counted
   from the last change to this value. Optionally enable the `release branch`
   and `release tag` rules to control when `rc` and release builds are produced.

4. **Fill in `ProjectName/package.xml`.**
   This is the metadata that end users see. Set at least:
   - `Name` — the package name shown in the repository.
   - `Description` — replace the placeholder text.
   - `InfoLink` — a URL to your project or documentation (currently empty).
   - `OS` — trim `Windows,Linux,MacOS` to the platforms you actually support.

5. **Build and test locally.**

   ```
   dotnet build -c Release
   dotnet test
   ```

   The `.TapPackage` is created under `ProjectName/bin/Release`.

6. **Set up the publish key.**
   Publishing to a repository requires an access token. Create a repository
   secret named `PUBLIC_REPO_PASS` containing your token. See
   [Package Publishing](https://doc.opentap.io/Developer%20Guide/Package%20Publishing/Readme.html)
   for how to obtain one.

7. **Push and tag a release.**
   Pushing to `main`, a `release` branch, or a `v*` tag triggers the workflow
   to build, test, and publish the package.

   ```
   git push -u origin main
   ```

## Build

```
dotnet build -c Release
```

## Test

```
dotnet test
```

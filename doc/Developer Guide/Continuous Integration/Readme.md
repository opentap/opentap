# Continuous Integration (CI/CD)
We recommend setting up a CI/CD workflow in order to reliably test, version, and publish your plugins.
This guide covers how to set up continuous integration using either GitLab or GitHub, but the information here should be applicable to other CI/CD systems.

This document describes using github CI for building plugins.

## GitHub
> This section is specific for **GitHub**, but parts may be relevant for other source control systems.

> This document describes the particulars of building and publishing OpenTAP packages with GitHub actions. Read more about Github Actions [here](https://docs.github.com/en/actions)

GitHub supports continuous integration using Github Actions. GitHub looks for workflow definitions in the `.github/workflows` directory.
A repository may have many workflows with different triggers. As an example, OpenTAP has a CI/CD workflow which is
triggered on pull requests, and when changes are pushed to the main branch or a release branch. It also has a 
book-keeping workflow which is triggered whenever an issue is closed. 

Each step in a workflow requires a runner. GitHub provides runners for all major platforms. A current list of available runner tags
can be found [here](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions#jobsjob_idruns-on).
These runners come equipped with most of the compilers and build tools you want, but if they don't suit your needs, GitHub also supports self-hosted runners. 

OpenTAP only uses default runners, and is tested using `windows-2022`, `ubuntu-20.04`, and `macos-11`.

> This example assumes that `REPO_USERTOKEN` is configured as a secret in the source repository. A secret can be configured by going to `Settings > Secrets > Actions > New repository secret`. For more information about user tokens and packaging, see [Package Publishing](../Package%20Publishing/Readme.md).

A workflow is defined by a yaml file, e.g. `.github/workflows/ci.yml`. 
Here is an example of how a GitHub Action can be configured to build, test, and publish an OpenTAP plugin:

```yaml
# Configure the name of this CI unit. This is the name that appears in the GitHub Actions tab
name: Name of this CI unit
# Configure what events trigger this action.
on: [push]

# Configure environment variables that are global to the action defined by this file
env:
  #OPENTAP_COLOR: auto # github messes with the "auto" color detection (i.e. it has no effect), and the "always" option breaks a lot of things
  OPENTAP_ANSI_COLORS: true
  OPENTAP_NO_UPDATE_CHECK: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_CONSOLE_ANSI_COLOR: true

jobs:

  ##############
  ### BUILD   ##
  ##############

  Build:
    runs-on: ubuntu-latest
    steps:
      # Check out the files in this repository. 
      - name: Checkout
        uses: actions/checkout@v4
        with:
          # 'tap sdk gitversion' can fail if the version history is incomplete. 
          # A fetch-depth of 0 ensures we get a complete history.
          fetch-depth: 0 
      # Fixes an issue with actions/checkout. This is required for automatic versioning to work using Git-assisted versioning. See https://github.com/actions/checkout/issues/290 and https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/Readme.html#git-assisted-versioning
      - name: Fix tags
        if: startsWith(github.ref, 'refs/tags/v')
        run: git fetch -f origin ${{ github.ref }}:${{ github.ref }} 
      # Build your project
      - name: Build
        run: dotnet build -c Release
      # Upload the package so it can be downloaded from GitHub, 
      # and consumed by other steps in this workflow
      - name: Upload binaries
        uses: actions/upload-artifact@v4
        with:
          name: tap-package
          retention-days: 5
          # Path to the package from the build step. If your package builds to a subfolder make sure to update this path.
          path: bin/Release/*.TapPackage

  ##############
  ### TEST    ##
  ##############

  UnitTests:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      - name: Build
        run: dotnet test

  ##############
  ### PUBLISH ##
  ##############

  Publish:
      # Only publish on the main branch, the release branch, or if the commit is tagged.
      if: github.ref == 'refs/heads/main' || contains(github.ref, 'refs/heads/release') || contains(github.ref, 'refs/tags/v')
      runs-on: ubuntu-latest
      # This step depends on the build step
      needs:
        - Build
      steps:
        # Download the tap-package artifact from the Build step
        - name: Download TapPackage Arfifact
          uses: actions/download-artifact@v4
          with:
            name: tap-package
            path: .
        # Setup OpenTAP with the PackagePublish package in order to publish the newly created package
        - name: Setup OpenTAP
          uses: opentap/setup-opentap@v1.0
          with:
            version: 9.18.4
            packages: "Repository Client"
        # Publish the package. This requires the package management key to be configured in the 'PUBLIC_REPO_PASS' environment variable.
        - name: Publish
          run: tap repo upload --token ${{ secrets.REPO_USERTOKEN }} *.TapPackage
```



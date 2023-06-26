# Continuous Integration (CI/CD)
Whether you use GitHub or GitLab, we recommend setting up a CI/CD workflow in order to reliably test, version, and publish your plugins.
This guide covers how to set up continuous integration using either GitLab or GitHub, but the information here should be applicable to other CI/CD systems.

## GitLab
> This section is specific for **GitLab**, but parts may be relevant for other source control systems.

>This document describes the configuration of the GitLab build script when using continuous integration in an OpenTAP plugin project. We do not go into detail about GitLab build scripts. Read more about GitLab CI/CD [here](https://docs.gitlab.com/ee/ci/yaml/README.html). 

Setting up Continuous Integration using GitLab build runners and OpenTAP is easy. GitLab CI/CD looks for a file called .gitlab-ci.yml in the root of the project repository (see [here](#example) for an example). This file defines what happens during CI/CD (see [here](https://docs.gitlab.com/ee/ci/yaml/README.html) for more details).

We recommend using build runners that use the [Docker executor](https://docs.gitlab.com/runner/executors/docker.html), as this is the most stable and mature process. The Docker executor creates a new and clean environment every time a build job is started.

### Linux
Using linux runners with Docker executor is the most mature and stable way to run your builds. We recommend always using Linux runners unless you need Windows.

Linux runners using Docker executor are provided for free by GitLab if your project is Open-Source. If you have a private project you are limited to 2000 CI minutes, read more about GitLab pricing [here](https://about.gitlab.com/pricing/#gitlab-com).

#### Tags
The [`tags:` parameter](https://docs.gitlab.com/ee/ci/yaml/README.html#tags) is used to select which build runner should execute the build job. To use the shared Linux runners provided by GitLab, use these specific tags:

```yaml
tags: [ docker, gce ]
```

#### Image
The [`image:` parameter](https://docs.gitlab.com/ee/ci/yaml/README.html#image) is used to specify the Docker image used for the job. When using the Linux runners you can use any existing Linux Docker image. Here are some suggestions:

| Description | Docker Image |
|-|-|
| Image with OpenTAP 9.17 installed, this image also has the .NET SDK installed. We recommended this image when building an OpenTAP .TapPackage. See more [here](https://github.com/opentap/opentap/blob/main/docker/Linux/Dockerfile).  | opentapio/opentap:9.17-bionic |
| Image with Node.JS 9.11 installed. | node:9.11.1 |
| Image with .NET 6.0 SDK installed. | mcr.microsoft.com/dotnet/sdk:6.0-focal |
| Image with Kaniko, use this image to create your own Docker image when Docker in Docker is unavailable. Read more about the project [here](https://github.com/GoogleContainerTools/kaniko). | gcr.io/kaniko-project/executor:debug |

#### Example
```yaml
Build:
  stage: build
  image: opentapio/opentap:9.17-bionic
  tags: [ docker, gce ]
  script:
        - dotnet build -c Release
        - cp Demo/bin/Release/Demo*.TapPackage .
  artifacts:
    expire_in: 1 week
    paths:
       - "Demo*.TapPackage"
```


### Windows
Sometimes it can be necessary to use Windows build runners. If your project is part of the OpenTAP Plugin group (on GitLab), we provide free Windows build runners that use the docker executor.

> GitLab provides free Windows build runners, but these do not use the Docker executor, but instead a custom executor, read more [here](https://about.gitlab.com/blog/2020/01/21/windows-shared-runner-beta/). These runners do not support Docker images.


#### Tags
To use the OpenTAP Windows runners, use these specific tags:

```yaml
tags: [ docker, windows ]
```

If you want to use the shared Windows runners provided by GitLab, use these specific tags:

```yaml
tags: [ shared-windows, windows, windows-1809 ]
```

#### Image
When using the Windows runners you can use any existing Windows Docker image compatible with `Windows Server 2019`. Here are some suggestions:

| Description | Docker Image |
|-|-|
| Image with OpenTAP 9.16 installed, this image also has the .NET SDK installed. We recommended this image when building an OpenTAP .TapPackage. See more [here](https://github.com/opentap/opentap/blob/v9.16.4/docker/Windows/Dockerfile).  | opentapio/opentap:9.16-windowsserver1809 |
| Image with .NET 4.7.2 SDK installed. | mcr.microsoft.com/dotnet/framework/sdk:4.7.2 |

> Please note that Windows Docker images must be compatible with the host OS. This means that all Docker images using OpenTAP build runners must be compatible with `Windows Server 2019`.

#### Example
```yaml
Build:
  stage: build
  image: opentapio/opentap:9.16-windowsserver1809
  tags: [ docker, windows ]
  script:
        - dotnet build -c Release
        - Move-Item Demo/bin/Release/Demo*.TapPackage .
  artifacts:
    expire_in: 1 week
    paths:
       - "Demo*.TapPackage"
```

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

> This example assumes that `PUBLIC_REPO_PASS` is configured as a secret in the repository. A secret can be configured by going to `Settings > Secrets > Actions > New repository secret`

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
        uses: actions/checkout@v3
        with:
          # 'tap sdk gitversion' can fail if the version history is incomplete. 
          # A fetch-depth of 0 ensures we get a complete history.
          fetch-depth: 0 
      # Fixes an issue with actions/checkout@v3. See https://github.com/actions/checkout/issues/290
      - name: Fix tags
        if: startsWith(github.ref, 'refs/tags/v')
        run: git fetch -f origin ${{ github.ref }}:${{ github.ref }} 
      # Build your project
      - name: Build
        run: dotnet build -c Release
      # Create the tap package
      - name: Package
        working-directory: bin/Release
        run: ./tap package create package.xml
      # Upload the package so it can be downloaded from GitHub, 
      # and consumed by other steps in this workflow
      - name: Upload binaries
        uses: actions/upload-artifact@v3
        with:
          name: tap-package
          retention-days: 5
          path: |
            bin/Release/*.TapPackage

  ##############
  ### TEST    ##
  ##############

  UnitTests:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v3
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
          uses: actions/download-artifact@v3
          with:
            name: tap-package
            path: .
        # Setup OpenTAP with the PackagePublish package in order to publish the newly created package
        - name: Setup OpenTAP
          uses: opentap/setup-opentap@v1.0
          with:
            version: 9.17.4
            packages: "PackagePublish:rc"
        # Publish the package. This requires the package management key to be configured in the 'PUBLIC_REPO_PASS' environment variable.
        - name: Publish
          run: tap package publish -r http://packages.opentap.io -k ${{ secrets.PUBLIC_REPO_PASS }} *.TapPackage
```



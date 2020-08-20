# Continuous Integration (CI/CD)
> This guide is specific for **GitLab**, but parts may be relevant for other source control systems.

>This document describe the configuration of the GitLab build script when using continuous integration in an OpenTAP plugin project. We do not go into general details on GitLab build script, for more info about GitLab CI/CD [here](https://docs.gitlab.com/ee/ci/yaml/README.html). 

Setting up Continuous Integration using GitLab build runners and OpenTAP is easy. GitLab CI/CD looks for a file called .gitlab-ci.yml in the root of the project repository (see [here](#example) for an example). This file defines what happens during CI/CD (see [here](https://docs.gitlab.com/ee/ci/yaml/README.html) for more details).

We recommend using build runners that use the [Docker executor](https://docs.gitlab.com/runner/executors/docker.html), as this is the most stable and mature process. The Docker executor creates a new and clean environment every time a build job is started.

## Linux
Using linux runners with Docker executor is the most mature and stable way to run your builds. We recommend always using Linux runners unless you need Windows.

Linux runners using Docker executor are provided for free by GitLab if your project is Open-Source. If you have a private project you are limited to 2000 CI minutes, read more about GitLab pricing [here](https://about.gitlab.com/pricing/#gitlab-com).

### Tags
The [`tags:` parameter](https://docs.gitlab.com/ee/ci/yaml/README.html#tags) is used to select which build runner should execute the build job. To use the shared Linux runners provided by GitLab, use these specific tags:

```yaml
tags: [ docker, gce ]
```

### Image
The [`image:` parameter](https://docs.gitlab.com/ee/ci/yaml/README.html#image) is used to specify the Docker image used for the job. When using the Linux runners you can use any existing Linux Docker image. Here are some suggestions:

| Description | Docker Image |
|-|-|
| Image with OpenTAP 9.8 installed, this image also has the .NET Core 2.1 SDK installed. We recommended this image when building an OpenTAP .TapPackage. See more [here](https://gitlab.com/OpenTAP/opentap/-/blob/master/docker/Linux/Dockerfile).  | opentapio/opentap:9.8-ubuntu18.04 |
| Image with Node.JS 9.11 installed. | node:9.11.1 |
| Image with .NET Core 2.1 SDK installed. | microsoft/dotnet:2.1-sdk-stretch |
| Image with Kaniko, use this image to create your own Docker image when Docker in Docker is unavailable. Read more about the project [here](https://github.com/GoogleContainerTools/kaniko). | gcr.io/kaniko-project/executor:debug |

### Example
```yaml
Build:
  stage: build
  image: opentapio/opentap:9.8-ubuntu18.04
  tags: [ docker, gce ]
  script:
        - dotnet build -c Release
        - cp Demo/bin/Release/Demo*.TapPackage .
  artifacts:
    expire_in: 1 week
    paths:
       - "Demo*.TapPackage"
```


## Windows
Sometimes it can be necessary to use Windows build runners. If your project is part of the OpenTAP Plugin group (on GitLab), we provide free Windows build runners that use the docker executor.

> GitLab provides free Windows build runners, but these do not use the Docker executor, but instead a custom executor, read more [here](https://about.gitlab.com/blog/2020/01/21/windows-shared-runner-beta/). These runners do not support Docker images.


### Tags
To use the OpenTAP Windows runners, use these specific tags:

```yaml
tags: [ docker, gce ]
```

If you want to use the shared Windows runners provided by GitLab, use these specific tags:

```yaml
tags: [ shared-windows, windows, windows-1809 ]
```

### Image
When using the Windows runners you can use any existing Windows Docker image compatible with `Windows Server 2019`. Here are some suggestions:

| Description | Docker Image |
|-|-|
| Image with OpenTAP 9.8 installed, this image also has the .NET 4.7.2 SDK installed. We recommended this image when building an OpenTAP .TapPackage. See more [here](https://gitlab.com/OpenTAP/opentap/-/blob/master/docker/Windows/Dockerfile).  | opentapio/opentap:9.8-windowsserver1809 |
| Image with .NET 4.7.2 SDK installed. | mcr.microsoft.com/dotnet/framework/sdk:4.7.2 |

> Please note that Windows Docker images must be compatible with the host OS. This means that all Docker images using OpenTAP build runners must be compatible with `Windows Server 2019`.

### Example
```yaml
Build:
  stage: build
  image: opentapio/opentap:9.8-windowsserver1809
  tags: [ docker, windows ]
  script:
        - dotnet build -c Release
        - Move-Item Demo/bin/Release/Demo*.TapPackage .
  artifacts:
    expire_in: 1 week
    paths:
       - "Demo*.TapPackage"
```

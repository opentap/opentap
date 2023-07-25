# Getting Started
This is the official OpenTAP documentation for users and developers.


## What is OpenTAP

OpenTAP is an Open Source project for fast and easy development and execution of automated tests. 

OpenTAP is built with simplicity, scalability and speed in mind, and is based on an extendable architecture that leverages .NET. 
OpenTAP offers a range of sequencing functionality and infrastructure that makes it possible for you to quickly develop plugins tailored for your automation needs – plugins that can be shared with the OpenTAP community through the OpenTAP package repository. 

Learn more about OpenTAP [here](http://opentap.io).

We recommend that you [download the Developer's System provided by Keysight Technologies](https://www.keysight.com/find/tapinstall) available with a commercial or community license. The Developer's System is a bundle that contains the SDK as well as a graphical user interface and result-viewing capabilities. 

## Install OpenTAP
### Windows
1. Download OpenTAP from our homepage [here](https://opentap.io/downloads). 
2. Start the installer.

### Linux
<!--When installing on Linux there are a few options:-->
#### 1. Install dependencies
On Linux, OpenTAP has a few dependencies that must be manually installed, namely
libc6, libunwind, unzip, git, and curl. On Debian derivatives, these can be installed
by running the following command:

`apt-get install libc6-dev libunwind8 unzip git curl`

Note that the packages may have different names on other distributions. OpenTAP
should still work if you install the equivalent packages for your distribution.

In addition to these packages, OpenTAP depends on dotnet runtime version 6.0. The installation procedure depends on your distribution. Please see [the official documentation from
Microsoft ](https://docs.microsoft.com/en-us/dotnet/core/install/runtime) for further instructions.

> Note: Dotnet installed using [Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap) is NOT supported.
The Snap permissions for dotnet does not permit it to read *hidden* files (files or directories starting with a '.') which breaks core functionality of OpenTAP.

#### 2. Install OpenTAP
Download the OpenTAP distribution (`.tar`<!--, `.dep` or `.rpm`-->) from our homepage
[here](https://opentap.io/downloads). 

Install the downloaded distribution:

<!--- `.dep` run `sudo apt install ./OpenTAP*.deb`
- `.rpm` run `sudo dnf install ./OpenTAP*.rpm`-->
- `.tar` do the following:
	1. Untar the package in you home directory `tar -xf OpenTAP*.tar`
	2. Change the permission of the INSTALL.sh file to be executable: `chmod u+x INSTALL.sh`
	3. Run the INSTALL.sh script: `./INSTALL.sh`.

### Docker
We also provide docker images for running OpenTAP. You can find them at
[hub.docker.com/r/opentapio/opentap](https://hub.docker.com/r/opentapio/opentap).

We maintain two images:


1. a development image which includes all necessary tools to build OpenTAP projects (~2.5GB)
2. a production image which includes only dependencies required to run OpenTAP (~330MB)

The development image is widely used for building and packaging plugins in highly reproducible environments, and we use
it internally for continuous deployment. Have a look at the [Demonstration
plugin's gitlab CI file](https://gitlab.com/OpenTAP/Plugins/demonstration/-/blob/master/.gitlab-ci.yml) where we build, test, version, and publish the plugin directly in a continuous integration pipeline.


### Where to go next
Are you already an OpenTAP user, or want to try it out? Have a look at the [User Guide](User%20Guide/Introduction/).

Are you a developer and want to create plugins for OpenTAP? Have a look at the [Developer Guide](Developer%20Guide/Introduction/).

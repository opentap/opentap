# Getting Started
This is the official OpenTAP documentation for users and developers.


## What is OpenTAP

OpenTAP is an Open Source project for fast and easy development and execution of automated tests. 

OpenTAP is built with simplicity, scalability and speed in mind, and is based on an extendable architecture that leverages .NET. 
OpenTAP offers a range of sequencing functionality and infrastructure that makes it possible for you to quickly develop plugins tailored for your automation needs – plugins that can be shared with the OpenTAP community through the OpenTAP package repository. 

Learn more about OpenTAP [here](http://opentap.io).

We recommend that you [download the Developer's System](https://www.keysight.com/find/tapinstall) provided by Keysight Technologies available with a commercial or community license. The Developer's System is a bundle that contains the SDK as well as a graphical user interface and result-viewing capabilities. 

## Install OpenTAP
### Windows
1. Download OpenTAP from our homepage [here](https://opentap.io/downloads). 
2. Start the installer.

### Linux
> Note: Dotnet installed using [Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap) is NOT supported.
The Snap permissions for dotnet does not permit it to read *hidden* files (files or directories starting with a '.') which breaks core functionality of OpenTAP.

For Ubuntu 20.04 and up, we provide an installer similar to the windows installer.

1. Download OpenTAP from our homepage [here](https://opentap.io/downloads). 
2. Make the installer executable: `chmod +x path-to-installer`
3. Start the installer.

The installer supports the `--quiet` flag which can be used to install OpenTAP in scripts, or in a terminal environemnt:

```bash
# Download the latest OpenTAP release
curl -Lo opentap.linux https://packages.opentap.io/4.0/Objects/www/OpenTAP?os=Linux
# Make it executable
chmod +x ./opentap.linux
# Run the installer
sudo ./opentap.linux --quiet
```

The installer is likely to work on other Linux distributions, but additional dependencies
may be required on these platforms, e.g. dotnet 6 runtime.

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

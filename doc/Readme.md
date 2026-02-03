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
1. Download the [installer](https://packages.opentap.io/4.0/Objects/www/OpenTAP.exe?format=full).
2. Start the installer.

### Linux
> Note: Dotnet installed using [Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap) is NOT supported.
The Snap permissions for dotnet does not permit it to read *hidden* files (files or directories starting with a '.') which breaks core functionality of OpenTAP.

#### Ubuntu 24.04
> Although this may work on other Linux distributions, we recommend using Ubuntu 24.04.

On Ubuntu we provide a graphical installer. 

1. Download the [installer](https://packages.opentap.io/4.0/Objects/www/OpenTAP?os=Linux&format=full).
2. Make the installer executable: `chmod +x path-to-installer`
3. Start the installer.

The installer supports the `--quiet` flag which can be used to install OpenTAP in scripts, or in a terminal environemnt:

```bash
# Download the latest OpenTAP release
curl -Lo opentap.linux https://packages.opentap.io/4.0/Objects/www/OpenTAP?os=Linux
# Make it executable
chmod +x ./opentap.linux
# Run the installer
./opentap.linux --quiet
```

The installer is likely to work on other Linux distributions, but additional dependencies
may be required on these platforms, such as dotnet 9 runtime.

#### Other Linux distributions
For other Linux distributions, OpenTAP can be installed using the terminal:

1. Install dotnet: [Instructions](https://learn.microsoft.com/da-dk/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website).
    - For Ubuntu: `sudo apt install -y dotnet-sdk-9.0`.
2. Download the latest OpenTAP release: `curl -Lo opentap.zip 'https://packages.opentap.io/4.0/Objects/Packages/OpenTAP?os=Linux&architecture=<architecture>'`.
    - Remember to change the architecture if needed (`x64`, `arm64`, `arm`).
3. Create a directory for OpenTAP: `mkdir -p /home/$USER/.local/share/opentap`.
4. Extract the OpenTAP release: `unzip ./opentap.zip -d /home/$USER/.local/share/opentap`.
    - If you don't have unzip installed, you can install it with `sudo apt install unzip`.
5. Make tap executable: `chmod +x /home/$USER/.local/share/opentap/tap`.
6. Add installation dir to PATH by adding the following line to your terminal read config file:
    - For bash: `echo 'export PATH=/home/$USER/.local/share/opentap/:$PATH' >> ~/.bashrc`.
    - For zsh: `echo 'export PATH=/home/$USER/.local/share/opentap/:$PATH' >> ~/.zshrc`.
7. Reload the terminal:
    - For bash: `source ~/.bashrc` or  `exec bash`.
    - For zsh: `source ~/.zshrc` or `exec zsh`.

##### An example using Ubuntu 24.04
This example shows how to install OpenTAP on ARM64 Ubuntu 24.04 running on a Raspberry PI 5. The steps are similar for other distributions.

```bash
# Install dotnet and unzip
sudo apt install -y dotnet-sdk-9.0 unzip

# Download the latest OpenTAP release
curl -Lo opentap.zip 'https://packages.opentap.io/4.0/Objects/Packages/OpenTAP?os=Linux&architecture=arm64'

# Create a directory for OpenTAP
mkdir -p /home/$USER/.local/share/opentap

# Extract the OpenTAP release
unzip ./opentap.zip -d /home/$USER/.local/share/opentap

# Make tap executable
chmod +x /home/$USER/.local/share/opentap/tap

# Add installation dir to PATH
echo 'export PATH=/home/$USER/.local/share/opentap/:$PATH' >> ~/.bashrc

# Reload your bash config
source ~/.bashrc
```

### MacOS
There is no installer available on Mac. Instead, OpenTAP must be installed using the terminal:

```bash
# Download the latest OpenTAP release
curl -Lo opentap.zip 'https://packages.opentap.io/4.0/Objects/Packages/OpenTAP?os=MacOS&architecture=arm64'
# Extract it wherever you would like the installation to be
unzip ./opentap.zip -d opentap
# Make tap executable
chmod +x ./opentap/tap
```

OpenTAP requires dotnet 9 runtime. If you do not already have dotnet installed, get it from [Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/macos).

Verify the installation works by trying for example `./opentap/tap package list --installed`.

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
Are you already an OpenTAP user, or want to try it out? Have a look at the [User Guide](User%20Guide/Introduction/Readme.md).

Are you a developer and want to create plugins for OpenTAP? Have a look at the [Developer Guide](Developer%20Guide/Introduction/Readme.md).

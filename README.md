# OpenTAP

OpenTAP is an Open Source project for fast and easy development and execution of automated tests. 
OpenTAP is built with simplicity, scalability and speed in mind, and is based on an extendable architecture that leverages .NET Core. 
OpenTAP offers a range of sequencing functionality and infrastructure that makes it possible for you to quickly develop plugins tailored for your automation needs – plugins that can be shared with the OpenTAP community through the OpenTAP package repository. 

## Getting OpenTAP

If you are looking to use OpenTAP, you can get pre-built binaries at [http://opentap.io](https://www.opentap.io/download.html). 

Using the OpenTAP CLI you are now able to download plugin packages from the OpenTAP package repository.

To list and install plugin packages do the following in the command prompt: 
```
cd %TAP_PATH%
tap package list
```

We recommend that you download the Software Development Kit, or simply the Developer’s System Community Edition provided by Keysight Technologies. The Developer System is a bundle that contains the SDK as well as a graphical user interface and result viewing capabilities. It can be installed by typing the following:
```
tap package install "Developer's System CE" -y
```

For a guide on how to develop using OpenTAP, check out our __[Developer Guide](https://doc.opentap.io/Developer%20Guide/Introduction/#introduction)__, it also exists as a [PDF](http://opentap.io/docs/OpenTAP%20Developer%20Guide.pdf) for offline reading. Note the [source](https://gitlab.com/OpenTAP/opentap/blob/master/doc/Developer%20Guide/Readme.md) can be found at GitLab as well.

## Building OpenTAP
Most users build plugins for OpenTAP but if you are interested in building OpenTAP yourself you can clone the git repository at https://gitlab.com/OpenTAP/opentap and build the OpenTAP.sln solution file.

### Microsoft Windows 10
 On Windows, Visual Studio 2017 or greater is needed to build. This can be done by opening the solution and pressing F5, or Ctrl-Shift-B.

**Note:** You need [Microsoft .NET Framework 4.6.2 Targeting Pack](https://www.microsoft.com/en-us/download/details.aspx?id=53321) to build OpenTAP for .NET Framework.

### Linux / .NET Core
On Linux, you can build the source with .NET Core. You usually need to install some dependencies in addition to .NET Core SDK. On Ubuntu, run the following on apt:

```sh
sudo apt install libc6-dev libunwind8 curl git libcurl3 -y
```
This should install the needed deb dependencies. Once these are installed to following can be done:

```sh
dotnet build -c NetCore -r linux-x64
dotnet publish -c NetCore -r linux-x64
dotnet publish -c NetCore -r linux-x64 tap/tap.csproj
```
*Note, the last line is there to ensure getting the right System.Runtime.dll.*

This creates a *Release* build. For a debug build set *NetCoreDebug* instead of *NetCore* when building.


## Testing
OpenTAP can be tested using NUnit. 

### Windows

Using Visual Studio 2017, open OpenTAP.sln and run the tests in the TestExplorer.

### Linux

To run the entire test suite on Linux, run:

```sh
dotnet test -c NetCore # Or NetCoreDebug
```

To debug the unittests, set the VSTEST_HOST_DEBUG environment variable to '1'. This causes dotnet test to wait for a debugger to become attached. 
```sh
export VSTEST_HOST_DEBUG=1
```

## Documentation
More documentation and help developing plugins for OpenTAP can be found here: [doc.opentap.io](https://doc.opentap.io).

## Contributing

If you are thinking of contributing code to OpenTAP itself, first of all, thank you! All fixes, patches and enhancements to OpenTAP are very warmly welcomed. In order to keep thing manageable, there are a number of guidelines that should be followed in order to ensure that your modification is included in OpenTAP as quickly as possible. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## Reporting Issues

We track issues at https://gitlab.com/OpenTAP/opentap/issues. You are welcome to file an issue there if you have found a bug or have a concrete request for a new feature. Please include a session log file if possible or relevant. Any other files needed to reproduce an issue are also appreciated.

## License

This source code is subject to the terms of the Mozilla Public License, v. 2.0. See full license in [LICENSE.txt](LICENSE.txt)

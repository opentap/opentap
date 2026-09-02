# OpenTAP

OpenTAP is an Open Source project for fast and easy development and execution of automated tests.

OpenTAP is built with simplicity, scalability and speed in mind, and is based on an extendable architecture that leverages .NET.

OpenTAP offers a range of sequencing functionality and infrastructure that makes it possible for you to quickly develop plugins tailored for your automation needs – plugins that can be shared with the OpenTAP community through the OpenTAP package repository.

## Getting OpenTAP

If you are looking to use OpenTAP, you can get pre-built binaries at [https://opentap.io/downloads](https://www.opentap.io/downloads).

Using the OpenTAP CLI you are now able to download plugin packages from the OpenTAP package repository.

To list and install plugin packages do the following in the command prompt after navigating to
the installation folder: (default: C:\Program Files\OpenTAP)

```cmd
cd "C:\Program Files\OpenTAP" (or the directory you selected in the installer)
tap package list
```

We recommend installing Developer’s System provided by Keysight Technologies. Developer's System is a bundle that contains the SDK and a graphical user interface with result viewing capabilities. It can be installed by running the following command:

```cmd
tap package install "Developer's System"
```

For a guide on how to develop using OpenTAP, check out our [Developer Guide](https://doc.opentap.io/Developer%20Guide/Introduction/#introduction)__. Note the [source](https://github.com/opentap/opentap/tree/main/doc/Developer%20Guide) can be found on GitHub as well.

## Building OpenTAP
To build OpenTAP, install [Microsoft .NET SDK 9.0](https://dotnet.microsoft.com/download) and run `dotnet build`:

```sh
dotnet build -c Release
```

This creates a *Release* build. For a debug build, omit *-c Release* when building.

## Testing
OpenTAP can be tested using NUnit:

```sh
dotnet test
```

## Documentation
More documentation and help developing plugins for OpenTAP can be found here:
[doc.opentap.io](https://doc.opentap.io).

## Contributing

If you are thinking of contributing code to OpenTAP, first of all, thank you!

All fixes, patches and enhancements to OpenTAP are very warmly welcomed. In order to keep thing manageable, there are a number of guidelines that should be followed in order to ensure that your modification is included in OpenTAP as quickly as possible. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## Reporting Issues

We track issues at https://github.com/opentap/opentap/issues. You are welcome to file an issue there if you have found a bug or have a concrete request for a new feature. Please include a session log file if possible or relevant. Any other files needed to reproduce an issue are also appreciated.

## License

This source code is subject to the terms of the Mozilla Public License, v2.0. See full license in [LICENSE.txt](LICENSE.txt).

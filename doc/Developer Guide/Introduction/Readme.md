Introduction
============
This document describes the programmatic interface to OpenTAP and shows how to get started using OpenTAP for implementing test steps, instrument plugins, DUT plugins and result listeners.

## Audience
This document is written for somebody who wants to develop OpenTAP plugins or integrate OpenTAP into their own applications using the C# programming language. It is not a reference manual, but rather a document that describes the principles behind OpenTAP and how to use its most important features from a programmer's perspective. If you are looking for Python developer documentation, go [here](https://doc.opentap.io/OpenTap.Python/).

For development, we recommend using the following software:

- Visual Studio, JetBrains Rider or Visual Studio Code.
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (Included in the installer.)
- Windows 11, Linux (Ubuntu >22.04) or MacOS (>26.6)

> **Note:** If you are upgrading from an earlier version (e.g. 9.28), .NET 9 is **not** automatically installed during the upgrade. You must install .NET 9 before running `tap`. See [Migrating to .NET 9](../Migrating%20to%20.NET%209/Readme.md) for details.

## Suggested Resources
VISA driver e.g. Keysight I/O libraries for instrument communication

### PathWave Test Automation
Together with OpenTAP it is recommended to use a Graphical User Interface. Keysight Technologies offers both enterprise and community licensed versions of [PathWave Test Automation Developer's System](https://www.keysight.com/find/tapinstall) that provides a highly flexible graphical user interface and code examples.

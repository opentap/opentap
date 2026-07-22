# ProjectName

This is an [OpenTAP](https://opentap.io) plugin package. It was generated from the OpenTAP project template and provides a starting point for developing your own OpenTAP plugins.

## Getting Started

Depending on the options selected when the project was generated, it may contain example implementations of one or more common OpenTAP plugin types. Each example is a starting point: rename it, remove the ones you do not need, and fill in the `ToDo` sections to implement your own functionality.

The plugin types you may encounter include:

- **`TestStep`** – A step that can be added to a test plan and executed.
- **`Instrument`** – A driver representing a piece of test equipment.
- **`Dut`** – A definition of a Device Under Test.
- **`ResultListener`** – A component that receives and stores results produced during a test plan run.
- **`ComponentSettings`** – Global, persisted settings for the plugin.
- **`ICliAction`** – A command that can be invoked from the `tap` command line.

You can add or remove any of these plugin types at any time; none of them are required.

## Building

Build the project from the command line:

```
dotnet build -c Release
```

When built in the `Release` configuration, an OpenTAP package (`.TapPackage`) is created based on `package.xml`. In `Debug` configuration the plugin is built without packaging, so you can debug it directly with the editor specified by the `DebugWith` property in `ProjectName.csproj`.

## Packaging

The package metadata is defined in `package.xml`. Update the following before publishing:

- **Name** – The name of the package.
- **Version** – A [SemVer 2.0](https://semver.org) compatible version. Use the `$(GitVersion)` macro to derive it from Git.
- **Description** – A short summary of what the package does.
- **InfoLink** – A URL where users can find more information.
- **Files** – The files to include in the package.

## Learn More

- [OpenTAP Documentation](https://doc.opentap.io)
- [Developer Guide](https://doc.opentap.io/Developer%20Guide/Introduction/)
- [OpenTAP Website](https://opentap.io)

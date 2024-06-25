# Getting Started

An OpenTAP **plugin** refers to a type which satisfies an interface recognized by OpenTAP. The majority of plugins are C# classes. In this case, more concrelety, you can think of a plugin as a C# class which inherits from a base class from OpenTAP, or implements an OpenTAP interface. A plugin is distributed as a compiled `.dll` file in a **TapPackage**. A TapPackage is a collection of one or more plugin DLLs, their dependencies, and additional metadata, such as a description and versioning information. 
TapPackages can be shared on the [Package Repository](https://packages.opentap.io). For in-depth information on packaging, see [Plugin Packaging and Versioning](../Plugin%20Packaging%20and%20Versioning/Readme.md). A TapPackage is usually based on a C# solution. This document is details how to create a new solution which automates the process of creating and versioning an OpenTAP plugin package.

Besides C#, it is also possible to develop plugins in Python by using the Python plugin. Python development will not be covered here. See the [Python Documentation](https://doc.opentap.io/OpenTap.Python/) for more information.

To get started, we recommend installing the [OpenTAP NuGet Templates](https://www.nuget.org/packages/OpenTap.Templates/). 
They can be installed by using the dotnet CLI: `dotnet new install OpenTap.Templates::9.23.2`, or if your IDE supports it, you can search for online templates and look for `OpenTap.Templates`.

## Using the NuGet Package

With the templates installed, you can create a new OpenTAP project through the `New Solution` option in your IDE, or you can use the dotnet CLI: 
```bash
dotnet new sln --name MySolution
dotnet new opentap --name MyFirstPlugin
dotnet sln add MyFirstPlugin
```

To convert an existing project to an OpenTAP plugin, add a reference to the [OpenTAP NuGet package](https://www.nuget.org/packages/OpenTAP/). You can do this by using the dotnet CLI: `dotnet add package OpenTAP --version 9.23.2`, or by searching for "OpenTAP" in the NuGet package manager in your IDE.
> NOTE: On Windows, only .NET Framework and netstandard2.0 is supported. If you are using .NET 6 or later, OpenTAP may not be able to correctly load your plugin.

## NuGet Features

The OpenTAP NuGet package provides the following build-time features:

### Installation

When your project references the NuGet package, OpenTAP will automatically be installed in your project's output directory (e.g. bin/Debug/). The installation will have the same version as your NuGet reference. By using a unique installation for each plugin, it is easy to manage different plugins which may depend on different versions of OpenTAP. The version of OpenTAP is recorded in the \*.csproj file, which should be managed by version control (e.g. git), so all developers use the same version. This also automates the process of installing OpenTAP on a new machine, such as a Continuous Integration environment.

### Package Creation

The NuGet package provides the option to automate packaging your plugins as a \*.TapPackage as part of the build process. To take advantage of this feature, your project needs a [package definition file](../Plugin%20Packaging%20and%20Versioning/Readme.md). If you created you project with the NuGet Template, a package definition file called `package.xml` was already created for you. 
If you did not use the NuGet template, you can use the OpenTAP CLI Action `tap sdk new packagexml` to generate a skeleton file. If your project contains a file named package.xml, a TapPackage will be generated when it is built in Release mode. You can customize this behavior using these MSBuild properties in your csproj file:

```xml
<OpenTapPackageDefinitionPath>package.xml</OpenTapPackageDefinitionPath>
<CreateOpenTapPackage>true</CreateOpenTapPackage>
<InstallCreatedOpenTapPackage>true</InstallCreatedOpenTapPackage>
```

### Reference Other OpenTAP Packages

When using the OpenTAP NuGet package, you can reference other TapPackages similar to how NuGet packages are referenced. Referenced packages are installed into your project's output directory (usually ./bin/Debug/) along with OpenTAP.

Reference a package by specifying it in your .csproj file:
```xml
<ItemGroup>
  <!-- NuGet reference to OpenTAP -->
  <PackageReference Include="OpenTAP" Version="9.23.2" />
  <!-- OpenTAP package sources -->
  <OpenTapPackageRepository Include="packages.opentap.io"/>
  <OpenTapPackageRepository Include="$HOME/Downloads;$HOME/Documents"/>

  <!-- Packages to reference  -->
  <OpenTapPackageReference Include="DMM API" Version="2.1.2" UnpackOnly="false" IncludeAssemblies="pattern1;pattern2" ExcludeAssemblies="pattern3;pattern4" />
</ItemGroup>
```
> Notice the similarity between `<PackageReference .../>` (a NuGet reference), and `<OpenTapPackageReference ../>`.

The `<OpenTapPackageRepository/>` element
is similar to the concept of [NuGet Sources](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-sources). It specifies from where
OpenTAP packages should be resolved. The element can be specified multiple times, or the repository sources can be separated by a semicolon (`;`). All instances of this element will be joined during compilation. Http repositories and directory names are both valid sources. `packages.opentap.io` is always included by default, and cannot be excluded. It is included here only as an example.

When referencing a package in this way, assemblies belonging to that package are automatically referenced in your project.

All attributes except `Include` are optional. `Version` defaults to the latest release if omitted. `UnpackOnly` defaults to false, and can be used to suppress any install actions from running as part of the package being installed in the output dir. 


The `IncludeAssemblies` and `ExcludeAssemblies` attributes control which assemblies are referenced. The supplied value is interpreted as one or more glob patterns, separated by semicolons (`;`). If not specified, they use the default values `IncludeAssemblies="**"` and `ExcludeAssemblies="Dependencies/**"`, meaning that all DLLs from the package are referenced if they are not in a subdirectory of the `Dependencies` folder. By leveraging glob patterns, your project can target specific dependencies of other packages. 


Glob patterns are tested in order of specificity, meaning that the evaluation order of patterns in IncludeAssemblies and ExcludeAssemblies can be interleaved. Specificity is measured by the number of tokens used in the expression.
By tokens, we mean the *components* of the expression, and not the characters. The components of `"Dependencies/**"` are `["Dependencies", "/", "**"]`, for instance. Because `Dependencies/**` is more specific than `**`, no DLLs in the Dependencies folder are referenced by default.


Building on this, the pattern `Dependencies/*AspNet*/**` will match all ASP.NET dependencies that the referenced package contains, and will take precedence over the default exclude pattern because it contains more components, and is thus more specific. In case of ties, Include patterns take precedence over Exclude patterns.


There is one exception to this rule: any pattern ending with `.dll`, except patterns ending with `*.dll`, are considered literal expressions, and will always take precedence regardless of the number of tokens, thus allowing for easily targeting a specific dll by using a pattern like `**NameOfDll.dll`. Note however that specifying an empty pattern, e.g. ExcludeAssemblies="", will not override the default value of this attribute, and will still exclude `Dependencies/**`. In order to not include or exclude anything, you must provide a placeholder value instead.


Note that OpenTAP uses the [DotNet.Glob](https://github.com/dazinator/DotNet.Glob#patterns) library to generate matches, which uses unix-like globbing syntax. 

You can also specify a package that you just want installed (in e.g. bin/Debug/) but don't want your project to reference. This can be useful for defining a larger context in which to debug. It is done as follows:
```xml
<ItemGroup>
  <AdditionalOpenTapPackage Include="DMM Instruments" Version="2.1.2"/>
</ItemGroup>
```

## SDK Package

The Software Development Kit (SDK) package demonstrates the core capabilities of OpenTAP and makes it faster and simpler to develop your solutions. It also contains the Developer Guide which contains documentation relevant for developers.

The package is available on
[packages.opentap.io](http://packages.opentap.io/index.html#/?name=SDK). 
You can use the CLI install the package with the following command:
```
tap package install SDK
```
If you are using the Keysight Developer's System (Community or Enterprise Edition) you already have the SDK package.

### SDK Templates

The OpenTAP SDK makes it easy to create plugin templates using the `tap sdk new` group of subcommands. From the command line you can call the following subcommands:

| Commands                           | Description                                                                                         |
|------------------------------------|-----------------------------------------------------------------------------------------------------|
| **tap sdk gitversion**                 | Calculates a semantic version number for a specific git commit.                                     |
| **tap sdk new cliaction**              | Create a C# template for a CliAction plugin. Requires a project.                                    |
| **tap sdk new dut**                    | Create a C# template for a DUT plugin. Requires a project.                                          |
| **tap sdk new instrument**             | Create C# template for an Instrument plugin. Requires a project.                                    |
| **tap sdk new packagexml**             | Create a package definition file (package.xml).                                                     |
| **tap sdk new resultlistener**         | Create a C# template for a ResultListener plugin. Requires a project.                               |
| **tap sdk new settings**               | Create a C# template for a ComponentSetting plugin. Requires a project.                             |
| **tap sdk new testplan**               | **OBSOLETED:** Use [an editor](https://doc.opentap.io/User%20Guide/Editors/) to create a TestPlan.  |
| **tap sdk new teststep**               | Create a C# template for a TestStep plugin. Requires a project.                                     |
| **tap sdk new project**                | Create a C# Project (.csproj). Including a new TestStep, solution file (.sln) and package.xml.      |
| **tap sdk new integration gitlab-ci**  | Create a GitLab CI build script. For building and publishing the .TapPackage in the given project.  |
| **tap sdk new integration gitversion** | Configure automatic version of the package using version numbers generated from git history.        |
| **tap sdk new integration vs**         | Create files that enable building and debugging with Visual Studio.                                 |
| **tap sdk new integration vscode**     | Create files to enable building and debugging with vscode.                                          |

The following example shows how to create a new project using the SDK:

```
tap sdk new project MyAwesomePlugin
```

This command creates a new project called `MyAwesomePlugin` with a .csproj file, a test step class, a solution file, and a package.xml file.

Once you created a project you can easily add other templates. To add a DUT for example simply call the following command:

```
tap sdk new dut MyNewDut
```

Use an editor to create a testplan to use your new teststep and DUT! See https://doc.opentap.io/User%20Guide/Editors/ for more info on the different editors!

### SDK Examples

Before you start to create your own project, look at the projects and files in **`TAP_PATH\Packages\SDK\Examples`**. This folder provides code for example DUT, instrument and test step plugins. First-time OpenTAP developers should browse and build the projects, then use e.g. the Editor GUI to view the example DUTs, instruments and test steps. 

SDK Examples contains the following projects:

| **Folder**  | **Description** |
| -------- | --------  |
| **`ExamplePlugin\ExamplePlugin.csproj`**                           | Creates a plugin package that contains one DUT resource, one instrument resource, and one test step.   |
|**`PluginDevelopment\PluginDevelopment.csproj`**                    | Creates a plugin package that contains several test steps, DUT resources, instrument resources, and result listeners.                                              |
|**`TestPlanExecution\BuildTestPlan.Api\BuildTestPlan.Api.csproj`**  | Shows how to build, save and execute a test plan using the OpenTAP API.  |
|**`TestPlanExecution\RunTestPlan.Api\RunTestPlan.Api.csproj`**      | Shows how to load and run a test plan using the OpenTAP API.   |

## Offline Development

It is possible to develop plugins in an offline development. If you compile your project once, all online resources will be cached
locally so subsequent builds will not require internet access. If you cannot bring your development machine online even once, you can perform the following steps:

1. Install Dotnet 6 SDK
2. Create a directory for local NuGet packages. Let's call it `\path\to\nuget\source`
3. Download required NuGet packages and put them in the local source directory:
> [OpenTAP](https://www.nuget.org/packages/OpenTAP)

> [NETStandard.Library 2.0.3](https://www.nuget.org/packages/NETStandard.Library/2.0.3)

> [Microsoft.NETCore.Platforms 1.1.0](https://www.nuget.org/packages/Microsoft.NETCore.Platforms/1.1.0)
4. Add the local source to the list of nuget sources: 
> `dotnet nuget add source \path\to\nuget\source`
5. Build your project with e.g. dotnet build

With these steps, you should be able to build an OpenTAP plugin if it does not have any dependencies.
If you have additional NuGet dependencies, they can of course be added to the source you just created.

OpenTAP plugin dependencies consumed with the `AdditionalOpenTapPackage` feature must be added to the OpenTAP package cache:
- Windows: `C:\Users\<username>\Appdata\Local\OpenTap\PackageCache\`.
- Linux:  `/home/<username>/.local/share/OpenTap/PackageCache/`.

If the directory does not exist you must create it.

# Getting Started

When creating an OpenTAP plugin you are essentially creating a `dll` that implements specific OpenTAP plugin types, such as the test step or DUT types. The `dll` can enforce one or more plugin types and it can be packaged and versioned, thus [creating an OpenTAP package](../Plugin%20Packaging%20and%20Versioning/Readme.md) with the `.TapPackage` file extension that you can share with the community using the package manager.

To make it easier to develop plugins we created two options you can choose from when creating a project and plugin templates:

- Using the OpenTAP NuGet Package - This is the recommended way to get OpenTAP if you are developing plugin projects. The NuGet package is available on [nuget.org](https://www.nuget.org/packages/OpenTAP/).
- Using the OpenTAP SDK Package - This provides templates for many types of common OpenTAP plugins and can be used via:
  - The **OpenTAP Visual Studio Integration** - This allows you to use Visual Studio to create your plugins. You need Visual Studio 2022 or newer. If you are using the KS8400A PathWave Test Automation Developer's System, this is already included. Otherwise, it can be downloaded from the the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=OpenTAP.opentapsdkce).
  - The **Command Line** - This allows you to create project code templates from the command line. You can learn how in [The OpenTAP SDK Templates](#opentap-sdk-templates) section.

## NuGet Package

The OpenTAP NuGet Package has several helpful features for plugin development. You can start a new project or you can turn an existing project into a plugin. Any C# project (\*.csproj) can be turned into a plugin project by adding a reference to the OpenTAP NuGet package. 

### Create a Project

You need a C# project to hold your plugin classes. This can be an existing project, or you can start a new one. To start a new one we recommend choosing "Class Library (.NET Standard)" in the Visual Studio "Create a new project" wizard.

### Reference OpenTAP

Since the OpenTAP NuGet package is available on [nuget.org](https://www.nuget.org/packages/OpenTAP/) Visual Studio lists it in the "Browse" tab of the NuGet package manager. You can get to that by right clicking your project in the Solution Explorer and selecting "Manage NuGet Packages...". In the NuGet package manager, search for OpenTAP, and click install.

### NuGet Features

The OpenTAP NuGet package has a couple of additional built-in MSBuild features. These are discussed below.

#### Installation

When your project references the NuGet package, OpenTAP will automatically be installed in your project's output directory (e.g. bin/Debug/). The installation will be the version of OpenTAP specified in your project file (e.g. using the VS NuGet package manager as described above). This feature makes it easier to manage several plugins that may target different versions of OpenTAP. The version of OpenTAP is recorded in the \*.csproj file, which should be managed by version control (e.g. git), so all developers use the same version.

#### Package Creation

The NuGet package also adds build features to help packaging your plugins as a \*.TapPackage for distribution through OpenTAP's package management system (e.g. by publishing it on [packages.opentap.io](http://packages.opentap.io)). To take advantage of these features, your project needs a [package definition file](../Plugin%20Packaging%20and%20Versioning/Readme.md) (normally named package.xml). You can use the command line `tap sdk new packagexml` to generate a skeleton file. As soon as a file named package.xml is in your project, a TapPackage will be generated when it is built in Release mode. You can customize this behavior using these MSBuild properties in your csproj file:

```xml
<OpenTapPackageDefinitionPath>package.xml</OpenTapPackageDefinitionPath>
<CreateOpenTapPackage>true</CreateOpenTapPackage>
<InstallCreatedOpenTapPackage>true</InstallCreatedOpenTapPackage>
```

#### Reference Other OpenTAP Packages

When using the OpenTAP NuGet package, you can reference other TapPackages you need directly. TapPackages referenced like this will be installed into your projects output directory (usually ./bin/Debug/) along with OpenTAP itself.

You can specify an OpenTAP package that your project should reference. You do this by adding the following to your csproj file:
```xml
<ItemGroup>
  <!-- OpenTAP package sources -->
  <OpenTapPackageRepository Include="packages.opentap.io"/>
  <OpenTapPackageRepository Include="$HOME/Downloads;$HOME/Documents"/>

  <!-- Packages to reference  -->
  <OpenTapPackageReference Include="DMM API" Version="2.1.2" UnpackOnly="false" IncludeAssemblies="pattern1;pattern2" ExcludeAssemblies="pattern3;pattern4" />
</ItemGroup>
```
This is similar to the way you add a NuGet package using `<PackageReference/>`. The `<OpenTapPackageRepository/>` element
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

In order to develop OpenTAP plugins using the NuGet package in an offline environment, there are some manual steps:

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

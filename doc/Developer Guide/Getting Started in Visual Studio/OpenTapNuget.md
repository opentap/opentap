
## Getting started with your own project

This section describes the features of the OpenTAP nuget package, that helps when developing an OpenTAP plugin. Any C# project (*.csproj) can be turned into a plugin project by adding a reference to the OpenTAP nuget package. 

### Create project

First you will need a C# project to hold your plugin classes. This can be an existing project you have already started, or you can start a new one. To start a new one we recommend choosing "Class Library (.NET Standard)" in the Visual Studio "Create a new project" wizard.

### Reference OpenTAP

The OpenTAP NuGet package is the recommended way to get OpenTAP for plugin developers/projects. The NuGet package is availeble on [nuget.org](https://www.nuget.org/packages/OpenTAP/), so Visual Studio will list it in the build in nuget package manager. You can get to that by right clicking your project in the Solution Explorer and selecting "Manage Nuget Packages". In the nuget package manager, search for OpenTAP click install.

### OpenTAP NuGet features

This section describes the aditional msbuild features that come with the OpenTAP pNuGet package. 

#### OpenTAP installation

When your peoject referenes the nuget package, OpenTAP will automatically be installed in your project's output directory (e.g. bin/Debug/). The installation will be exactly the version of OpenTAP that you specified in your project file (e.g. using the VS nuget package manager as described above). This feature makes it easier to manage several plugins that might not target the same version of OpenTAP. The version of OpenTAP is recorded in the *.csproj file that should be managed by version control (e.g. git), so all developers use the same version.

#### OpenTAP Package creation

The nuget also adds build features to ease packaging your plugins as a *.TapPackage for distribution through OpenTAP's package management system (e.g. by publishing it on pckages.opentap.io). To take advantage of these features, your project needs a [package definition file](../Plugin%20Packaging%20and%20Versioning/Readme.md) (normally named package.xml). You can use the command line `tap sdk new packagexml` to generate a skeleton file (See the [SDK Template Generator](?) section for more details). As soon as a file named package.xml is in your project, a TapPackage will be generated when it is built in Release mode. You can costumize this behavior using these MSBUILD properties in your csproj file:
```xml
    <OpenTapPackageDefinitionPath>package.xml</OpenTapPackageDefinitionPath>
    <CreateOpenTapPackage>true</CreateOpenTapPackage>
    <InstallCreatedOpenTapPackage>true</InstallCreatedOpenTapPackage>
```

#### Reference other OpenTAP packages

When using the OpenTAP nuget, you can reference other TapPackages you need directly. TapPackages referenced like this will be installed into your projects output directory (e.g. bin/Debug/) along with OpenTAP itself.

You can specify an OpenTAP package that your project should reference. When doing this any .NET assemblies in that packages are added as references in your project. You do this by adding the following to your csproj file:
```xml
    <ItemGroup>
      <OpenTapPackageReference Include="DMM API" Version="2.1.2" Repository="packages.opentap.io"/>
    </ItemGroup>
```
This should be very similar to the way you add a nuget package using `<PackageReference>`. `Version` and `Repository` attributes are optional and if omitted defaults to latest release and packages.opentap.io respectively. 

You can also specify a package that you just want installed (in e.g. bin/Debug/) but don't want your project to reference. This can be useful for defining a larger context in which to debug and looks like below
```xml
    <ItemGroup>
      <AdditionalOpenTapPackage Include="DMM Instruments" Version="2.1.2" Repository="packages.opentap.io"/>
    </ItemGroup>
```
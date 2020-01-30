## Getting started with your own project

This section describes the helpful features of the OpenTAP NuGet package for developing plugins. Any C# project (\*.csproj) can be turned into a plugin project by adding a reference to the OpenTAP NuGet package. 

### Create project

First you will need a C# project to hold your plugin classes. This can be an existing project you have already started, or you can start a new one. To start a new one we recommend choosing "Class Library (.NET Standard)" in the Visual Studio "Create a new project" wizard.

### Reference OpenTAP

The OpenTAP NuGet package is the recommended way to get OpenTAP for plugin developers/projects. The NuGet package is available on [nuget.org](https://www.nuget.org/packages/OpenTAP/), so Visual Studio will list it in the build in the NuGet package manager. You can get to that by right clicking your project in the Solution Explorer and selecting "Manage NuGet Packages". In the NuGet package manager, search for OpenTAP, and click install.

### OpenTAP NuGet features

This section describes the additional msbuild features that come with the OpenTAP NuGet package. 

#### OpenTAP installation

When your project references the NuGet package, OpenTAP will automatically be installed in your project's output directory (e.g. bin/Debug/). The installation will be the version of OpenTAP specified in your project file (e.g. using the VS NuGet package manager as described above). This feature makes it easier to manage several plugins that might not target the same version of OpenTAP. The version of OpenTAP is recorded in the \*.csproj file, which should be managed by version control (e.g. git), so all developers use the same version.

#### OpenTAP Package creation

The NuGet package also adds build features to help packaging your plugins as a \*.TapPackage for distribution through OpenTAP's package management system (e.g. by publishing it on packages.opentap.io). To take advantage of these features, your project needs a [package definition file](../Plugin%20Packaging%20and%20Versioning/Readme.md) (normally named package.xml). You can use the command line `tap sdk new packagexml` to generate a skeleton file. As soon as a file named package.xml is in your project, a TapPackage will be generated when it is built in Release mode. You can customize this behavior using these MSBUILD properties in your csproj file:
```xml
<OpenTapPackageDefinitionPath>package.xml</OpenTapPackageDefinitionPath>
<CreateOpenTapPackage>true</CreateOpenTapPackage>
<InstallCreatedOpenTapPackage>true</InstallCreatedOpenTapPackage>
```

#### Reference other OpenTAP packages

When using the OpenTAP NuGet package, you can reference other TapPackages you need directly. TapPackages referenced like this will be installed into your projects output directory (e.g. bin/Debug/) along with OpenTAP itself.

You can specify an OpenTAP package that your project should reference. When doing this any .NET assemblies in that packages are added as references in your project. You do this by adding the following to your csproj file:
```xml
<ItemGroup>
  <OpenTapPackageReference Include="DMM API" Version="2.1.2" Repository="packages.opentap.io"/>
</ItemGroup>
```
This should be very similar to the way you add a NuGet package using `<PackageReference>`. `Version` and `Repository` are optional attributes, and default to latest release, and packages.opentap.io if omitted.

You can also specify a package that you just want installed (in e.g. bin/Debug/) but don't want your project to reference. This can be useful for defining a larger context in which to debug. It is done as follows:
```xml
<ItemGroup>
  <AdditionalOpenTapPackage Include="DMM Instruments" Version="2.1.2" Repository="packages.opentap.io"/>
</ItemGroup>
```

Plugin Packaging and Versioning
===============================
## Packaging
A TAP Package is a file that contains plugin DLLs and supporting files. Packages are used to distribute TAP plugins, while providing support for correct versioning and dependency checking. This section deals with the construction and use of TAP packages. The different programs and processes involved are described below:

- The TAP installation includes the **Package Manager** accessible by the `tap package` command. This can be used to create, install or uninstall packages, list installed packages, and run tests on one or more packages.
- The TAP installation also includes the **Keysight.OpenTap.PackageManager.Gui.exe** program. The PackageManager has a GUI that permits package downloading, displays an inventory of the packages, and ultimately installs package files found into the TAP install directory.
- The default TAP plugin project (release builds only) includes an *AfterBuild* task for creating a TAP Package based on package declarations in the package.xml file. The resulting TAP package has the **.TapPackage** suffix. Files with this suffix are renamed zip files, and as such, can be examined with a file compressor and archiver software, such as WinZip.

When run from Visual Studio, most of the processes of the packaging system are automatic and invisible to the operation. However, the developer may wish to modify the content and/or properties of the package by editing the package.xml file. The following package.xml is found in `TAP_PATH\SDK Examples\ExamplePlugin`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!-- 
InfoLink: Specifies a location where additional information about the package can be found.
Version: The version of the package. Must be in a semver 2.0 compatible format. This can be automatically updated from GIT.

For Version the following macro is available (Only works if the project directory is under Git source control):
$(GitVersion) - Gets the version number in the recommended format Major.Minor.Build-PreRelease+CommitHash.BranchName.
-->
<Package Name="PluginExample"
         xmlns="http://opentap.io/schemas/package"
         InfoLink="http://www.keysight.com/"
         Version="0.1.0-alpha">
  <Description>Plugins from the example.</Description>
  <Files>
    <!--Obfuscate only applies to C# assemblies.
    Obfuscate = "false" (the default) indicates that there should be no attempt to obfuscate this assembly.
    If set to true, Dotfuscator Professional Edition must be installed, and the assembly will be obfuscated.-->

    <!--SetAssemblyInfo updates assembly info according to package version.-->
    <File Path="Packages/PluginExample/Example.Tap.Plugins.ExamplePlugin.dll" Obfuscate="false"  SetAssemblyInfo="Version"></File>
    <File Path="Packages/PluginExample/SomeSampleData.txt"></File>
  </Files>
</Package>
```

**Note**: A package that references a TAP assembly version 8 is compatible with any TAP version 8.y, but not compatible with an earlier TAP version 7 or a future TAP version 9. The PackageManager checks version compatibility before installing packages.  

## Packaging Configuration File
When creating a TAP Package the configuration is specified using an xml file (typically called package.xml).

The configuration file supports five optional attributes:

| **Attribute** | **Description** |
| ---- | -------- |
| **InfoLink**   | Specifies a location where additional information about the package can be found. It is visible in the Package Manager as the **More Information** link.  |
| **Version**  | The version of the package. This field supports the $(GitVersion) macro. The version is displayed in the Package Manager. See [Versioning](#Versioning) for more details. |
| **OS**   | Used to filter packages which are compatible with the operating system the PackageManager is running on. If the attribute is not specified, the default "Windows" is used. |
| **Architecture**   | Used to filter packages which are compatible with a certain CPU architecture. If the attribute is not specified it is assumed that the Plugin works on all architectures. |
| **Class**   | This attribute is used to classify a package. It can be set to **package**, **bundle** or **system-wide** (default value: **package**). A package of class **bundle** references a collection of TAP packages, but does not contain the referenced packages. Packages in a bundle do not need to depend on each other to be referenced. For example, TAP Developer's System is a bundle that reference the TAP GUI, Timing Analyzer, Results Viewer, and SDK packages. <br><br> A package of class **system-wide** is installed in a global system folder so these packages can affect other installations of TAP and cannot be uninstalled with the PackageManager. System-wide packages should not be TAP plugins, but rather drivers and libraries.  The system folders are located differently depending on operating system and drive specifications: Windows (normally) - `C:\ProgramData\Keysight\TAP`, Linux -  `/usr/share/Keysight/TAP`|

The content of one of the strings assigned to the `OS` attribute must be contained in the output of the commands `uname -a` on linux/osx or `ver` on windows for the plugin to be considered compatible. The use of strings like `"Windows"`, `"Linux"` or `"Ubuntu"` is recommended. However, it is possible to use abbreviations, such as `"Win"` or to target a specific version of an operating system. This can be done by writing the exact name and the version number. For example, a plugin with the `OS` attribute `"Microsoft Windows [Version 10.0.14393]"` targets the specified version of Windows and is incompatible with other versions or operating systems. 

Inside the configuration file, the **File** element supports the following attributes:

| **Attribute** | **Description** |
| ---- | -------- |
| **Path** | The path to the file. This is relative to the root the OpenTAP installation directory. This serves as both source (where the packaging tool should get the file when creating the package) and target (where the file sould be located when installed). Unless there are special requirements, the convention is to put all payload files in a Packages/<PackageName>/ subfolder.
| **SourcePath** | Optional. If present the packaging tool will get the file from this path when creating the package.
| **Obfuscate**   | Used to obfuscate the assembly file. When not specified, the Obfuscate attribute defaults to false. To use this attribute, you need Dotfuscator Professional Edition installed on your PC.   |
| **SetAssemblyInfo**  | Used to set the version of the assembly file. When `SetAssemblyInfo` is set to `Version`, AssemblyVersion, AssemblyFileVersion and AssemblyInformationalVersion attributes of the file are set according to the package's version. |
| **Sign**   | Used to sign the package. It takes the certificate name as an argument. To use this attribute you need to have signing certificates installed in the Windows Certificate Store and the signtool needs to be in one of the supported locations, such as `C:\Program Files (x86)\Microsoft SDKs`. |
| **UseVersion**   | Package version can be set with the UseVersion attribute. If true (defaults to false) the package version is set to the version of the assembly file. This attribute should only be set in one File element. If more are present, the last File will be used to set the package version. The UseVersion attribute should not be used if the Version attribute is specified for the package. |


### Example

The below configuration file results in `MyPlugin.{version}.TapPackage` file,containing `TapPlugin.MyPlugin.dll`, `waveform1.wfm` and `waveform2.wfm`. `TapPlugin.MyPlugin.dll` is obfuscated but none of the waveform files are.  

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package Name="MyPlugin" xmlns="http://opentap.io/schemas/package" InfoLink="http://myplugin.com"
		 Version="$(GitVersion)" OS="Windows,Linux" Architecture="x64">
  <Description>
    This is an example of an "package.xml" file.
    <Status>Released</Status>
    <Organisation>Keysight Technologies</Organisation>
    <Contacts>
      <Contact Email="tap.support@keysight.com" Name="TAP Support"/>
    </Contacts>
    <Prerequisites>None</Prerequisites>
    <Hardware>Emulated PSU</Hardware>
    <Links>
      <Link Description="Description of the MyPlugin" Name="MyPlugin" Url="http://www.keysight.com/find/TAP"/>
    </Links>
  </Description>  
  <Files>
    <File Path="Packages/MyPlugin/TapPlugin.MyPlugin.dll" Obfuscate="true" SetAssemblyInfo="Version" Sign="Keysight Technologies..."></File>
    <File Path="Packages/MyPlugin/waveform1.wfm"></File>
    <File Path="Packages/MyPlugin/waveform2.wfm"></File>
  </Files>
</Package>
```

In this example the package version is set according to Git tag and branch, since `GitVersion` is expanded based on Git (described later in this section). The resulting filename would be something like `MyPlugin.8.1.103+d58122db.TapPackage`. Additionally, the `TapPlugin.MyPlugin.dll` file would have the same version as the package, according to the `SetAssemblyInfo` attribute and it would be signed using a Keysight Technologies certificate. The `OS` attribute of the file is set to `Windows,Linux` which means that the package is compatible with both Windows and Linux operating systems. The `Architecture` attribute set to `x64`, indicating that the plugin is compatible with 64-bit processors. 

This `package.xml` file is preserved inside the TapPackage as metadata. The Package Manager will add some additional information to the file. The metadata file for the above configuration could look like the following:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Package Version="8.1.103+d58122db" Name="MyPlugin" InfoLink="http://myplugin.com" Date="10/18/2017 08:04:46" OS="Windows,Linux" Architecture="x64" xmlns="http://opentap.io/schemas/package">
  <Description>
    This is an example of an "package.xml" file.
    <Status>Released</Status>
    <Organisation>Keysight Technologies</Organisation>
    <Contacts>
      <Contact Email="tap.support@keysight.com" Name="TAP Support"/>
    </Contacts>
    <Prerequisites>None</Prerequisites>
    <Hardware>Emulated PSU</Hardware>
    <Links>
      <Link Description="Description of the MyPlugin" Name="MyPlugin" Url="http://www.keysight.com/find/TAP"/>
    </Links>
  </Description>  
  <Dependencies>
    <PackageDependency Package="TAP Base" Version="8.1" />
  </Dependencies>
  <Files>
    <File Path="Packages/MyPlugin/TapPlugin.MyPlugin.dll" Obfuscate="true" UseVersion="false" SetAssemblyInfo="Version" Sign="Keysight Technologies...">
      <Plugins>
        <Plugin>TapPlugin.MyPlugin.Step</Plugin>
        <Plugin>TapPlugin.MyPlugin.MyDut</Plugin>
      </Plugins>
    </File>
    <File Path="Packages/MyPlugin/waveform1.wfm" Obfuscate="false" UseVersion="false"></File>
    <File Path="Packages/MyPlugin/waveform2.wfm" Obfuscate="false" UseVersion="false"></File>
  </Files>
</Package>
```

The dependency and version information added by the Package Manager allows it to determine whether all prerequisites have been met when trying to install the package on the client.

If the package has dependencies on other packages it is possible to create a file with the .TapPackages extension. This is essentially a zip file that contains the created package and all the other packages it depends on. This allows the installation of all necessary packages at the same time, thus making package distribution easier.

## Command Line Use
You can create a TAP package from the command line or from MSBUILD (directly in Visual Studio). If you create a TAP project in Visual Studio using the SDK, the resulting project is set up to generate a .TapPackage using the Keysight.Tap.Sdk.MSBuild.dll (only when building in "Release" configuration).

**tap.exe** is the TAP command line tool. It can be used for different package related operations using the "package" group of subcommands. The following subcommands are supported:

| **Command** | **Description** |
| ---- | -------- |
| **tap package create** | Creates a package based on an XML description file.   |
| **tap package install** | Install one or more packages.  |
| **tap package list** | List installed packages.   |
| **tap package uninstall** | Uninstall one or more packages.   |
| **tap package test** | Runs tests on one or more packages.   |

The following example shows how to create TAP packages based on package.xml:
```
tap.exe package create -v package.xml
```
The behavior of the `tap package create` command when packaging, can be customized using arguments. To list these arguments, from a terminal call the following:

```bash
$ tap.exe package create --help
Options:
Usage: create [-h] [-v] [-c] [--project-directory <arg>] [--no-obfuscation] [--obfuscator <arg>] [-o <arg>] [-p <arg>] <PackageXmlFile>
  -h, --help             Write help information.
  -v, --verbose          Also show verbose/debug level messages.
  -c, --color            Color messages according to their level.
  --project-directory    The directory containing the GIT repo.
                         Used to get values for version/branch macros.
  --no-obfuscation       Specify this to skip any obfuscation and signing steps.
  --obfuscator           Specify which .Net obfuscator to use.
  -o, --out              Path to the output file.
  -p, --prerelease       Set type of prerelease
```

### Obfuscation
TAP now supports two different obfuscators: Preemptive Software Dotfuscator (official Keysight obfuscation tool), and Obfuscar v2.2.9. These can be selected using the `--obfuscator` command line argument.

Obfuscar requires the `Obfuscar.Console.exe` tool to be located in the working directory, or in a directory pointed to by an environment variable `%OBFUSCAR_PATH%`.

## Versioning
The TAP executables and TAP packages are versioned independently and should use semantic versioning (see definition [here](https://semver.org/)). TAP versions are of the form **X**.**Y**.**Z**+**W**, where:

- X is the major version number, incremented upon changes that **break** backwards-compatibility.
-	Y is the minor version number, incremented upon backwards-compatible changes.
-	Z is the patch version, incremented upon every set of code changes. The patch version can include pre-release labels.
-	W is the metadata (e.g. Git short commit hash).

It is possible to set the version of the *.TapPackage using one of the following methods:

- Git Assisted Versioning
- Manual Versioning
- UseVersion Attribute

### Git Assisted Versioning

The **$(GitVersion) Macro** can be used in the Version attribute of the Package and File element in package.xml. It enables using Git annotated tags to manage the versioning. Git supports two types of tags: Lightweight and Annotated. Git Assisted Versioning only works for  Annotated Tags. If the repository does not have any **Annotated Tags**, the macro expands to 0.0.0 when the TapPackage is built. Git assisted versioning follows semantic versioning with the **X**.**Y**.**Z**+**W** format (as described earlier). It enables you to build packages like follows, when `Version` is set to `$(GitVersion)` for the Package element in package.xml:

- **release**: The code is on a branch named "release" or "ship" (e.g. "release8x"). The format of the resulting package version is **X**.**Y**.**Z**+**W**. For example, if the last commit for "MyPackage" is marked with the `v1.0` Git annotated tag, MyPackage.1.0.0+c5317128.TapPackage is created. The metadata is set to the Git short commit hash (`c5317128`).

- **rc**: The code is on a release candidate branch named "rc" (e.g. "rc8x"). The format of the resulting package version is **X**.**Y**.**Z**-**rc**+**W**. For example: MyPackage.1.0.0-rc+c5317128, where the patch version includes the "-rc" pre-release label.

- **beta**: The code is on a pre-release candidate branch named "integration", "develop", "dev" or "master". The format of the resulting package version is **X**.**Y**.**Z**-**beta**+**W**. For example: MyPackage.1.0.0-beta+c5317128, where the patch version includes the "-beta" pre-release label.

- **alpha**: Code is on an alpha/feature branch. All branches, which do not meet the above criteria, are considered as alpha/feature branches. The format of the resulting package version is **X**.**Y**.**Z**-**alpha**+**W**.**BRANCH_NAME**. For example: MyPackage.1.0.0-alpha+c5317128.456-FeatureBranch, where the branch name is appended to the metadata.

To add and push Annotated Tag to the latest commit, run the following command in your project folder:

  ```sh
  git tag -a v1.0 -m "version 1.0"
  git push --tags
  ```

Annotated Tags can also be created in Visual Studio. This is done by including a tag message during tag creation. A Lightweight Tag, which TAP is not able to use for package versioning, is created if the tag message is left out.

The example above marks the latest commit as version 1.0. When the package is created, the major and minor version number of the package is set based on the tag. On subsequent commits, the build number (the third number) will automatically increment (1.0.1 , 1.0.2 , etc.). To increment the major or minor version number (first and second number), another Annotated Tag can be added.

### Manual Versioning
The version can be set manually, e.g. `Version="1.0.2"`. The version **must** follow semantic versioning.

### UseVersion Attribute
The UseVersion attribute is specified for the File element in package.xml. The version of the TapPackage is set based on the version number in the AssemblyInfo.cs file. You can manually increment the version in AssemblyInfo.cs. The version **must** follow semantic versioning. For example, you can specify the version number like this in AssemblyInfo.cs for "MyPackage":

  ```csharp
  [assembly: AssemblyInformationalVersion("0.1.8")]
  ```

According to the above specification, when the package is created, its version is set to 0.1.8.
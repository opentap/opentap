Release Notes  - OpenTAP 9.17
=============

New Features:
-------

- Upgrade to .NET 6 [#50](https://github.com/opentap/opentap/issues/50)
- System-wide package cache [#102](https://github.com/opentap/opentap/issues/102)
- Add Url -> Add Package Location [#272](https://github.com/opentap/opentap/issues/272)
- Support for adding `IResultListener` when the test plan starts [#298](https://github.com/opentap/opentap/issues/298)
- Create an Admin Flag in `PackageActions` [#40](https://github.com/opentap/opentap/issues/40)
- Cannot update system packages, admin rights required [#47](https://github.com/opentap/opentap/issues/47)
- Dynamic Package XML: Conditions and Targets, Platforms, Configurations, etc [#76](https://github.com/opentap/opentap/issues/76)
- `CliActions` to support more argument types [#82](https://github.com/opentap/opentap/issues/82)
- `IfVerdict` Continue only works if the step is the immediate child of a loop step [#88](https://github.com/opentap/opentap/issues/88)
- OpenTap Picture SDK example [#116](https://github.com/opentap/opentap/issues/116)


Usability Improvements: 
-------

- `tap package show` correlation between architecture and platform [#348](https://github.com/opentap/opentap/issues/348)
- `tap sdk new project` project name not properly validated [#123](https://github.com/opentap/opentap/issues/123)
- Deserializing Settings without required package has confusing log warning. [#274](https://github.com/opentap/opentap/issues/274)
- When installing a package from a repository that does not exists, the error seems overly verbose [#64](https://github.com/opentap/opentap/issues/64)
- `TapSerializer` errors should be accessible from plugins [#87](https://github.com/opentap/opentap/issues/87)
- Emit a warning if multiple packages are found with the same name. [#100](https://github.com/opentap/opentap/issues/100)
- `LicenseRequired=""` inserted into OpenTAP package XML [#103](https://github.com/opentap/opentap/issues/103)
- Packages for the wrong platform can be installed without any warning [#106](https://github.com/opentap/opentap/issues/106)
- Obsolete Newtonsoft APIs [#110](https://github.com/opentap/opentap/issues/110)
- Ubuntu: Incorrect cache message when not using `--repository` [#337](https://github.com/opentap/opentap/issues/337)
- `Tap.Upgrader` should not try to update `tap.exe` if the input is the same as the output [#339](https://github.com/opentap/opentap/issues/339)
- `tap package show` compatible platform Linux shown twice [#346](https://github.com/opentap/opentap/issues/346)
- Typos for `Use Local Package Cache` tooltip [#391](https://github.com/opentap/opentap/issues/391)
- Update `PluginDevelopment.Gui` in Examples to use .NET Framework 4.7.2 [#398](https://github.com/opentap/opentap/issues/398)
- Implement downloading from MockRepository [#402](https://github.com/opentap/opentap/issues/402)
- Allow controlling the `WorkQueue.EnqueueWork` thread context [#408](https://github.com/opentap/opentap/issues/408)
- `PackageDependencies` should always be prepended with ^ [#243](https://github.com/opentap/opentap/issues/243)
- Install OpenTAP as package in debug builds [#248](https://github.com/opentap/opentap/issues/248)
- Support overlapping enum values [#264](https://github.com/opentap/opentap/issues/264)
- Make `ScpiInstrument` Not Abstract [#84](https://github.com/opentap/opentap/issues/84)
- Version injection using the Mono method cannot add a version attribute [#89](https://github.com/opentap/opentap/issues/89)
- Parent Verdict is set to Error on Break Condition of Child Step [#90](https://github.com/opentap/opentap/issues/90)
- Step Break Conditions - break on Pass? [#91](https://github.com/opentap/opentap/issues/91)
- Migrate to using `SmartInstaller` instead of Inno [#98](https://github.com/opentap/opentap/issues/98)
- Add key/value list for additional custom metadata to `PackageDef` [#114](https://github.com/opentap/opentap/issues/114)


Bug Fixes: 
-------

- No user dialogs when breaking install by downgrading OpenTAP [#411](https://github.com/opentap/opentap/issues/411)
- Incorrect OS when installing dependencies [#341](https://github.com/opentap/opentap/issues/341)
- `Run` command is returning an error on parameterized test plan [#350](https://github.com/opentap/opentap/issues/350)
- Uninstall Developer's System - not working properly [#376](https://github.com/opentap/opentap/issues/376)
- `GenericSequenceAnnotation` cache bug [#378](https://github.com/opentap/opentap/issues/378)
- `GetVersion` throws an exception when serializing test steps [#380](https://github.com/opentap/opentap/issues/380)
- Cannot update OpenTAP to release version [#421](https://github.com/opentap/opentap/issues/421)
- Skipping assembly `Tap.Upgrader.exe`. Image is too small. [#422](https://github.com/opentap/opentap/issues/422)
- `TestPlanRun` constructor throw when resultListeners is null [#434](https://github.com/opentap/opentap/issues/434)
- Unable to rebuild projects: Files are in use [#447](https://github.com/opentap/opentap/issues/447)
- Writing annotations fails when using shared projects [#195](https://github.com/opentap/opentap/issues/195)
- Upgrading OpenTAP 9.16.2 to 9.17.0-beta.8 is working without .NET 6 [#211](https://github.com/opentap/opentap/issues/211)
- Signing fails on main branch [#233](https://github.com/opentap/opentap/issues/233)
- `AvailableValues` not updating automatically [#254](https://github.com/opentap/opentap/issues/254)
- `AvailableValueList` is not refreshed by the GUI [#255](https://github.com/opentap/opentap/issues/255)
- `TypeData` hard crash when loading dll's from incompatible frameworks [#278](https://github.com/opentap/opentap/issues/278)
- Version resolution issue in `tap package install` [#311](https://github.com/opentap/opentap/issues/311)
- Test Plan break does not work for Pass and Inconclusive [#316](https://github.com/opentap/opentap/issues/316)
- Error displayed in log "Installed OpenTAP version is not compatible" [#327](https://github.com/opentap/opentap/issues/327)
- Multiple assemblies of different versions named tap exists [#333](https://github.com/opentap/opentap/issues/333)
- ComponentSettings `SaveAllCurrentSettings` does not save all current settings [#113](https://github.com/opentap/opentap/issues/113)
- Incompatible dll references are silently ignored [#119](https://github.com/opentap/opentap/issues/119)
- `tap sdk gitversion` fails on RHEL 8 [#99](https://github.com/opentap/opentap/issues/99)
- .NET 6 MonoResolver cannot resolve GAC assemblies [#337](https://github.com/opentap/opentap/issues/337)
- Uninstall HTML5 does not work properly. Exception in log [#343](https://github.com/opentap/opentap/issues/343)
- Could not install packages from Package Cache [#345](https://github.com/opentap/opentap/issues/345)
- `TestStepList.AllowType(Type,Type)` throws exception for .net 6 references [#352](https://github.com/opentap/opentap/issues/352)
- Image install Msbuild does not download all packages [#386](https://github.com/opentap/opentap/issues/386)
- Cannot downgrade OpenTAP 9.17 to 9.16 [#394](https://github.com/opentap/opentap/issues/394)
- Unhandled GUI error when adding a sweep value in sweep loop: parameter name source null [#468](https://github.com/opentap/opentap/issues/468)
- Version resolution can't handle simple usecases [#188](https://github.com/opentap/opentap/issues/188)
- `PackageDependencies` are not written to XML files! [#241](https://github.com/opentap/opentap/issues/241)
- OpenTAP Picture Example: Default path incorrect [#256](https://github.com/opentap/opentap/issues/256)
- `tap.exe` and `tap.dll` have different versions [#315](https://github.com/opentap/opentap/issues/315)
- OpenTAP nuget installs 32bit OpenTAP in bin folders [#330](https://github.com/opentap/opentap/issues/330)
- Ubuntu - Installing plugins to target folder does not work [#336](https://github.com/opentap/opentap/issues/336)
- `GenerateOpenTapReferenceProps` sometimes fails on 2nd build [#107](https://github.com/opentap/opentap/issues/107)


Documentation: 
-------

- Remove VS2015 needed software required - "Getting Started" section from Developer Guide [#258](https://github.com/opentap/opentap/issues/258)
- Recommend against using Snap for dotnet on linux [#259](https://github.com/opentap/opentap/issues/259)
- Document scoped parameters in user guide [#57](https://github.com/opentap/opentap/issues/57)



















































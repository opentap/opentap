Release Notes  - OpenTAP 9.17
=============

New Features:
-------

- System-wide package cache [#102]https://github.com/opentap/opentap/issues/102
- Add Url -> Add Package Location [#272](https://github.com/opentap/opentap/issues/272)
- Support for adding IResultListener when the test plan starts [#298](https://github.com/opentap/opentap/issues/298)
- Create an Admin Flag in PackageActions [#40](https://github.com/opentap/opentap/issues/40)
- Cannot update system packages, admin rights required [#47](https://github.com/opentap/opentap/issues/47)
- Dynamic Package XML: Conditions and Targets, Platforms, Configurations, etc [#76](https://github.com/opentap/opentap/issues/76)
- CliActions to support more argument types [#82] (https://github.com/opentap/opentap/issues/82)
- IfVerdict Continue only works if the step is the immediate child of a loop step [#88](https://github.com/opentap/opentap/issues/88)


Usability Improvements: 
-------

- tap package show correlation between architecture and platform [#348](https://github.com/opentap/opentap/issues/348)
- 'tap sdk new project' project name not properly validated [#123](https://github.com/opentap/opentap/issues/123)
- Deserializing Settings without required package has confusing log warning. [#274](https://github.com/opentap/opentap/issues/274)
- When installing a package from a repository that does not exists, the error seems overly verbose [#64](https://github.com/opentap/opentap/issues/64)
- TapSerializer errors should be accessible from plugins [#87](https://github.com/opentap/opentap/issues/87)
- tap sdk gitversion fails on RHEL 8 [#99](https://github.com/opentap/opentap/issues/99)
- Emit a warning if multiple packages are found with the same name. [#100](https://github.com/opentap/opentap/issues/100)
- LicenseRequired="" inserted into OpenTAP package XML [#103](https://github.com/opentap/opentap/issues/103)
- Packages for the wrong platform can be installed without any warning [#106](https://github.com/opentap/opentap/issues/106) 


Bug Fixes: 
-------

- No user dialogs when breaking install by downgrading OpenTAP [#411](https://github.com/opentap/opentap/issues/411)
- Incorrect OS when installing dependencies [#341](https://github.com/opentap/opentap/issues/341)
- Run command is returning an error on parameterized test plan [#350](https://github.com/opentap/opentap/issues/350)
- Uninstall Developer's System - not working properly [#376](https://github.com/opentap/opentap/issues/376)
- GenericSequenceAnnotation cache bug [#378](https://github.com/opentap/opentap/issues/378)
- GetVersion throws an exception when serializing test steps [#380](https://github.com/opentap/opentap/issues/380)
- Cannot update OpenTAP to release version [#421](https://github.com/opentap/opentap/issues/421)
- Skipping assembly 'Tap.Upgrader.exe'. Image is too small. [#422](https://github.com/opentap/opentap/issues/422)
- TestPlanRun constructor throw when resultListeners is null [#434](https://github.com/opentap/opentap/issues/434)
- Unable to rebuild projects: Files are in use [#447](https://github.com/opentap/opentap/issues/447)
- Writing annotations fails when using shared projects [#195](https://github.com/opentap/opentap/issues/195)
- Upgrading OpenTAP 9.16.2 to 9.17.0-beta.8 is working without .NET 6 [#211](https://github.com/opentap/opentap/issues/211)
- Signing fails on main branch [#233](https://github.com/opentap/opentap/issues/233)
- AvailableValues not updating automatically [#254](https://github.com/opentap/opentap/issues/254)
- AvailableValueList is not refreshed by the GUI [#255](https://github.com/opentap/opentap/issues/255)
- TypeData hard crash when loading dll's from incompatible frameworks [#278](https://github.com/opentap/opentap/issues/278)
- Version resolution issue in tap package install [#311](https://github.com/opentap/opentap/issues/311)
- Test Plan break does not work for Pass and Inconclusive [#316](https://github.com/opentap/opentap/issues/316)
- Error displayed in log "Installed OpenTAP version is not compatible" [#327](https://github.com/opentap/opentap/issues/327)
- Multiple assemblies of different versions named tap exists [#333](https://github.com/opentap/opentap/issues/333)
- ComponentSettings SaveAllCurrentSettings does not save all current settings [#113](https://github.com/opentap/opentap/issues/113)
- Incompatible dll references are silently ignored [#119](https://github.com/opentap/opentap/issues/119)























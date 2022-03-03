Release Notes  - OpenTAP 9.17
=============

New Features:
-------

- Upgrade to .NET 6 [#50](https://github.com/opentap/opentap/issues/50)
- Migrate to using 'SmartInstaller' instead of Inno [#98](https://github.com/opentap/opentap/issues/98)


Usability Improvements: 
-------

- CliActions to support more argument types [#82](https://github.com/opentap/opentap/issues/82)
- Step Break Conditions - break on Pass? [#91](https://github.com/opentap/opentap/issues/91)
- System-wide package cache [#102](https://github.com/opentap/opentap/issues/102)
- Packages for the wrong platform can be installed without any warning [#106](https://github.com/opentap/opentap/issues/106)


Bug Fixes: 
-------

- Parent Verdict is set to Error on Break Condition of Child Step [#90](https://github.com/opentap/opentap/issues/90)
- ComponentSettings SaveAllCurrentSettings does not save all current settings [#113](https://github.com/opentap/opentap/issues/113)
- Incompatible dll references are silently ignored [#119](https://github.com/opentap/opentap/issues/119)
- Hidden Files cannot be overwritten by package install [#182](https://github.com/opentap/opentap/issues/182)
- TypeData hard crash when loading dll's from incompatible frameworks [#278](https://github.com/opentap/opentap/issues/278)
- Test Plan break does not work for Pass and Inconclusive [#316](https://github.com/opentap/opentap/issues/316)
- Error displayed in log "Installed OpenTAP version is not compatible" [#327](https://github.com/opentap/opentap/issues/327)
- Run command is returning an error on parameterized test plan [#350](https://github.com/opentap/opentap/issues/350)




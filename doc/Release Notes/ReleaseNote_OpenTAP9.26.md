Release Notes - opentap 9.26.0 
 ============= 

New Features 
 ------- 

- Made ViaPoint.IsActive virtual [#1745](https://github.com/opentap/opentap/issues/1745)
- Connection.GetOtherPort now uses Equals instead of reference equality check [#1699](https://github.com/opentap/opentap/issues/1699)
- ScpiQuery and ScpiCommand from SCPIInstrument are now thread safe [#1698](https://github.com/opentap/opentap/issues/1698)
- Add PluginManager Initialize method [#1683](https://github.com/opentap/opentap/issues/1683)
- Add support for disabling VISA discovery [#1678](https://github.com/opentap/opentap/issues/1678)


Bug Fixes 
 ------- 

- SetAssemblyInfo no longer adds System.Private.CoreLib as a dependency when building on Linux [#1684](https://github.com/opentap/opentap/issues/1684)
- Linux: `.TapPackage.lock` file is now properly deleted after downloading a package [#1662](https://github.com/opentap/opentap/issues/1662)
- `tap sdk new integration vscode -o <path>` now treats path as a directory rather than the output file name [#1533](https://github.com/opentap/opentap/issues/1533)
- RfConnection.GetInterpolatedCableLoss no longer throws an exception when there is no calibration data [#1701](https://github.com/opentap/opentap/issues/1701)


Other 
 ------- 

- Show appropriate error when installing SDK while 32 bit dotnet is in PATH [#1672](https://github.com/opentap/opentap/issues/1672)
- NuGet: speed up incremental builds [#1670](https://github.com/opentap/opentap/issues/1670)

Release Notes - opentap 9.24.0 
 ============= 

New Features 
 ------- 

- Report artifact size with units [#1465](https://github.com/opentap/opentap/issues/1465)
- Provide the iteration number in the Repeat Step [#1455](https://github.com/opentap/opentap/issues/1455)
- tap package download should have --non-interactive [#1387](https://github.com/opentap/opentap/issues/1387)
- Allow setting a repository token from the CLI [#1383](https://github.com/opentap/opentap/issues/1383)
- Allow Assignment of Outputs to Members with Compatible Types [#1368](https://github.com/opentap/opentap/issues/1368)
- Add TestPlanRun / TestStepRun property: Status [#1117](https://github.com/opentap/opentap/issues/1117)
- `DateTime` fields are not handled properly by OpenTAP GUI [#659](https://github.com/opentap/opentap/issues/659)
- CliUserInputImplementation: Handle case when no TTY is connected [#1092](https://github.com/opentap/opentap/issues/1092)
- Make tap.sh shell independent [#1423](https://github.com/opentap/opentap/issues/1423)


Bug Fixes 
 ------- 

- ParentAnnotation is Null casuing an Exception [#1507](https://github.com/opentap/opentap/issues/1507)
- Allow ScpiInstrument to not send IDN on connect [#1497](https://github.com/opentap/opentap/issues/1497)
- Incorrect stepRun exception set in Defer [#1492](https://github.com/opentap/opentap/issues/1492)
- Log rollover does not work when producing data too fast [#1471](https://github.com/opentap/opentap/issues/1471)
- Offline image resolution problems [#1469](https://github.com/opentap/opentap/issues/1469)
- Empty logs while logs rollover [#1464](https://github.com/opentap/opentap/issues/1464)
- Test Plan Editor: "If" parameter from "If Verdict" step is cleared after parent step is moved as child of another step [#1456](https://github.com/opentap/opentap/issues/1456)
- CheckForUpdates does not work for FilePackageRepository [#1445](https://github.com/opentap/opentap/issues/1445)
- The version of plugins in DMM bundle does not change when bundle is downgraded [#1159](https://github.com/opentap/opentap/issues/1159)
- CLI repository determined incorrectly  [#1431](https://github.com/opentap/opentap/issues/1431)
- tap.exe package install --repository flag does not use https [#1326](https://github.com/opentap/opentap/issues/1326)


Usability Improvements 
 ------- 

- Align tap sdk new and dotnet new opentap [#1319](https://github.com/opentap/opentap/issues/1319)
- Made debug log colors readable in Gnome Terminal default theme [#1264](https://github.com/opentap/opentap/issues/1264)


Documentation 
 ------- 

- Visual Studio Template Support [#1462](https://github.com/opentap/opentap/issues/1462)
- Update `Break Conditions` documentation [#1452](https://github.com/opentap/opentap/issues/1452)
- Add documentation about installing on Mac [#1151](https://github.com/opentap/opentap/issues/1151)

Cleanup 
 ------- 
- Reduce the number of warnings during CI builds [#1502](https://github.com/opentap/opentap/issues/1502)
- Clean up unused code from "ReflectionHelper" [#1488](https://github.com/opentap/opentap/issues/1488)
- MixinBuilder ToDynamicMember is called multiple times [#1483](https://github.com/opentap/opentap/issues/1483)
- TestPlanRun performance improvements [#1459](https://github.com/opentap/opentap/issues/1459)
- Update SmartInstaller to the newest version [#1454](https://github.com/opentap/opentap/issues/1454)
- Fix GitHub warnings [#1440](https://github.com/opentap/opentap/issues/1440)
- Clean up IEEE Block related code in ScpiInstrument [#1415](https://github.com/opentap/opentap/issues/1415)

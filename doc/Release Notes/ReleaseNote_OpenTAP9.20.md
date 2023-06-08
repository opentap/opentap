Release Notes - opentap 9.20 
 ============= 

New Features 
 ------- 

- Feature: PrepareUninstall package action verb [#957](https://github.com/opentap/opentap/issues/957)
- Add iteration functionality to SweepParameterRangeStep [#902](https://github.com/opentap/opentap/issues/902)
- Ability to override *CLS from ScpiInstrument Open [#872](https://github.com/opentap/opentap/issues/872)
- Add support for PyVISA-py [#863](https://github.com/opentap/opentap/issues/863)
- Add a way for a serializer plugin to dynamically specify if it is needed in deserialization [#1006](https://github.com/opentap/opentap/issues/1006)


Bug Fixes 
 ------- 

- Moving steps may cause Inputs to become corrupted [#1018](https://github.com/opentap/opentap/issues/1018)
- Newtonsoft missing on Linux since 9.20.0-beta.21 [#1012](https://github.com/opentap/opentap/issues/1012)
- Memory Leak : if PluginManager.SearchAsync()  called repeatedly  [#970](https://github.com/opentap/opentap/issues/970)
- Process Isolation can cause tap.exe to hang forever [#968](https://github.com/opentap/opentap/issues/968)
- tap image install without an image specified causes it to install a 'null' image, deleting everything. [#967](https://github.com/opentap/opentap/issues/967)
- Process isolation issues on Mac and Linux [#960](https://github.com/opentap/opentap/issues/960)
- Set MacroString.Context on deserialization [#931](https://github.com/opentap/opentap/issues/931)
- OpenTAP build sometimes produces invalid package XML [#903](https://github.com/opentap/opentap/issues/903)
- Parameterized `Verdict Of` is a field not a dropdown [#900](https://github.com/opentap/opentap/issues/900)
- StepNameAnnotation.Value on TestStep[] throws an exception [#898](https://github.com/opentap/opentap/issues/898)
- doc mentions SettingsRetrieval.cs, but it seems to have been renamed... [#862](https://github.com/opentap/opentap/issues/862)
- null reference during 'tap package create' [#858](https://github.com/opentap/opentap/issues/858)
- Dockerfile Base image is Focal , but Docker Image tag is Bionic [#850](https://github.com/opentap/opentap/issues/850)
- SDK Examples build error due to CE EULA pop-up [#848](https://github.com/opentap/opentap/issues/848)
- Package create allows non-semantic versions [#815](https://github.com/opentap/opentap/issues/815)
- 872 Scpi Instrument Virtual Methods: Fix [#895](https://github.com/opentap/opentap/pull/895)


Usability Improvements 
 ------- 

- ComponentSettings.SetCurrent does not return errors [#1000](https://github.com/opentap/opentap/issues/1000)
- Break condition value is reset when the enabled checkmark is toggled  [#935](https://github.com/opentap/opentap/issues/935)
- Let the user know which other version(s) of a package is available in case a release does not exist [#922](https://github.com/opentap/opentap/issues/922)
- Installing a package that does not exist as a release version is not straight forward [#920](https://github.com/opentap/opentap/issues/920)
- Dont allow parameterizing a swept property [#856](https://github.com/opentap/opentap/issues/856)
- 858 improve error message for invalid version specs [#859](https://github.com/opentap/opentap/pull/859)


Documentation 
 ------- 

- Improve release-note generation [#543](https://github.com/opentap/opentap/issues/543)


Other 
 ------- 

- Engine UnitTests diabled in builds [#1003](https://github.com/opentap/opentap/issues/1003)
- SDK Examples depends on the Editor CE package [#983](https://github.com/opentap/opentap/issues/983)
- 856 Disallow Parameterizing Swept Property: Fix [#857](https://github.com/opentap/opentap/pull/857)
- 815 Require Semantic versioned packages: Fix [#816](https://github.com/opentap/opentap/pull/816)
- Create dotnet templates from SDK templates [#505](https://github.com/opentap/opentap/issues/505)

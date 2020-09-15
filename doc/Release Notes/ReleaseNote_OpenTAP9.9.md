Release Notes  - OpenTAP 9.9
=============

New Features:
-------

- Add support for promptUser in `MetaData` in ComponentSettings [#170](https://gitlab.com/OpenTAP/opentap/issues/170)
- The `tap package show` CLI action should display the latest compatible release version [#184](https://gitlab.com/OpenTAP/opentap/issues/184)
- Add globbing attribute to OpenTapPackageReference [#209](https://gitlab.com/OpenTAP/opentap/issues/209)
- Package Create: Less greedy package dependency detection [#264](https://gitlab.com/OpenTAP/opentap/issues/264)
- Added cancellation token to `Run Program` test step [#270](https://gitlab.com/OpenTAP/opentap/issues/270)
- Add tags support in TapPackage metadata [#275](https://gitlab.com/OpenTAP/opentap/issues/275)
- Allow a TestPlan object to be annotated [#302](https://gitlab.com/OpenTAP/opentap/issues/302)

Usability Improvements:
-------

- Obsolete ResultListenerIgnoreAttribute [#29](https://gitlab.com/OpenTAP/opentap/issues/29)
- Remove .NET Core 2.1.503 from Linux docker image [#36](https://gitlab.com/OpenTAP/opentap/issues/36)
- Issues passing external parameter related to a dropdown menu via CLI [#176](https://gitlab.com/OpenTAP/opentap/issues/176)
- Test run execution gives success message even if the test plan depends on a plugin that is not installed [#205](https://gitlab.com/OpenTAP/opentap/issues/205)
- Add INSTALL.sh test to CI [#207](https://gitlab.com/OpenTAP/opentap/issues/207)
- The folder structure created by `tap sdk new` is problematic [#223](https://gitlab.com/OpenTAP/opentap/issues/223)
- Sweep parameter step to show name of the parameter(s) being swept [#265](https://gitlab.com/OpenTAP/opentap/issues/265)
- Support Http Range Headers to resume failed package download [#257](https://gitlab.com/OpenTAP/opentap/issues/257)
- Improve performance with regards to running very quick test plans [#272](https://gitlab.com/OpenTAP/opentap/issues/272)
- Better handling of assembly version conflicts between packages' DLL dependencies [#292](https://gitlab.com/OpenTAP/opentap/issues/292)
- Uninformative error message when instrument/dut driver is not configured [#314](https://gitlab.com/OpenTAP/opentap/issues/314)
- Set TapMutex when running a CLI action [#317](https://gitlab.com/OpenTAP/opentap/issues/317)

Bug Fixes:
-------

- Handle input step (Examples): Dynamic Input Value property is not displayed correctly [#162](https://gitlab.com/OpenTAP/opentap/issues/162)
- tap.exe: Nothing happens after "waiting for files to become unlocked" message when files become unlocked [#169](https://gitlab.com/OpenTAP/opentap/issues/169)
- SDK has two OpenTAP dependencies [#180](https://gitlab.com/OpenTAP/opentap/issues/180)
- CLI process does not get a chance to finish [#262](https://gitlab.com/OpenTAP/opentap/issues/262)
- Several issues with `tap sdk gitversion` [#263](https://gitlab.com/OpenTAP/opentap/issues/263)
- Sweep: Removed step (and parameter) are still displayed in the log when executed [#268](https://gitlab.com/OpenTAP/opentap/issues/268)
- Cannot locate System.Net.IPAddress type [#277](https://gitlab.com/OpenTAP/opentap/issues/277)
- Package install overwrites existing files [#281](https://gitlab.com/OpenTAP/opentap/issues/281)
- `Deferred Results` step's abort behavior: abort does not wait for `Deferred Results`, and there is no indication about abort at all [#288](https://gitlab.com/OpenTAP/opentap/issues/288)
- ComponentSettings don't work reliable [#289](https://gitlab.com/OpenTAP/opentap/issues/289)
- The `Run Program` step's PrePlanRun should not check if the working directory exists [#297](https://gitlab.com/OpenTAP/opentap/issues/297)
- TestPlanReference load sometimes does not update [#298](https://gitlab.com/OpenTAP/opentap/issues/298)
- ComponentSettingsList does not use DisplayAttribute name [#301](https://gitlab.com/OpenTAP/opentap/issues/301)
- `tap package verify` fails on Linux for OpenTAP and SDK [#303](https://gitlab.com/OpenTAP/opentap/issues/303)
- SDK/Examples should depend on OpenTAP 9.8 [#305](https://gitlab.com/OpenTAP/opentap/issues/305)
- Creating new project using CLI has old NuGet version [#307](https://gitlab.com/OpenTAP/opentap/issues/307)
- External parameters from a .csv file can only be used if the CSV plugin is installed [#311](https://gitlab.com/OpenTAP/opentap/issues/311)

Documentation:
-------

- Ensure docs are using https instead of http where applicable [#117](https://gitlab.com/OpenTAP/opentap/issues/117)
- Document  OpenTAP annotation system [#202](https://gitlab.com/OpenTAP/opentap/issues/202)
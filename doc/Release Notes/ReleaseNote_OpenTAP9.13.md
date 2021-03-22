Release Notes  - OpenTAP 9.13
=============

New Features:
-------

- `OpenTapPackageReference` without `InstallActions` [#274](https://gitlab.com/OpenTAP/opentap/issues/274)
- Sub Test Plan Execution [#472](https://gitlab.com/OpenTAP/opentap/issues/472)
- Support for merging related numeric types in test plan parameters [#497](https://gitlab.com/OpenTAP/opentap/issues/497)
- Need better error logging for `Resource.Open` [#515](https://gitlab.com/OpenTAP/opentap/issues/515)
- Enum `AvailableValues` `DisplayAttributes` are missing `Order` [#528](https://gitlab.com/OpenTAP/opentap/issues/528)
- Improve outputs by adding to `OutputAttribute` [#529](https://gitlab.com/OpenTAP/opentap/issues/529)
- Look for `OPENTAP_COLOR` variable to enable colors without having to use `--color` on every command [#544](https://gitlab.com/OpenTAP/opentap/issues/544)
- Error during `ResultListener.OnTestPlanComplete` should cause an Error verdict [#550](https://gitlab.com/OpenTAP/opentap/issues/550)
- Offer API to disable session-logs being written to text files [#563](https://gitlab.com/OpenTAP/opentap/issues/563)
- Add `UserInput` to uninstall CLI action [#583](https://gitlab.com/OpenTAP/opentap/issues/583)
- Descriptions for test steps and test plans[#593](https://gitlab.com/OpenTAP/opentap/issues/593)


Usability Improvements: 
-------

- Deprecate SDK TestPlan generation and refer to Editors instead. [#181](https://gitlab.com/OpenTAP/opentap/issues/181)
- Add "SourceLicense" to PackageDef [#479](https://gitlab.com/OpenTAP/opentap/issues/479)
- SDK `new cliaction` creates files without a project [#493](https://gitlab.com/OpenTAP/opentap/issues/493)
- `TypeData.GetTypeData` should never return null [#499](https://gitlab.com/OpenTAP/opentap/issues/499)
- Package Manager to show if a package is installed system-wide [#512](https://gitlab.com/OpenTAP/opentap/issues/512)
- Objects that cannot be cloned might be marked as cloneable [#522](https://gitlab.com/OpenTAP/opentap/issues/522)
- Reduce Log Severity of parameterization changes from Warning to Info [#527](https://gitlab.com/OpenTAP/opentap/issues/527)
- Parameterizing shows the wrong name [#530](https://gitlab.com/OpenTAP/opentap/issues/530)
- Improve build error messages [#531](https://gitlab.com/OpenTAP/opentap/issues/531)
- CLI Action constructor exceptions gets printed in a strange way. [#532](https://gitlab.com/OpenTAP/opentap/issues/532)
- Support `--color` in `tap package install` command [#543](https://gitlab.com/OpenTAP/opentap/issues/543)
- Provide delegate for subscribing to package download progress [#547](https://gitlab.com/OpenTAP/opentap/issues/547)
- Reduce slim OpenTAP docker image size by 20% by removing apt leftovers [#588](https://gitlab.com/OpenTAP/opentap/issues/588)
- Reflect whether the user cancelled an uninstall action using exit codes [#590](https://gitlab.com/OpenTAP/opentap/issues/590)
- Avoid using negative integer exit codes [#596](https://gitlab.com/OpenTAP/opentap/issues/596)
- Add `IStringReadOnlyValueAnnotation` to `MultiResourceSelector` annotation [#599](https://gitlab.com/OpenTAP/opentap/issues/599)
- Install / uninstall UserRequest dialogues are inconsistent and unnecessarily cluttered [#600](https://gitlab.com/OpenTAP/opentap/issues/600)

Bug Fixes: 
-------

- Dependency resolution does not look at patch version [#356](https://gitlab.com/OpenTAP/opentap/issues/356)
- OpenTAP Nuget 32 bit project support [#498](https://gitlab.com/OpenTAP/opentap/issues/498)
- Caught error during `UserRequest` when running an invalid test plan and doing CTRL+C [#557](https://gitlab.com/OpenTAP/opentap/issues/557)
- Duplicate metadata prompt for `ComponentSettings<OperationalParameterSettings>` properties [#558](https://gitlab.com/OpenTAP/opentap/issues/558)
- `OpenTapPackageReference` only works the first time on Linux [#567](https://gitlab.com/OpenTAP/opentap/issues/567)
- Uninstalling multiple packages with `force` will fail if a single file is locked [#591](https://gitlab.com/OpenTAP/opentap/issues/591)
- `ITypeDataSearcher` types not used properly [#595](https://gitlab.com/OpenTAP/opentap/issues/595)
- Builds fail when the environment variable `OPENTAP_DEBUG_INSTALL` is already set [#589](https://gitlab.com/OpenTAP/opentap/issues/598)

Documentation:
-------

- Create example of adding new `IconAnnotations` [#535](https://gitlab.com/OpenTAP/opentap/issues/535)
- Document WinForms thread behavior [#551](https://gitlab.com/OpenTAP/opentap/issues/551)
- ResultViewer custom panel example [#552](https://gitlab.com/OpenTAP/opentap/issues/552)
- SDK examples use TAP_PATH [#556](https://gitlab.com/OpenTAP/opentap/issues/556)
- Typo in "Check for Updates at Startup" setting tooltip [#566](https://gitlab.com/OpenTAP/opentap/issues/566)
- Document `PackageActionExtension` in the developer guide [#572](https://gitlab.com/OpenTAP/opentap/issues/572)
- User guide "developer system" links to OpenTAP download [#575](https://gitlab.com/OpenTAP/opentap/issues/575)

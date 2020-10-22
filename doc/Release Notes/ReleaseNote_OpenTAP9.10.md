Release Notes  - OpenTAP 9.10
=============

New Features:
-------

- Allow more flexibility in the `ValidatingObject` API [#46](https://gitlab.com/OpenTAP/opentap/issues/46)
- Package unpack: hide files starting with a '.' [#168](https://gitlab.com/OpenTAP/opentap/issues/168)
- Better `DefaultValueAttribute` support in serializer [#216](https://gitlab.com/OpenTAP/opentap/issues/216)
- Add OpenTapPackage reference from command line [#294](https://gitlab.com/OpenTAP/opentap/issues/294)
- `--out` argument for `tap package download` [#379](https://gitlab.com/OpenTAP/opentap/issues/379)
- Option for `tap package install` to only install if a newer version is not already installed [#391](https://gitlab.com/OpenTAP/opentap/issues/391)
- Package install/uninstall: 2 min timeout for waiting for files to become unlocked [#414](ttps://gitlab.com/OpenTAP/opentap/issues/414)

Usability Improvements:
-------

- OpenTAP does not have an icon in Control Panel [#132](https://gitlab.com/OpenTAP/opentap/issues/132)
- Support that all settings can be an "Input" (not only IInput<> types) [#279](https://gitlab.com/OpenTAP/opentap/issues/279)
- IMenuAnnotation - For inputs and parameters [#280](https://gitlab.com/OpenTAP/opentap/issues/280)
- Misleading error message when package is not compatible [#334](https://gitlab.com/OpenTAP/opentap/issues/334)
- PluginManager.Search Optimization [#348](https://gitlab.com/OpenTAP/opentap/issues/348)
- The CLI help shows string array arguments as optional [#355](https://gitlab.com/OpenTAP/opentap/issues/355)
- Add SDK/Example of OpenTAP Serializer plugin [#360](https://gitlab.com/OpenTAP/opentap/issues/360)
- Inherit the log source and log type from subprocesses [#371](https://gitlab.com/OpenTAP/opentap/issues/371)
- `tap package download`/`install` should use the version specified and return error if not found [#380](https://gitlab.com/OpenTAP/opentap/issues/380)
- NuGet msbuild script should not install TapPackages with `--force` [#405](https://gitlab.com/OpenTAP/opentap/issues/405)

Bug Fixes:
-------

- `tap package create`/`verify` should automagically make the paths in the package definition file Linux friendly [#327](https://gitlab.com/OpenTAP/opentap/issues/327)
- `MenuAnnotations` not updated on a parameterized property [#336](https://gitlab.com/OpenTAP/opentap/issues/336)
- `tap package download` downloads the wrong version [#341](https://gitlab.com/OpenTAP/opentap/issues/341)
- Test Plan Reference can in some cases run even though no test plan is loaded [#346](https://gitlab.com/OpenTAP/opentap/issues/346)
- Unexpected behavior when rebuilding projects with OpenTapPackageReference [#368](https://gitlab.com/OpenTAP/opentap/issues/368)
- Memory leak when running test plans with Test Plan Settings Parameters [#369](https://gitlab.com/OpenTAP/opentap/issues/369)
- `tap sdk new integration vscode` creates invalid path [#373](https://gitlab.com/OpenTAP/opentap/issues/373)
- `MenuAnnotations` missing for "Break Conditions" property [#376](https://gitlab.com/OpenTAP/opentap/issues/376)
- ProjectBuildTest unit tests broken on Linux [#381](https://gitlab.com/OpenTAP/opentap/issues/381)
- ExternalParameter attr. applied on parameter in an embedded property does not show Display name on the GUI [#382](https://gitlab.com/OpenTAP/opentap/issues/382)
- Builds does not pass on Linux (OpenTap.Package.UnitTests testing fails) [#384](https://gitlab.com/OpenTAP/opentap/issues/384)
- FilePackageRepository fails if it can't access a subdirectory [#390](https://gitlab.com/OpenTAP/opentap/issues/390)
- Multi-select + EditParameters does not work as expected [#398](https://gitlab.com/OpenTAP/opentap/issues/398)
- Paths in `<PackageActionExtensions>` should be relative to the `--target` directory, and not the temp install dir [#409](https://gitlab.com/OpenTAP/opentap/issues/409)

Documentation:
-------

- Document `sdk new` cli commands [#183](https://gitlab.com/OpenTAP/opentap/issues/183)
- Update docs to cover how resources and test steps interact with Open() and Close() methods [#221](https://gitlab.com/OpenTAP/opentap/issues/221)
- Document API Reference for OpenTAP.Package.dll on doc.opentap.io [#234](https://gitlab.com/OpenTAP/opentap/issues/234)
- Create SDK code example of complex settings data scenarios [#239](https://gitlab.com/OpenTAP/opentap/issues/239)
- Improve the documentation on `tap package` and `tap run` [#278](https://gitlab.com/OpenTAP/opentap/issues/278)
- "SDK Templates" documentation formatting broken [#395](https://gitlab.com/OpenTAP/opentap/issues/395)
- Clean-up in SDK/Examples section of DevGuide [#389](https://gitlab.com/OpenTAP/opentap/issues/389)
- "Instrument Plugin Development" dead link [#394](https://gitlab.com/OpenTAP/opentap/issues/394)

Testing:
-------

- Add UnitTest to verify the functionality of XmlTextAttribute [#51](https://gitlab.com/OpenTAP/opentap/issues/51)
- Make NUnit3 tests work on Linux [#353](https://gitlab.com/OpenTAP/opentap/issues/353)

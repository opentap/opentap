Release Notes - OpenTAP 9.3
=============

New Features:
-------

- Added a "continue" loop functionality.
- Added multi-select support in AvailableValues.
- SDK now includes Cli tools to generate OpenTAP templates [#16](https://gitlab.com/OpenTAP/opentap/issues/16).

Usability Improvements: 
-------

- Remove Connection now triggers `NotifyCollectionChangedAction.Remove`.
- Package list now indicates new version and beta status of available updates in the CLI [#10](https://gitlab.com/OpenTAP/opentap/issues/10).
- Removed duplicates from the output of `tap package list` [#11](https://gitlab.com/OpenTAP/opentap/issues/11).

Bug Fixes: 
-------

- `List<Resource>` no longer ignoring AvailableValues attribute.
- Fixed issue with sweep loop value.
- Abort verdict for steps does not influence other executions anymore.
- Fixed issue with `tap package install` with no packages specified.
- Fixed a bug in the implementation of == operator in the `Input<T>` class.
- SweepLoop can now configure values when 'Select All' is selected.
- Fixed SweepLoop issue: Index was outside the bounds of the array.
- SimpleTapAssemblyResolver no longer throws exception.
- Loading/Saving xml no longer changes the dependency list.
- Input no longer causes error during Annotation.
- Dropdown is now populated when using Available Values more than once with `List<string>`.
- Removed LoadInSeparateAppDomain from MSBuild task.
- Fixed `package list` argument support
- `tap package install` no longer defaults to downloading `Any` version instead of released versions [#12](https://gitlab.com/OpenTAP/opentap/issues/12).
- Unit test build jobs no longer fail silently [#14](https://gitlab.com/OpenTAP/opentap/issues/14).

Other: 
-------

- Improved log rotation.
- Created build to publish NuGet package to NuGet.org.
- Cleaned up build on gitlab.com/opentap/opentap.
- Added CliAction support for display groups.
- Implemented a warning message when unsintalling OpenTap through the CLI
- Migrated Linux SDK generate tool to OpenTAP [#16](https://gitlab.com/OpenTAP/opentap/issues/16).
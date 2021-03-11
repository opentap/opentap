Release Notes  - OpenTAP 9.12
=============

New Features:
-------

- Change `BreakConditionProperty`'s visibility from internal to public [#374](https://gitlab.com/OpenTAP/opentap/issues/374)
- Expose more information about properties in `AnnotationCollection` [#464](https://gitlab.com/OpenTAP/opentap/issues/464)
- Support parameterizing lists [#469](https://gitlab.com/OpenTAP/opentap/issues/469)
- Track package installation count [#490](https://gitlab.com/OpenTAP/opentap/issues/490)


Usability Improvements:
-------

- `IDisplayName` annotation [#286](https://gitlab.com/OpenTAP/opentap/issues/286)
- FlagsEnum to handle zero (0) value specially [#293](https://gitlab.com/OpenTAP/opentap/issues/293)
- Read-Only List Example: Serializer warnings when copy pasting the step [#425](https://gitlab.com/OpenTAP/opentap/issues/425)
- Make `opentap.targets` use default parameters for repository [#431](https://gitlab.com/OpenTAP/opentap/issues/431)
- `OpenTAP.Diagnostic.Event.DurationNS` returns 0 on `OpenTAP.ILogListener` events [#458](https://gitlab.com/OpenTAP/opentap/issues/458)
- `TestStep.GetObjectSettings` performance issue [#467](https://gitlab.com/OpenTAP/opentap/issues/467)
- Connection name is missing in log panel [#468](https://gitlab.com/OpenTAP/opentap/issues/468)
- `tap package install`: The actions and messages related to `--interactive` are not logged [#504](https://gitlab.com/OpenTAP/opentap/issues/504)
- `tap package uninstall`: Extra messages when uninstalling a package which is a dependency to other packages [#505](https://gitlab.com/OpenTAP/opentap/issues/505)
- `tap package download` should not give messages about dependencies not being installed [#506](https://gitlab.com/OpenTAP/opentap/issues/506)
- `TestStep.GetParent<T>` T should be allowed to be any type [#517](https://gitlab.com/OpenTAP/opentap/issues/517)
- `DefaultDisplayAttribute` is needed to be backwards compatible with `UserInputRequest` [#521](https://gitlab.com/OpenTAP/opentap/issues/521)

Bug Fixes:
-------

- `tap package create`: Cannot resolve OpenTap dependency if manually specified and not running from the OpenTAP folder [#366](https://gitlab.com/OpenTAP/opentap/issues/366)
- `ProjectBuildTest` unit tests unstable on Linux [#386](https://gitlab.com/OpenTAP/opentap/issues/386)
- New exception thrown when loading a recursive test plan in Test Plan Reference [#448](https://gitlab.com/OpenTAP/opentap/issues/448)
- Issues building package when referencing other OpenTAP Package [#457](https://gitlab.com/OpenTAP/opentap/issues/457)
- Sweep Parameter: Removed parameter still displayed in log [#466](https://gitlab.com/OpenTAP/opentap/issues/466)
- `Input<string>` and `Input<Object>` cannot co-exists in the package [#473](https://gitlab.com/OpenTAP/opentap/issues/473)
- TestPlanReference EnabledIf on Resource property [#474](https://gitlab.com/OpenTAP/opentap/issues/474)
- Race condition when doing defer [#483](https://gitlab.com/OpenTAP/opentap/issues/483)
- Dynamic step name for a setting with assigned output uses value from previous run not current run [#494](https://gitlab.com/OpenTAP/opentap/issues/494)
- Unhandled GUI Error : `ScopeMember.getCommonParents` [#524](https://gitlab.com/OpenTAP/opentap/issues/524)

Documentation: 
-------

- Align OpenTap marketplace VS requirements with OpenTap developer guide VS requirements [#456](https://gitlab.com/OpenTAP/opentap/issues/456)

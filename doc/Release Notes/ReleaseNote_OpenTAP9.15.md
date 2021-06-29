Release Notes  - OpenTAP 9.15
=============

New Features:
-------

- Read-Only List unable to copy if  property setter is private [#562](https://gitlab.com/OpenTAP/opentap/issues/562)
- Aliases for  ports and via points [#568](https://gitlab.com/OpenTAP/opentap/issues/568)
- UserInputs are unable to be mapped to the OpenTAP Session that triggered it [#621](https://gitlab.com/OpenTAP/opentap/issues/621)
- PackageDependencySerializerPlugin to detect files coming from packages and add dependency [#627](https://gitlab.com/OpenTAP/opentap/issues/627)
- TapPackage should support `/` in the package name [#641](https://gitlab.com/OpenTAP/opentap/issues/641)


Usability Improvements: 
-------

- Subprocesses run update checks [#650](https://gitlab.com/OpenTAP/opentap/issues/650)
- Nuget build tasks run update checks [#652](https://gitlab.com/OpenTAP/opentap/issues/652)
- Description and Break Conditions saved as metadata [#654](https://gitlab.com/OpenTAP/opentap/issues/654)
- Provide a waning when `ValidationRuleCollection.Add` is used wrong [#667](https://gitlab.com/OpenTAP/opentap/issues/667)
- Restore `UserInput.Interface` after running CLI actions non-interactive [#673](https://gitlab.com/OpenTAP/opentap/issues/673)


Bug Fixes: 
-------

- Sweeping resources does not work correctly [#664](https://gitlab.com/OpenTAP/opentap/issues/664)
- Flow Control \ If Verdict step gets stuck when it is a child step under Flow Control \ Parallel [#666](https://gitlab.com/OpenTAP/opentap/issues/666)
- Uninstalling a plugin with dependencies, text is not arranged correctly [#669](https://gitlab.com/OpenTAP/opentap/issues/669)
- ResultParameter MetaData name confusion [#677](https://gitlab.com/OpenTAP/opentap/issues/677)
- ResultParameter constructor generate invalid results for duplicate keys [#681](https://gitlab.com/OpenTAP/opentap/issues/681)
- `OpenTap.targets` copies a 64-bit package.xml to 32-bit builds [#686](https://gitlab.com/OpenTAP/opentap/issues/686)
- Incompatible ResultVector types introduced in 9.13 [#688](https://gitlab.com/OpenTAP/opentap/issues/688)

Documentation:
------

- Add Example for 'Stacked Resources' / Resource Topologies [#668](https://gitlab.com/OpenTAP/opentap/issues/668)
- Dynamic Attached Property Example [#675](https://gitlab.com/OpenTAP/opentap/issues/675)
- Update copyright information of API documentation [#680](https://gitlab.com/OpenTAP/opentap/issues/680)
- Update copyright 2021 in license files [#682](https://gitlab.com/OpenTAP/opentap/issues/682)
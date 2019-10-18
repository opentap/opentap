Release Notes - OpenTAP 9.4
=============

New Features:
-------

- Added `UnsweepableAttribute`
- Allow ICliActions in plugins to run isolated - Implemented `IsolatedPackageAction` [#25](https://gitlab.com/OpenTAP/opentap/issues/25)

Usability Improvements:
-------

- Run Program step sets verdict to Aborted on timeout [#28](https://gitlab.com/OpenTAP/opentap/issues/28)


Bug Fixes: 
-------

- Fixed issue with Linux install.
- Fixed an issue in `TestPlanRun` that caused a runtime error.
- Test plan Hash now gets updated between runs with open resources.

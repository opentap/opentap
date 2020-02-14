Release Notes - OpenTAP 9.6
=============

New Features:
-------

- Abort based on verdicts to be decided from the test step [#24](https://gitlab.com/OpenTAP/opentap/issues/24)
- ScpiInstrument Error Checking using viReadSTB [#100](https://gitlab.com/OpenTAP/opentap/issues/100)
- Build Debian packages in CI [#101](https://gitlab.com/OpenTAP/opentap/issues/101)
- `tap package install` should check for a local file before using the PackageCache [#108](https://gitlab.com/OpenTAP/opentap/issues/108)


Usability Improvements: 
-------

- Too many switch positions in code example [#4](https://gitlab.com/OpenTAP/opentap/issues/4)
- SDK Package Linux Support [#105](https://gitlab.com/OpenTAP/opentap/issues/105)
- SDK Examples: In Browsable Attribute Example step, the field should be ReadOnly instead of ReadyOnly [#119](https://gitlab.com/OpenTAP/opentap/issues/119)


Bug Fixes: 
-------

- DUT ID parameter meta data prompt changes are not set before OnTestPlanRunStart(TestPlanRun planRun) is called. [#35](https://gitlab.com/OpenTAP/opentap/issues/35)
- Errors produced from TypeDataProviderStack [#96](https://gitlab.com/OpenTAP/opentap/issues/96)
- Embedded Properties don't get the right group name [#97](https://gitlab.com/OpenTAP/opentap/issues/97)
- ValidatingObject throws exception when used with ExternalParameters in special cases [#98](https://gitlab.com/OpenTAP/opentap/issues/98)
- Set OutputType=Exe for SDK TestPlanExecution Examples [#99](https://gitlab.com/OpenTAP/opentap/issues/99)
- Creating of package fails if Version attribute is left out from package.xml [#102](https://gitlab.com/OpenTAP/opentap/issues/102)
- package create wildcards does not work with package checksum [#106](https://gitlab.com/OpenTAP/opentap/issues/106)
- Incorrect ScalarResultSink documentation [#114](https://gitlab.com/OpenTAP/opentap/issues/114)
- TypeData.Display is not loaded for non-ITapPlugin types [#122](https://gitlab.com/OpenTAP/opentap/issues/122)
- `tap package list <package> --version x.y.z` does not work [#128](https://gitlab.com/OpenTAP/opentap/issues/128)
- IStringConvertProvider should only generate two-way convertible strings [#131](https://gitlab.com/OpenTAP/opentap/issues/131)

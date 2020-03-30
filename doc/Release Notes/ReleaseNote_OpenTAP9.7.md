Release Notes - OpenTAP 9.7
=============

New Features:
-------

- Slimmer runtime docker image [#13](https://gitlab.com/OpenTAP/opentap/-/issues/13)
- CLI option: tap package show [#107](https://gitlab.com/OpenTAP/opentap/issues/107)
- {MyField} curly brace templating in step name for Enum values. [#120](https://gitlab.com/OpenTAP/opentap/issues/120)
- Support a Retry in Repeat step that does not affect Test Plan Verdict [#141](https://gitlab.com/OpenTAP/opentap/issues/141)

Usability Improvements:
-------

- `libc6-dev` is a prerequisite to run OpenTAP on Linux [#66](https://gitlab.com/OpenTAP/opentap/issues/66)
- Add description to TestStep.Name and TestStep.Enabled [#103](https://gitlab.com/OpenTAP/opentap/issues/103)
- Building examples leads to "Multiple assemblies of different versions" message when running tap.exe [#116](https://gitlab.com/OpenTAP/opentap/issues/116)
- Result Listener generated from `tap sdk new resultlistener` does not have a name [#123](https://gitlab.com/OpenTAP/opentap/issues/123)
- Add a Rule to the SCPI step to validate a command with a "?" is paired with the Query action [#134](https://gitlab.com/OpenTAP/opentap/issues/134)
- Improved SCPI Instrument Settings [#165](https://gitlab.com/OpenTAP/opentap/issues/165)

Bug Fixes:
-------

- Rule validation not enforced for test plan meta data prompt. [#30](https://gitlab.com/OpenTAP/opentap/issues/30)
- DUT ID parameter meta data prompt changes are not set before OnTestPlanRunStart(TestPlanRun planRun) is called. [#35](https://gitlab.com/OpenTAP/opentap/issues/35)
- Missing dependency DLL on Windows after building plugin package [#70](https://gitlab.com/OpenTAP/opentap/issues/70)
- Deleting rows from Sweep Loop always causes the last row to be deleted [#112](https://gitlab.com/OpenTAP/opentap/issues/112)
- Test Plan Reference does not get a `IStringReadOnlyValueAnnotation` [#115](https://gitlab.com/OpenTAP/opentap/issues/115)
- `EnabledIfAttribute` not working on GUI Button [#138](https://gitlab.com/OpenTAP/opentap/issues/138)
- `Enabled<T>` not working with SweepLoop [#144](https://gitlab.com/OpenTAP/opentap/issues/144)
- Unhandled exception in Sweep Loop when updating parameters on multiple sweep loops at the same time [#148](https://gitlab.com/OpenTAP/opentap/issues/148)
- Break conditions - different behavior for same setting If Verdict vs Time Guard [#153](https://gitlab.com/OpenTAP/opentap/issues/153)
- Exception: Index was outside the bounds of the array. [#156](https://gitlab.com/OpenTAP/opentap/issues/156)
- Git version warning on first commit [#158](https://gitlab.com/OpenTAP/opentap/issues/158)
- `tap sdk gitversion` to work on Linux. **Minor compatible change** libcurl3 is no longer supported for git integration, instead use libcurl4. This change was made to avoid using an obsolete libcurl that could not be installed alongside libcurl4.
 [#159](https://gitlab.com/OpenTAP/opentap/issues/159)
- TestStep name annotation no longer uses `GetFormattedName` [#160](https://gitlab.com/OpenTAP/opentap/issues/160)

Documentation: 
-------
- Developer Guide: package.xml documentation lacking [#67](https://gitlab.com/OpenTAP/opentap/issues/67)
- API Reference page: docs.opentap.io/api [#146](https://gitlab.com/OpenTAP/opentap/issues/146)
- Document "Break Conditions" behavior in user documentation [#163](https://gitlab.com/OpenTAP/opentap/-/issues/163)
- CLI reference commands missing (documentation) [#166](https://gitlab.com/OpenTAP/opentap/issues/166)
- Pages for chapters are weird on doc.opentap.io [#171](https://gitlab.com/OpenTAP/opentap/issues/171)

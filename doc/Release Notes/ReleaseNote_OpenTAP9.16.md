Release Notes  - OpenTAP 9.16
=============

New Features:
-------

- Sweep Parameter Range / Step Size Needs to be Parameterizable [#671](https://gitlab.com/OpenTAP/opentap/issues/671)
- Place Built-In Connection Types in their Own Group [#678](https://gitlab.com/OpenTAP/opentap/issues/678)
- Allow Addition of Pictures to Test Steps [#689](https://gitlab.com/OpenTAP/opentap/issues/689)
- Add Support for Including Dependencies from .xml Files in package.xml [#724](https://gitlab.com/OpenTAP/opentap/issues/724)
- Implement Image Functionality [#732](https://gitlab.com/OpenTAP/opentap/issues/732)
- Test Plan Package as Test Plan Run Parameter [#745](https://gitlab.com/OpenTAP/opentap/issues/745)
- Support a list of expected exit codes for action steps [#778](https://gitlab.com/OpenTAP/opentap/issues/778)


Usability Improvements: 
-------

- EmbeddedProperties Gets an Empty Group and is Misaligned With Other Properties [#715](https://gitlab.com/OpenTAP/opentap/issues/715)
- Add Bench Setting for Including Prereleases in Package Actions [#723](https://gitlab.com/OpenTAP/opentap/issues/723)
- Make TestPlan Path Available to Result Listeners [#738](https://gitlab.com/OpenTAP/opentap/issues/738)
- Generate <OpenTapPackageReference> references earlier [#742](https://gitlab.com/OpenTAP/opentap/issues/742)
- Confusing Error When 'tap sdk gitversion' Fails [#744](https://gitlab.com/OpenTAP/opentap/issues/744)
- `tap package install --version` Considers Packages Differing Only for the Build Metadata Identical [#761](https://gitlab.com/OpenTAP/opentap/issues/761)


Bug Fixes: 
-------

- Sweep Parameter: Object of Type 'System.Object' Cannot be Converted to Type 'System.String' [#696](https://gitlab.com/OpenTAP/opentap/issues/696)
- Unhandled GUI Error when Changing Parameter Scope [#698](https://gitlab.com/OpenTAP/opentap/issues/698)
- The Plugin Manager Sometimes Loads DLLs too Eagerly [#700](https://gitlab.com/OpenTAP/opentap/issues/700)
- tap.sh Launch Script Does Not Work on Mac [#702](https://gitlab.com/OpenTAP/opentap/issues/702)
- Error When Loading a Test Plan with a "Dynamic Step Example" Step [#707](https://gitlab.com/OpenTAP/opentap/issues/707)
- Suggested Value Annotations Not Updating [#708](https://gitlab.com/OpenTAP/opentap/issues/708)
- Test Step Description Showing Random String in Package Manager [#722](https://gitlab.com/OpenTAP/opentap/issues/722)
- Exception Thrown Inside MultiResourceSelector [#729](https://gitlab.com/OpenTAP/opentap/issues/729)
- Large Numbers Cause OpenTAP to Halt or Crash [#731](https://gitlab.com/OpenTAP/opentap/issues/731)
- Resources Cannot be set Using IStringValueAnnotations [#750](https://gitlab.com/OpenTAP/opentap/issues/750)
- Aborting a Test Plan Execution Returns a “Caught unhandled GUI error” [#752](https://gitlab.com/OpenTAP/opentap/issues/752)
- Commit 4dd2f869741af7b239fdc404dacc60926bdad1c0 Overloads `tap sdk gitversion --log` [#753](https://gitlab.com/OpenTAP/opentap/issues/753)
- GetPackageContainingFile Throws if Argument is not a File Path [#758](https://gitlab.com/OpenTAP/opentap/issues/758)
- Installing Editor 9.15.2 Throws XML Exceptions [#760](https://gitlab.com/OpenTAP/opentap/issues/760)
- Tap Run Test Plan Memory Usage [#767](https://gitlab.com/OpenTAP/opentap/issues/767)


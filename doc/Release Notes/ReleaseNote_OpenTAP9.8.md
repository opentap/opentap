Release Notes - OpenTAP 9.8
=============

New Features:
------

- Scope Parameters [#233](https://gitlab.com/OpenTAP/opentap/-/issues/233)

Usability Improvements:
-------

- Improve the performance of test plan reference [#175](https://gitlab.com/OpenTAP/opentap/-/issues/175)
- Allow packages to be directly downloaded from AWS S3 [#185](https://gitlab.com/OpenTAP/opentap/issues/185)
- Test plans missing resources should produce 'warning' logs instead of 'info' [#220](https://gitlab.com/OpenTAP/opentap/issues/220)

Bug Fixes: 
-------

- Resource Close sometimes called before Open is finished [#212](https://gitlab.com/OpenTAP/opentap/issues/212)
- `tap run` with external parameter does not work with resources [#225](https://gitlab.com/OpenTAP/opentap/issues/225)
- MSBuild error when creating package using Linux docker image [#226](https://gitlab.com/OpenTAP/opentap/issues/226)
- Connections view does not use DisplayAttribute [#227](https://gitlab.com/OpenTAP/opentap/issues/227)
- Resources that uses EmbedPropertiesAttribute do not deserialize correctly [#228](https://gitlab.com/OpenTAP/opentap/issues/228)
- Incorrect prerelease versions if `.gitversion` file contains default version number (0.1.0) does not exist [#229](https://gitlab.com/OpenTAP/opentap/-/issues/229)
- Cannot Set Flags Beyond 32 bits [#245](https://gitlab.com/OpenTAP/opentap/issues/245)
- ICliAction: Browsable(false) and Display Groups bug [#246](https://gitlab.com/OpenTAP/opentap/issues/246)
- AvailableValues do not show when used as merged external parameter with Test Plan Reference Step [#251](https://gitlab.com/OpenTAP/opentap/-/issues/251)

Documentation:
----

- HTTP 404 error : documentation links [#167](https://gitlab.com/OpenTAP/opentap/issues/167)
- Document docker images on doc.opentap.io [#194](https://gitlab.com/OpenTAP/opentap/issues/194)
- Document UserInput.Request API [#204](https://gitlab.com/OpenTAP/opentap/-/issues/204)
- More documentation of OpenTapPackageReference and AdditionalOpenTapPackage [#208](https://gitlab.com/OpenTAP/opentap/issues/208)
- Improve documentation on doc.opentap.io [#224](https://gitlab.com/OpenTAP/opentap/issues/224)
- Typo in developer guide "Result Listners" [#230](https://gitlab.com/OpenTAP/opentap/-/issues/230)
- Document ResourceOpenAttribute [#231](https://gitlab.com/OpenTAP/opentap/-/issues/231)
- NuGet and TapPackage files do not contain API documentation [#240](https://gitlab.com/OpenTAP/opentap/-/issues/240)
- Documentation links don't work [#243](https://gitlab.com/OpenTAP/opentap/issues/243)

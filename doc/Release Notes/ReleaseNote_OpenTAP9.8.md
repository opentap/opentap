Release Notes - TestAutomation 9.8
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

Documentation:
----

- Document UserInput.Request API [#204](https://gitlab.com/OpenTAP/opentap/-/issues/204)
- Improve documentation on doc.opentap.io [#224](https://gitlab.com/OpenTAP/opentap/-/issues/224)
- NuGet and TapPackage files do not contain API documentation [#240](https://gitlab.com/OpenTAP/opentap/-/issues/240)
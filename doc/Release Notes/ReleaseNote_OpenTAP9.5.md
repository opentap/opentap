Release Notes - OpenTAP 9.5
=============

New Features:
-------

- Hashes of files in TapPackages [#50](https://gitlab.com/OpenTAP/opentap/issues/50)
- Improve process of referencing assembly from other TapPackage. [#57](https://gitlab.com/OpenTAP/opentap/issues/57)
- Multiselect list of ITestSteps [#58](https://gitlab.com/OpenTAP/opentap/issues/58)
- Allow TestSteps to access results from other steps [#59](https://gitlab.com/OpenTAP/opentap/issues/59)
- Add libiovisa to the search in Visa.cs [#63](https://gitlab.com/OpenTAP/opentap/issues/63)
- Creating a new project with the OpenTAP SDK should use a NuGet version corresponding to the installed version. [#64](https://gitlab.com/OpenTAP/opentap/issues/64)
- Allow For Classes Containing Common Properties to be Used [#65](https://gitlab.com/OpenTAP/opentap/issues/65)
- Input to support selecting parent steps [#71](https://gitlab.com/OpenTAP/opentap/issues/71)
- Support External Parameters of type List\<string\> [#76](https://gitlab.com/OpenTAP/opentap/issues/76)
- Max size for all session log files increased to a total of 2GB [#79](https://gitlab.com/OpenTAP/opentap/issues/79)

Usability Improvements: 
-------

- Http Package Repository Download is the cause of zombie processes [#56](https://gitlab.com/OpenTAP/opentap/issues/56)
- IVersionConvert throws exception whenever starting a debug session[#61](https://gitlab.com/OpenTAP/opentap/issues/61)
- TypeData.GetTypeData is ambiguous when using it with a Type object. [#62](https://gitlab.com/OpenTAP/opentap/issues/62)
- Place log files in the SessionLog folder and not in subfolders [#83](https://gitlab.com/OpenTAP/opentap/issues/83)
- Added package dependency on serializer plugins
- Sweep loop range now clears parameters

Bug Fixes: 
-------

- `tap package list` errors out while packages are being uploaded to repo [#17](https://gitlab.com/OpenTAP/opentap/issues/17)
- `tap package list` with package name and `--installed` yields unexpected result [#44](https://gitlab.com/OpenTAP/opentap/issues/44)
- Properties with no getters break typedata [#52](https://gitlab.com/OpenTAP/opentap/issues/52)
- Run Program step can't run tap on windows [#75](https://gitlab.com/OpenTAP/opentap/issues/75)
- Fixed a bug where invalid station macro caused a crash
- `--results "" ` now disables all result listeners

Other: 
-------

- Add Owner xml element to Package Definition [#48](https://gitlab.com/OpenTAP/opentap/issues/48)
- Extensible Plugin/Type Searcher [#53](https://gitlab.com/OpenTAP/opentap/issues/53)
- Input throws IndexOutOfRange exception when PropertyName is set [#69](https://gitlab.com/OpenTAP/opentap/issues/69)
- Add `LicenseRequired` and `SourceUrl` to package definition [#72](https://gitlab.com/OpenTAP/opentap/issues/72)
- TestStep.PlanRun is null during PrePlanRun and PostPlanRun [#74](https://gitlab.com/OpenTAP/opentap/issues/74)

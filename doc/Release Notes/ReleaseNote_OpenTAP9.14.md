Release Notes  - OpenTAP 9.14
=============

New Features:
-------

- Support Parameters on ResultTable and ResultColumn [#577](https://gitlab.com/OpenTAP/opentap/issues/577)


Usability Improvements: 
-------

- Unintended package action behavior when not launched from tap.exe [#606](https://gitlab.com/OpenTAP/opentap/issues/606)
- Validate parameters values on sweep parameters [#611](https://gitlab.com/OpenTAP/opentap/issues/611)
- Return exit code exception from `uninstall` and `test` [#612](https://gitlab.com/OpenTAP/opentap/issues/612)
- `ITableExport`: wrong `Display` name [#629](https://gitlab.com/OpenTAP/opentap/issues/629)
- Possible race-condition in Scpi.cs [#622](https://gitlab.com/OpenTAP/opentap/issues/622)
- Improve performance of HttpRepository communication [#626](https://gitlab.com/OpenTAP/opentap/issues/626)
- Installing an incompatible package shows invalid messages [#645](https://gitlab.com/OpenTAP/opentap/issues/645)
- `tap package download`: Add a more user friendly message when downloading an incompatible package [#648](https://gitlab.com/OpenTAP/opentap/issues/648)
- Update checks occurs in the middle of user input requests [#651](https://gitlab.com/OpenTAP/opentap/issues/651)

Bug Fixes: 
-------

- Assigned outputs still work when steps are moved outside scope [#584](https://gitlab.com/OpenTAP/opentap/issues/584)
- Boolean value can not be added [#616](https://gitlab.com/OpenTAP/opentap/issues/616)
- Test Plan Reference: `Edit Parameter` works on a loaded parent step [#613](https://gitlab.com/OpenTAP/opentap/issues/613)
- `CliUserInputInterface` assumes that request object members are declared in a specific order [#610](https://gitlab.com/OpenTAP/opentap/issues/610)
- `tap run`: wrong path handling for the csv parameter file [#623](https://gitlab.com/OpenTAP/opentap/issues/623)
- `tap run` ignores the csv external-parameter file if the CSV plugin is missing [#625](https://gitlab.com/OpenTAP/opentap/issues/625)
- The `non-interactive` flag should really be non-interactive [#635](https://gitlab.com/OpenTAP/opentap/issues/635)
- `tap run --metadata` is not being treated as metadata during test plan run [#639](https://gitlab.com/OpenTAP/opentap/issues/639)
- Sweep Parameters: Invalid Values but execution completes successfully  [#642](https://gitlab.com/OpenTAP/opentap/issues/642)
- Test plan settings disconnected from test steps when a test step is removed [#643](https://gitlab.com/OpenTAP/opentap/issues/643)
- Multiple reads can cause performance issues with slow properties [#644](https://gitlab.com/OpenTAP/opentap/issues/644)
- NuGet package fails [#653](https://gitlab.com/OpenTAP/opentap/issues/653)


Documentation: 
-------

- Revise "Packaging Configuration File" in the developer guide [#585](https://gitlab.com/OpenTAP/opentap/issues/585)
- Document how plugin properties work [#592](https://gitlab.com/OpenTAP/opentap/issues/592)
- Update Docs on Obfuscation [#624](https://gitlab.com/OpenTAP/opentap/issues/624)

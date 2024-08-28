Release Notes - opentap 9.25.0 
 ============= 

New Features 
 ------- 

- Parallel step disables "suggested next step" functionality [#1568](https://github.com/opentap/opentap/issues/1568)
- Duplicate Type names should produce a warning during startup [#1503](https://github.com/opentap/opentap/issues/1503)
- Test Plan Summary: Duration units improved for long test plans [#1577](https://github.com/opentap/opentap/issues/1577)
- Make the 'tap image' subcommand browsable [#1589](https://github.com/opentap/opentap/issues/1589)


Bug Fixes 
 ------- 

- Rare resolver error during image resolution [#1642](https://github.com/opentap/opentap/issues/1642)
- Messages logged during tap initialization are sometimes lost [#1631](https://github.com/opentap/opentap/issues/1631)
- System-wide packages can break an installation if they depend on opentap [#1605](https://github.com/opentap/opentap/issues/1605)
- Run Program: Test Plan cannot be stopped while Firefox is open [#1604](https://github.com/opentap/opentap/issues/1604)
- tap package install returns 405 Not Allowed [#1595](https://github.com/opentap/opentap/issues/1595)
- `tap --help` is not a valid command [#1576](https://github.com/opentap/opentap/issues/1576)
- SDK: RC version is considered Release version after OpenTAP is updated to latest RC [#1520](https://github.com/opentap/opentap/issues/1520)
- Log rollover: Latest.txt log file content is older than the one in the last log generated [#1518](https://github.com/opentap/opentap/issues/1518)


Usability Improvements 
 ------- 

- Refuse to create new installations inside existing installations [#1571](https://github.com/opentap/opentap/issues/1571)
- CreateMutexCore Exception in Rider on MacOS [#1561](https://github.com/opentap/opentap/issues/1561)
- improve resolver errors in trivial error cases [#1525](https://github.com/opentap/opentap/issues/1525)
- OpenTAP Image Resolver Errors [#1563](https://github.com/opentap/opentap/issues/1563)


Documentation 
 ------- 

- Document recommendations about threading [#1574](https://github.com/opentap/opentap/issues/1574)
- docs: search function not functional [#1515](https://github.com/opentap/opentap/issues/1515)


Cleanup 
 ------- 
- Remove unnecessary usings [#1636](https://github.com/opentap/opentap/issues/1636)
- Delete Engine.TestModule/ [#1630](https://github.com/opentap/opentap/issues/1630)
- Update System.Text.Json to latest compatible version [#1608](https://github.com/opentap/opentap/issues/1608)
- Update to Newtonsoft.Json 13.0.3 [#1599](https://github.com/opentap/opentap/issues/1599)


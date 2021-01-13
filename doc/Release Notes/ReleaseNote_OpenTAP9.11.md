Release Notes  - OpenTAP 9.11
=============

Usability Improvements:
-------

- `tap package install --interactive`: List of overwritten files is displayed twice [#428](https://gitlab.com/OpenTAP/opentap/issues/428)
- AnnotationCollection performance improvements [#439](https://gitlab.com/OpenTAP/opentap/issues/439)
- Package Actions for system-wide packages should be executed with the system-wide package folder as a working directory. [#454](https://gitlab.com/OpenTAP/opentap/issues/454)

Bug Fixes:
-------

- Connections: only PortA is used to get available connections. [#291](https://gitlab.com/OpenTAP/opentap/issues/291)
- "Type" column does not display ITypeData display attribute [#400](https://gitlab.com/OpenTAP/opentap/issues/400)
- Test Plan Reference: Can assign output to step setting loaded from test plan [#447](https://gitlab.com/OpenTAP/opentap/issues/447)
- Resource property on TestSteps deselects when used with `AvailableValues` [#455](https://gitlab.com/OpenTAP/opentap/issues/455)
- Endless VS background tasks with projects that use the OpenTAP NuGet [#460](https://gitlab.com/OpenTAP/opentap/issues/460)
- Cannot Install 32bit dependencies on a 64 OS [#465](https://gitlab.com/OpenTAP/opentap/issues/465)

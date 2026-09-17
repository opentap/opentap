Release Notes - OpenTAP 9.35.0
============= 

This release focuses on stability, SDK and documentation improvements.

## Highlights

### Parameterize and Merge Composite Parameters

Test step settings of composite types can now be parameterized and merged on
parent steps, meaning it is now possible to sweep e.g. a list of objects which
is shared by multiple child steps. Previously this was only possible with
primitives types.

### New Solution Template

Version 9.35.0 of the `SDK` package now includes a new and improved OpenTAP solution template.
The template can be used via `dotnet new opentap-solution --name <name>`.


### Minor Improvements

- The `Run Program` step now checks it exit code and logs its output by default.
- The `PackageDef` constructor is now public.
- The plugin system now loads types slightly less eagerly when using `[AllowChild]` and related attributes.
- [`OPENTAP_WARNINGS_TO_STDERR`][2] can now be used to log warnings to `stderr` instead of `stdout`.
- [`OPENTAP_NONINTERACTIVE`][2] can now be checked to see if the current OpenTAP context is running non-interactively.
- `AuthenticationSettings` URLs now support specifying a port.
- `ScpiInstrument` constructor no longer calls `GetResourceManager()`. This
  avoids hard to diagnose errors when the host system has faulty VISA drivers
  by deferring errors to a later point which developers have more control over.

### SDK

- Overhauled the `dotnet new opentap-solution` template to use modern OpenTAP and .NET features.
- Added `dotnet new` item templates for common plugin types.
- SDK GUI examples now build on Linux and MacOS
- `tap package verify` now succeeds in build folders.
- Incremental builds in multi-project solutions are now faster.

### Documentation

- Added documentation about how to [**document plugins with markdown**][3].
- Updated [**HelpLink Documentation**][4] about linking to markdown.
- Documented [**how to use repository tokens**][1].
- OpenTAP online documentation is now included in the package in markdown format.

### Bug Fixes

- Fixed Break Conditions not triggering in `Sequence` steps when using `Results.Defer()`
- Fixed `Repeat Step` repeat check race condition when using `Results.Defer()`
- Fixed `package.xml` wildcard globs (`**`) not expanding correctly.
- Fixed `BreakCondition` related strings not translating correctly.
- Mixins can once again be used on Resources, such as instrument and DUT settings.
- Sweep Row columns are no longer duplicated when using the Annotation API in a certain way.
- `SecureString` content is no longer truncated after serialization.
- Extreme double values are now correctly roundtripped without loss of precision


[1]: <https://doc.opentap.io/Developer%20Guide/Getting%20Started%20in%20Visual%20Studio/Readme.html#referencing-private-packages> "doc/Referencing Private Packages"
[2]: <https://doc.opentap.io/Developer%20Guide/Appendix/Readme.html#environment-variables> "doc/Environment Variables"
[3]: <https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/Readme.html#package-documentation> "doc/Package Documentation"
[4]: <https://doc.opentap.io/Developer%20Guide/Attributes/Readme.html#helplink-attribute> "doc/HelpLink Attribute"

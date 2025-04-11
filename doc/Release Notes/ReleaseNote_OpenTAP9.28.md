Release Notes - OpenTAP 9.28.0 
 ============= 

New Features 
 ------- 

- Support for custom EULA in packages [#1902](https://github.com/opentap/opentap/issues/1902)
- Support Nullable&lt;T&gt; in test steps [#1852](https://github.com/opentap/opentap/issues/1852)
- Modify Mixin: Use Display Name instead of type name if no name is specified [#1847](https://github.com/opentap/opentap/issues/1847)
- Improve performance when resolving images from multiple http repositories [#1716](https://github.com/opentap/opentap/issues/1716)
- Allow XmlIgnore on EmbedProperties [#1327](https://github.com/opentap/opentap/issues/1327)
- "Validation" package.xml element to validate that a package is correctly installed [#1929](https://github.com/opentap/opentap/issues/1929)


Bug Fixes 
 ------- 

- Fix cache inconsistency in MemberValueAnnotation [#1947](https://github.com/opentap/opentap/issues/1947)
- Fix issue causing ParameterMemberData.Remove to not properly remove the parameter [#1939](https://github.com/opentap/opentap/issues/1939)
- Fix issue causing OpenTAP NuGet package to not treat PlatformTarget correctly [#1926](https://github.com/opentap/opentap/issues/1926)
- Fix AbandonedMutex causing OpenTAP to stop functioning [#1913](https://github.com/opentap/opentap/issues/1913)
- Fix null reference exception in `tap sdk gitversion [#1895](https://github.com/opentap/opentap/issues/1895)
- Fix signal handler on mac: Stuck on "waiting for other package manager operation to complete" [#1834](https://github.com/opentap/opentap/issues/1834)
- Correctly detect compatiblity errors during package create when a package manually overrides a dependency version [#1899](https://github.com/opentap/opentap/issues/1899)

Usability Improvements 
 ------- 

- Comply with UI Standards in the Dialog Buttons for Dialog Step [#1857](https://github.com/opentap/opentap/issues/1857)


Documentation 
 ------- 

- Add license file to OpenTap Templates nuget package [#1849](https://github.com/opentap/opentap/issues/1849)


Other 
 ------- 

- Cleanup: Prefer ordinal string comparisons over invariant string comparisons [#1819](https://github.com/opentap/opentap/issues/1819)

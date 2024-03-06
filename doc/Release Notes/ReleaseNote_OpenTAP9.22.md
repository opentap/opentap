Release Notes - OpenTAP 9.22.0 
 ============= 

Highlights
 ------
- Support for mixins support - a new way of extending test step settings and functionality.
- Artifacts - data as results. Read more here: https://doc.opentap.io/Developer%20Guide/Test%20Step/#artifacts
- Support for authentication tokens inside C# project files.
- New installation experience for Linux.
- ScpiStep and ProcessStep now can now save their outputs in an [Output] property.

New Features 
 ------- 

- [#1254 Support authentication tokens in builds Feature](https://github.com/opentap/opentap/issues/1254)
- [#1233 Add Outputs to Scpi Step and Process Step Feature](https://github.com/opentap/opentap/issues/1233)
- [#1228 Make ResultOptimization Optional Feature](https://github.com/opentap/opentap/issues/1228)
- [#1212 Artifacts Feature](https://github.com/opentap/opentap/issues/1212)
- [#1194 Expressions and string interpolations Feature](https://github.com/opentap/opentap/issues/1194)
- [#1175 Mixins Feature](https://github.com/opentap/opentap/issues/1175)
- [#1174 Handle SIGTERM on Linux Add test Feature](https://github.com/opentap/opentap/issues/1174)
- [#1161 User Defined Settings and expressions and string interpolations Add test Feature](https://github.com/opentap/opentap/issues/1161)
- [#1158 Support more OSs Feature](https://github.com/opentap/opentap/issues/1158)
- [#1120 Support basic math operations on Settings Add test Feature](https://github.com/opentap/opentap/issues/1120)
- [#1110 Add Hash property to TestPlanReference steps Feature](https://github.com/opentap/opentap/issues/1110)
- [#1097 Allow more flexibility in ordering of IStringConvertProviders Feature](https://github.com/opentap/opentap/issues/1097)
- [#1063 PackageActionExtension feature: Pass when ExeFile does not exist Feature](https://github.com/opentap/opentap/issues/1063)
- [#1037 Type menu annotations API Feature](https://github.com/opentap/opentap/issues/1037)
- [#800 SessionLog include information about Installed Packages Add test Feature](https://github.com/opentap/opentap/issues/800)
- [#627 Add elevation prompt when uninstalling system-wide packages Feature Usability](https://github.com/opentap/opentap/issues/627)
- [#1216 Add 'main' to list of default branches in GitVersionCalculator enhancement](https://github.com/opentap/opentap/issues/1216)

Bug Fixes 
 ------- 

- [#1309 SDK plugin install should fail silently if .NET SDK is not installed bug](https://github.com/opentap/opentap/issues/1309)
- [#1306 Mixins can be added on read-only test steps bug Regression](https://github.com/opentap/opentap/issues/1306)
- [#1303 null strings with ResultAttribute causes errors bug](https://github.com/opentap/opentap/issues/1303)
- [#1301 Mixins can be removed or modified on a locked test plan bug](https://github.com/opentap/opentap/issues/1301)
- [#1297 Multi-select and multiple IEnabledAnnotations are not compatible bug](https://github.com/opentap/opentap/issues/1297)
- [#1293 Test Plan Reference: Can add/edit/remove expressions and mixins bug](https://github.com/opentap/opentap/issues/1293)
- [#1291 Parameterized mixin remains after removing the mixin itself bug](https://github.com/opentap/opentap/issues/1291)
- [#1282 Race Condition in GetSettingsLookup bug](https://github.com/opentap/opentap/issues/1282)
- [#1279 Result property does not work for strings bug](https://github.com/opentap/opentap/issues/1279)
- [#1275 Linux: SmartInstaller fails when webkit2gtk is not installed bug Regression](https://github.com/opentap/opentap/issues/1275)
- [#1259 Warning shown when listing packages in the default folder bug](https://github.com/opentap/opentap/issues/1259)
- [#1243 Installing/uninstalling system-wide packages fails without error bug](https://github.com/opentap/opentap/issues/1243)
- [#1237 'Tap package verify' command fails for system-wide packages Add test bug](https://github.com/opentap/opentap/issues/1237)
- [#1211 Reinstalling/updating OpenTAP plugin resolves oldest version (instead of newest) bug](https://github.com/opentap/opentap/issues/1211)
- [#1186 Error when selecting two Sweep Loop steps bug](https://github.com/opentap/opentap/issues/1186)
- [#1182 Multiple edit: Run Program: Environment Variables does not work bug](https://github.com/opentap/opentap/issues/1182)
- [#1166 Package references not on C:\ fail to install bug](https://github.com/opentap/opentap/issues/1166)
- [#1162 ConstResource cache not invalidating bug](https://github.com/opentap/opentap/issues/1162)
- [#1146 Nested embedded properties does not support validation rules bug](https://github.com/opentap/opentap/issues/1146)
- [#1134 Parameterizing and unparameterizing the same property causes errors bug](https://github.com/opentap/opentap/issues/1134)
- [#1132 PackageInstallActions broken due to locked installation bug Regression](https://github.com/opentap/opentap/issues/1132)
- [#1127 Two members of different objects can be parameterized, but not unparameterized at the same time. bug](https://github.com/opentap/opentap/issues/1127)
- [#1106 Reinstalling Dotfuscator generates logs referring to missing files bug](https://github.com/opentap/opentap/issues/1106)
- [#1083 'Tap package verify' command fails for OpenTAP: 'tap.exe' has non-matching checksum bug](https://github.com/opentap/opentap/issues/1083)
- [#1052 NuGet package: Build fails when installing system-wide packages bug SDK](https://github.com/opentap/opentap/issues/1052)
- [#817 tap package list on macOS shows all packages on the repo bug To be Discussed](https://github.com/opentap/opentap/issues/817)


Other 
 ------- 

- [#1265 Linux: tap does not behave well as a background job in shells CLI Usability](https://github.com/opentap/opentap/issues/1265)
- [#1216 Add 'main' to list of default branches in GitVersionCalculator enhancement](https://github.com/opentap/opentap/issues/1216)
- [#1215 Document the Artifacts feature DOC: Example documentation](https://github.com/opentap/opentap/issues/1215)
- [#1214 Document EmbedPropertiesAttribute documentation](https://github.com/opentap/opentap/issues/1214)
- [#1204 OpenTap.Templates.9.21.0.nupkg - Editor Reference SDK](https://github.com/opentap/opentap/issues/1204)
- [#1189 Update CE references to Community License documentation](https://github.com/opentap/opentap/issues/1189)
- [#1160 Package Manager: The local repository path in Settings is not user friendly Add test Usability](https://github.com/opentap/opentap/issues/1160)
- [#1125 Add .exe and .tar files as assets to the Release Notes documentation](https://github.com/opentap/opentap/issues/1125)
- [#1078 Rules and [EnableIf(..., HideIfDisabled=true)] do not have effect on properties using [EmbedProperties] attribute Usability](https://github.com/opentap/opentap/issues/1078)

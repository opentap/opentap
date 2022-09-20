Release Notes - opentap 9.19 
 ============= 

New Features 
 ------- 

- Process Step: Allow setting environment variables [#476](https://github.com/opentap/opentap/issues/476)
- Fall back to matching name rather than declared type when deserializing resources [#795](https://github.com/opentap/opentap/issues/795)


Usability Improvements 
 ------- 

- Packages cannot contain '/' characters. This should be enforced and documented [#550](https://github.com/opentap/opentap/issues/550)
- Error messages are unhelpful when installer fails to extract files [#746](https://github.com/opentap/opentap/issues/746)


Bug Fixes 
 ------- 

- Transitive dependency resolution bug [#460](https://github.com/opentap/opentap/issues/460)
- TypeData.CanCreateInstance has invalid value for ValueTypes [#610](https://github.com/opentap/opentap/issues/610)
- DMM Test Steps not installed after choosing Ignore [#625](https://github.com/opentap/opentap/issues/625)
- Object reference not set to an instance of an object - better error message handling [#688](https://github.com/opentap/opentap/issues/688)
- Stop shipping netstandard.dll on Linux [#689](https://github.com/opentap/opentap/issues/689)
- Creating unbuildable projects is extremely easy [#692](https://github.com/opentap/opentap/issues/692)
- The .PackageCache XML for the user Package Cache is being deleted on every run [#720](https://github.com/opentap/opentap/issues/720)
- "tap package list --os" should show an error with an invalid argument [#732](https://github.com/opentap/opentap/issues/732)
- CLI option '--quiet' works, but '-q' does not [#734](https://github.com/opentap/opentap/issues/734)
- tap run --non-interactive runs a test plan even if it fails to load it [#737](https://github.com/opentap/opentap/issues/737)
- I can't install OpenTAP when I have installed a plugin with a missing dependency [#741](https://github.com/opentap/opentap/issues/741)
- deserializing `PackageVersion`s with multiple licenses concatenates the licenses in a single string [#742](https://github.com/opentap/opentap/issues/742)
- Resources are Opened after LicenseException [#751](https://github.com/opentap/opentap/issues/751)
- Verify integrity of packages after creation [#752](https://github.com/opentap/opentap/issues/752)
- On linux it is not possible to install packages which contains ICustomPackageData as package dependencies. [#753](https://github.com/opentap/opentap/issues/753)
- Ignore dependencies from system-wide packages [#754](https://github.com/opentap/opentap/issues/754)
- CLI options with [Browsable(false)] are visible [#755](https://github.com/opentap/opentap/issues/755)
- Package Manager default settings not happy on Windows (file:///C:/Program%252520Files/OpenTAP)  [#760](https://github.com/opentap/opentap/issues/760)
- REST-API 2.9.1 does not show up in Package Manager UI when OpenTAP 9.18.4 is installed [#761](https://github.com/opentap/opentap/issues/761)
- InvalidOperationException: Collection was modified [#762](https://github.com/opentap/opentap/issues/762)
- Caught error while finishing serialization: Failed cloning NotSet as OpenTap.Input`1 [#763](https://github.com/opentap/opentap/issues/763)
- All test step threads are blocked while trying to publish results [#765](https://github.com/opentap/opentap/issues/765)
- AvailableValues are overridden when parameterizing parameterizing properties [#774](https://github.com/opentap/opentap/issues/774)
- tap sdk gitversion outputs a debug message [#789](https://github.com/opentap/opentap/issues/789)
- ResultTableOptimizer and multiresult step timing issue [#797](https://github.com/opentap/opentap/issues/797)
- Behavior change with old plugins 9.19 vs 9.18 [#808](https://github.com/opentap/opentap/issues/808)
- Running the command to install with a specific --architecture will not return a warning for incompatibility [#818](https://github.com/opentap/opentap/issues/818)
- Incorrect package hash calculation [#820](https://github.com/opentap/opentap/issues/820)
- Having a package in cache changes the order in `tap package list` [#828](https://github.com/opentap/opentap/issues/828)


Documentation 
 ------- 

- Add CI/CD documentation for Github [#494](https://github.com/opentap/opentap/issues/494)
- Documentation on Resource Deserialization [#771](https://github.com/opentap/opentap/issues/771)
- Document how to use multiple giversions [#806](https://github.com/opentap/opentap/issues/806)


Other 
 ------- 

- Serialize performance improvements [#588](https://github.com/opentap/opentap/pull/588)
- SDK: Require "Version" field for packages [#691](https://github.com/opentap/opentap/issues/691)
- Relative URLs [#701](https://github.com/opentap/opentap/issues/701)
- Obsolete CommandLineArgumentAttribute.Visible in favor of BrowsableAttribute [#764](https://github.com/opentap/opentap/issues/764)
- ScpiInstrument not supported on Ubuntu 22.04 [#781](https://github.com/opentap/opentap/issues/781)
- Can't install packages on Linux [#801](https://github.com/opentap/opentap/issues/801)

Release Notes - opentap 9.27.0 
 ============= 

New Features 
 ------- 

- Support `Convert to sequence` when multi-selecting [#1824](https://github.com/opentap/opentap/issues/1824)
- TestStepList API now raises an event when setting an index. [#1768](https://github.com/opentap/opentap/issues/1768)
- CableLoss interpolation now uses binary search instead of linear scan [#1758](https://github.com/opentap/opentap/issues/1758)
- Support adding Mixins to Test Plan Reference steps [#1737](https://github.com/opentap/opentap/issues/1737)
- Make ResourceManager available outside of TestPlan [#1679](https://github.com/opentap/opentap/issues/1679)
- Add IElementFactory interface for constructing list elements in lists with special behavior [#1798](https://github.com/opentap/opentap/issues/1798)
- Sweeps now include an iteration parameter [#1789](https://github.com/opentap/opentap/issues/1789)
- Allow TestPlan.Open to be called multipled times [#1766](https://github.com/opentap/opentap/issues/1766)
- Added a button for converting test plan reference to a sequence step [#1762](https://github.com/opentap/opentap/issues/1762)
- Added support for mixins on `TestPlanReference` steps [#1761](https://github.com/opentap/opentap/pull/1761)
- Support settings assembly versions at build-time [#1687](https://github.com/opentap/opentap/issues/1687)


Bug Fixes 
 ------- 

- Image no longer tries extracting local files as zip archives if they happen to match a package name from the image [#1835](https://github.com/opentap/opentap/issues/1835)
- TapAssemblyResolver.Invalidate now correctly invalidates assemblies [#1775](https://github.com/opentap/opentap/issues/1775)
- TestPlanReference can now be copy-pasted after adding an Expression mixin [#1771](https://github.com/opentap/opentap/issues/1771)
- Fixed intermittent memory leaks with Connections and Cable Losses [#1756](https://github.com/opentap/opentap/issues/1756)
- SDK: Error is not displayed when selecting multiple Measure Peak Amplitude steps [#1663](https://github.com/opentap/opentap/issues/1663)
- Embed Properties Attribute Example: Cannot modify test step settings when more than one step is selected [#1527](https://github.com/opentap/opentap/issues/1527)
- Properties changed in TestStepPreRunEvent are now properly reflected in result parameters [#1805](https://github.com/opentap/opentap/issues/1805)
- Fix OS detection logic [#1783](https://github.com/opentap/opentap/issues/1783)
- gitversion calculation fails when local revision of beta branch is outdated [#1780](https://github.com/opentap/opentap/issues/1780)
- Error annotations now properly shown when Multi-Selected properties have the same error [#1700](https://github.com/opentap/opentap/issues/1700)


Documentation 
 ------- 

- Documented FilePath on Lists in the developer guide [#1793](https://github.com/opentap/opentap/issues/1793)


Other 
 ------- 

- TestStepList: Update ID Lookup on set item [#1773](https://github.com/opentap/opentap/issues/1773)
- Improve error message when .NET 6 runtime dlls cannot be loaded [#1765](https://github.com/opentap/opentap/issues/1765)

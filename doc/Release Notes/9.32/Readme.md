Release Notes - OpenTAP 9.32.0
============= 

Highlights
---------

### Multi-level Nested Sweeps

Parameter Sweep values can now be swept themselves, enabling multi-level sweeps.

### Parameter Improvements

Issues were identified at the intersection of parameterizing embedded values and mixin values. 

- Embedded settings can now be parameterized.
- Parameterized embedded settings now forward validation errors.

### Performance Improvements

Extensive performance improvements have been made to the way numbers are parsed from text. This gives a boost in UIs, save/load performance. Additionally, value cloning was significantly improved so now multi-selecting many steps has better performance.

- Number / Range parsing: Improves UI, loading XML.
- Trivial object cloning: Improves multi-select, parameters.
- List multi select: Improves performance of selecting many steps.

### Smaller Features
- API: ILoopStep - Marks a step as a loop step with the ability to get or set the current iteration. This can allow other plugins to go an iteration back or skip some iterations.

### Bug Fixes
- Parameterized embedded properties are now properly deserialized.
- Process Step does not add an extra newline to the output.
- Settings load no longer throws an exception if an error occurred during ComponentSettings.Initialize.
- New lines in the session log are now replaced with " " (space) instead of "" (empty string). 
- "tap package install Editor" now works even when a file named "Editor" exists.
- "tap sdk gitversion" now works with git worktrees.
- Custom annotators throwing an exception in their constructor no longer interfere with other annotators.

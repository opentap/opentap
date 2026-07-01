Release Notes - OpenTAP 9.34.0
============= 

This is a maintenance release focusing on performance and stability.

### Performance

- Editing sweeps with many parameters is now significantly faster https://github.com/opentap/opentap/issues/2379
- Image resolution is now significantly faster for non-resolving images https://github.com/opentap/opentap/issues/2398

### Minor Improvements
- macOS support is now enabled by default for newly created plugins. https://github.com/opentap/opentap/pull/2393

### Bug Fixes
- Fixed an installation lock bug on macOS which would lead to a bricked installation if an OpenTAP process crashed hard while holding the lock.
- Fixed installation lock issues on Linux and macOS which allowed simultaneous lock acquisition when more than two processes are involved: https://github.com/opentap/opentap/issues/2405
- Fixed a deadlock in the post-run of composite runs: https://github.com/opentap/opentap/issues/2371
- Fixed break conditions not triggering for steps using Defer: https://github.com/opentap/opentap/issues/2387
- Fixed a race condition causing issues in some circumstances when using EmbedPropertiesAttribute: https://github.com/opentap/opentap/issues/2395

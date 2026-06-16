Release Notes - OpenTAP 9.33.0
============= 

## Highlights

### Parameterization of Outputs

Outputs can now be parameterized. This is useful when the result of a sub-sequence of steps amounts to a few important values that you want to carry on through the test plan execution.

### Safer Test Plan Saving
Instead of writing directly to the test plan file, a temporary file is created first. Once the plan is fully saved without error, Delete and Move are used to replace the original. This ensures file integrity in the event of a connection loss (e.g. on a shared drive) or power loss.

### Minor Features
- `OPENTAP_LIBVISA_LOCATION` can now be used to specify a custom libvisa shared library location.
- `tap sdk gitversion` now works on Alpine Linux (musl libc).

### Bug Fixes
- Test plan stuck on parameter resolution error: https://github.com/opentap/opentap/issues/2307
- Test plan / step run parameters now support full groups.
- Memory leak when creating many dynamic test step types fixed: https://github.com/opentap/opentap/issues/2318
- SDK: Read-only list example had a stack overflow error due to the added Equals override.

Release Notes - OpenTAP 9.31.0
============= 

Highlights
---------

### Features

* Packages can now be updated or removed even if their files are in use

### Bug Fixes

* Test plans can now be saved despite custom serializers throwing unhandled exceptions
* Added missing OpenTapApiReference documentation on Linux / MacOS
* `tap.dll` is now correctly updated when updating OpenTAP
* Fixed a bug causing artifacts from custom stream types to be saved incorrectly in some cases
* Fixed bug causing CLI User Input to hang if initiated during an update check when not connected to the internet
* Fixed race condition leading to crashes and deadlocks during Editor startup and plugin search.

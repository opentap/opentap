CLI Reference
============

Although this table uses Windows variable expansion, OpenTAP behaves in the exact same way on Unix. On Unix, you need to replace `%TAP_PATH%` with `$TAP`

## Generic commands
| Command                          | Description                                                                                                                                                                 | Example                                    |
|----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------|
| `tap <command>`                  | Lists valid subcommands for `<command>`                                                                                                                                     | `tap package`                              |
| `tap <command> -h`               | Show help information for `<command>`                                                                                                                                       | `tap package --help`                       |
| `tap <command> (-v | --verbose)` | Show all log output from all sources                                                                                                                                        | `tap package run MyTestPlan.TapPlan -v`    |
| `tap <command> (-c | --color)`   | Color messages according to their level. Can be used with any of the above commands. Useful in conjunction with `--verbose`. Note that some terminals do not support colors. | `tap package run MyTestPlan.TapPlan -v -c` |



## The `run` command
| Command                                                        | Description                                                                                                                                                                                                | Example                                                                      |
|----------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------|
| `tap run <testplan>`                                           | Run `<testplan>` -- all log output except `Debug` sent to stdout                                                                                                                                           | `tap run MyTestPlan.TapPlan`                                                 |
| `tap run <testplan> --settings <dirname>`                      | Specify bench settings for the test plan being run. This refers to the configuration of DUTs, Connections, and Instruments. `<dirname>` should be the name of a subdirectory of `%TAP_PATH%/Settings/Bench` | `tap run MyTestPlan.TapPlan --settings RadioTestSetup`                       |
| `tap run <testplan> --search <path>`                           | Add `<path>` to search path when looking for plugin dlls. By default, `OpenTAP` searches in `%TAP_PATH%` and `%TAP_PATH%/Packages/*`                                                                       | `tap run MyTestPlan.TapPlan --search C:\Users\Me\OtherPlugins\MyDutProvider` |
| `tap run <testplan> --results`                                 | Set enabled results listeners as a comma separated list. Disable all results listeners with `--results ""`                                                                                             | `tap run MyTestPlan.TapPlan --results CSV,SQLite        `                    |
| `tap run <testplan> --non-interactive`                         | Never prompt for user input                                                                                                                                                                                | `tap run MyTestPlan.TapPlan --non-interactive`                               |
| `tap run <testplan> --list-external-parameters`                | List available external parameters                                                                                                                                                                         | `tap run MyTestPlan.TapPlan --list-external-parameters  `                    |
| `tap run <testplan> (-e | --external) <parameter>=<value>`     | Set the value of an external parameter. Can be specified one or more times. Parameters can also be loaded from a csv file, e.g. `-e params.csv`                                                        | `tap run MyTestPlan.TapPlan -e delay=1.0 -e timeout=5.0  `                   |
| `tap run <testplan> (-t | --try-external) <parameter>=<value>` | Same as `--external`, but ignore errors if the parameter does not exist.                                                                                                                                   | `tap run MyTestPlan.TapPlan -t delay=1.0 -t timeout=5.0 -t nonexistent=fine` |
| `tap run <testplan> --metadata <parameter>=<value>`            | Set metadata, such as the serial number of a DUT. Can be specified one or more times                                                                                                                       | `tap run MyTestPlan.TapPlan --metadata dut1-id=11 --metadata dut2-id=17`     |


## Package manager commands
> Note: Package names are case sensitive, and package names containing spaces (such as "Developer's System CE") must be quoted.

| Command                                | Description                                                                                                                                                                                                           | Example                                                          |
|----------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| `tap package verify <package>`         | Verify the integrity of installed packages by comparing hashes.                                                                                                                                                       | `tap package verify OpenTAP `                                    |
| `tap package uninstall <package 1…n>`  | Uninstall one or more packages. Specify location with `-t <dir>`                                                                                                                                                      | `tap package uninstall Demonstration Python`                     |
| `tap package test <package>`           | Run the `test` actionstep of `<package>` (defined in the [package.xml](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#plugin-packaging-and-versioning) file)                                          | `tap package test OpenTAP`                                       |
| `tap package download`                 | Download `<package>` to `%TAP_PATH%/<package>.TapPackage` without installing it.                                                                                                                                      | `tap package download TestPackage2`                              |
| `tap package install <package 1…n>`    | Install one or more packages. Specify repository with `-r <repo url>`. Automatically install dependencies with `-y`. Can either install packages from a [repository](http://packages.opentap.io/) or local TapPackage | `tap package install TestPackage1 TestPackage2 -y`               |
| `tap package create <package xml dir>` | Create a tap package, or plugin, from an XML description ([package.xml](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#plugin-packaging-and-versioning))                                              | `tap package create /path/to/folder/containing/package.xml/file` |

Other packages can add their own CLI actions. Documentation of these CLI actions is beyond the scope of this guide. Please refer to the documentation of these packages for more information about these CLI actions.


<style>

</style>
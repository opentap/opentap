# CLI Reference

## Generic commands

There are a couple of generic commands that can be useful when working with OpenTAP from the CLI.
These are:

 - **`tap <command>`** <br>List valid subcommands for the given `<command>`. For example: 
    ```powershell
       tap package
     ```
 - **`tap <command> -h`** <br>Show help information for the given `<command>`. For example: 
    ```powershell
    tap package --help
     ```
 - **`tap <command> (-v | --verbose)`** <br>Show all log output from all sources. For example:
    ```powershell
    tap package run MyTestPlan.TapPlan -v
    ```
 - **`tap <command> (-c | --color)`** <br>Color messages according to their level. Can be used with any of the above commands. Useful in conjunction with `--verbose`. Note that some terminals do not support colors. For example: 
    ```powershell
    tap package run MyTestPlan.TapPlan -v -c
    ```

## The `run` command

The `run` command group executes a test plan and can take the following arguments:

- **`tap run <testplan>`** <br>Run the specified `<testplan>`. All log outputs, except *Debug*, are sent to `stdout`. For example:
    ```powershell
       tap run MyTestPlan.TapPlan
     ```
- **`tap run <testplan> --settings <dirname>`** <br>Specify bench settings for the test plan being run. This refers to the configuration of DUTs, connections, and instruments. `<dirname>` should be the name of a subdirectory of `<install dir>/Settings/Bench`. For example:
    ```powershell
       tap run MyTestPlan.TapPlan --settings RadioTestSetup
     ```                      
- **`tap run <testplan> --search <path>`** <br>Add `<path>` to search path when looking for plugins. By default, OpenTAP searches in the installation directory and the `<install dir>/Packages` directory. For example: 
    ```powershell
       tap run MyTestPlan.TapPlan --search C:\Users\Me\OtherPlugins\MyDutProvider
     ```
- **`tap run <testplan> --results`** <br>Set enabled result listeners as a comma separated list. Disable all result listeners with `--results ""`. For example:
    ```powershell
       tap run MyTestPlan.TapPlan --results CSV,SQLite
     ```                            
- **`tap run <testplan> --non-interactive`** <br>Never prompt for user input. For example:
    ```powershell
      tap run MyTestPlan.TapPlan --non-interactive
     ```                               
- **`tap run <testplan> --list-external-parameters`** <br>List available external parameters. For example: 
    ```powershell
       tap run MyTestPlan.TapPlan --list-external-parameters
     ```                      
- **`tap run <testplan> (-e | --external) <parameter>=<value>`** <br>Set the value of an external parameter. Can be specified one or more times. Parameters can also be loaded from a csv file using `-e params.csv`. For example: 
    ```powershell
       tap run MyTestPlan.TapPlan -e delay=1.0 -e timeout=5.0
     ```                     
- **`tap run <testplan> (-t | --try-external) <parameter>=<value>`** <br>Same as `--external`, but ignore errors if the parameter does not exists. For example: 
    ```powershell
       tap run MyTestPlan.TapPlan -t delay=1.0 -t timeout=5.0 -t nonexistent=fine
     ```
- **`tap run <testplan> --metadata <parameter>=<value>`** <br>Set metadata, such as the serial number of a DUT. Can be specified one or more times. For example: 
    ```powershell
       tap run MyTestPlan.TapPlan --metadata dut1-id=11 --metadata dut2-id=17
     ```     

## Package manager commands

The `package` command group contains all commands and subcommands that help you manage your packages.

> Package names are case sensitive, and package names containing spaces (such as "Developer's System CE") must be surrounded by quotation marks.

- **`tap package create <package xml dir>`** <br>Create a tap package, or plugin, from an XML description ([package.xml](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#plugin-packaging-and-versioning)). For example: 
    ```powershell
       tap package create /path/to/folder/containing/package.xml/file
     ```
- **`tap package download`** <br>Download `<package>` to the installation directory without installing it. For example: 
    ```powershell
       tap package download TestPackage2
     ```                              
- **`tap package install <package 1…n>`** <br>Install one or more packages. Specify repository with `-r <repo url>`. Automatically install dependencies with `-y`. Can either install packages from a [repository](http://packages.opentap.io/) or local TapPackage. For example: 
    ```powershell
       tap package install TestPackage1 TestPackage2 -y
     ```               
- **`tap package list [package] [--version <version>] [--installed]`** <br>List available packages. If a package name is specified, only list versions of that package. If a version is specified, list all sub-versions of that version. `tap package list OpenTAP --version 9.4` lists OpenTAP versions 9.4.0, 9.4.1, 9.4.2. When using the `--installed` flag, only installed packages are listed. For example: 
    ```powershell
       tap package list OpenTAP
     ```                                       
- **`tap package show <package> [--version <version>] [--offline] [--include-files] [--include-plugins]`** <br>Show details about a package. If `--offline` is specified, OpenTAP will only search the local path and installation for packages. `--include-files` and `--include-plugins` display all files and plugins included in the package. For example: 
    ```powershell
       tap package show OpenTAP --include-files
     ```                       
- **`tap package test <package>`** <br>Run the `test` actionstep of `<package>` (defined in the [package.xml](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#plugin-packaging-and-versioning) file). For example: 
    ```powershell
       tap package test OpenTAP
     ```                                      
- **`tap package uninstall <package 1…n>`** <br>Uninstall one or more packages. Specify location with `-t <dir>`. For example: 
    ```powershell
       tap package uninstall Demonstration Python
     ```                     
- **`tap package verify <package>`** <br>Verify the integrity of installed packages by comparing hashes. For example: 
    ```powershell
       tap package verify OpenTAP
     ```

Other packages can add their own commands. Documentation of these commands is beyond the scope of this guide. Please refer to the package's own documentation for more information about them.

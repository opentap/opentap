# CLI Usage

Although this chapter primarily targets users, developers will likely find it helpful as well. The purpose of this document is twofold:
1. to familiarize you with the built-in features of the OpenTAP CLI, and get started installing plugins. 
2. to introduce you to useful tools in constructing and managing test plans. 

Since a large chunk of the value of OpenTAP as a platform comes from its extensibility through plugins, the application itself only ships with a few essential components:

1. a package manager to browse and install plugins
2. the capability to execute test plans.

This keeps the core engine fast, lean, and enables easy deployment in container solutions such as Docker.

Every CLI action, whether package subcommands or user provided, share three CLI flags:

| flag        | description                                                                                                                                   |
|-------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `--help`    | Output help text for the given command                                                                                                        |
| `--verbose` | Send all debug output to standard output. The additional output shown here is always available in the [log files](../Introduction/#log-files) |
| `--color`   | Color standard output according to their severity                                                                                             |

## Using the package manager

The package manager is meant for installing, uninstalling, and creating packages. It is capable of listing available packages and versions based on CPU architecture and operating system, but it does not provide any *information* about packages beyond a name. For a package description, dependencies, and a list of files and plugins included in it, you need to browse [our repository](http://packages.opentap.io/index.html#/?name=OpenTAP).

The package manager has 7 subcommands:

1. [list](#list)
2. [install](#install)
3. [uninstall](#uninstall)
4. [download](#download)
5. [verify](#verify)
6. create [[see in developer guide]](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#command-line-use)
7. test &nbsp; &nbsp; [[see in developer guide]](../../Developer%20Guide/Plugin%20Packaging%20and%20Versioning/#command-line-use)

The `create` and `test` options are geared towards developers, and will not be covered in this section. The rest will be covered shortly, in sequence.

### list

In order to check what packages are available, run `tap package list`. To see what versions are available of a package, such as OpenTAP itself for instance, try `tap package list OpenTAP`.


### install
Basic usage of `install` is quite simple, but there are flags for advanced usage that you may find interesting.

`tap package install <package name> [<args>]`

By default, the `install` action installs the latest release for your platform. Updating any package, including OpenTAP itself, is easy. Just run `tap package install OpenTAP`. Installing a specific version of any package is also simple:

`tap package install OpenTAP --version 9.5.1` installs version 9.5.1; 

` ... --version beta` installs the latest beta; 

` ... --version rc` installs the latest release candidate. 

Whenever you install a package, the package manager will attempt to resolve the dependencies.
If you are missing a package dependency, the package manager will prompt you, and install it automatically if you confirm.
To avoid this behavior, you may install a package with the `-y` flag to automatically confirm all prompts.
If you are trying to install a package which is incompatible with your current install, the package manager will stop.
This could happen if you have a package installed which depends on OpenTAP ^9.5, and you try to install OpenTAP 9.4.
You can overwrite this behavior by using the `--force` option. 

Using the `-r` flag allows you to specify which repository to search for packages. Currently, the only public repository is [packages.opentap.io](http://packages.opentap.io).
Alternatively to a URL, you can specify a file path, or a network drive (e.g. `C:\Users\You\MyPlugins`), in order to collaborate locally. ```

The package manager also provides flags for specifying operating systems and CPU architecture, namely `--os` and `--architecture`, respectively.

Finally, the `-t` flag allows you to specify an installation directory. This creates a new tap install in the specified directory with only the plugins required for the packages you requested. This could be useful, for instance, if you need to install a package which is incompatible with your current tap installation. It could also be combined with the `--os` and `--architecture` flags to deploy to a different machine.

New plugins may provide their own CLI actions, thus increase the number of options. OpenTAP keeps track of installed plugins for you, so you can always verify available CLI actions by running `tap`. Example output for a clean install (version 9.5.1):

```
> tap

OpenTAP Command Line Interface (9.5.1)
Usage: tap <command> [<subcommand>] [<args>]

Valid commands are:
  run                   Runs a Test Plan.
  package
    create                Creates a package based on an XML description file.
    download              Downloads one or more packages.
    install               Install one or more packages.
    list                  List installed packages.
    test                  Runs tests on one or more packages.
    uninstall             Uninstall one or more packages.
    verify                Verifies installed packages by checking their hashes.
  sdk
    gitversion            Calculates a semantic version number for a specific git commit.

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.specific command.
```

### uninstall

### download

### verify




## Running test plans



The `run` commands executes a test plan.

> `tap run <file path> [<args>]`

### External settings

Step settings can be marked as "External". This means they can be set 
from the CLI, or from a file. This makes it possible to reuse the same test plan for a variety of tests.

To see what external steps a test plan 
contains, try 

> `tap run My.TapPlan --list-external`

If `--list-external-parameters` outputs:
```
TestPlan: My
Listing 3 external test plan parameters.
      value1 = x
      value2 = y
      value3 = z
```
then you can then set these values from the command line with 

> `tap run My.TapPlan -e value1 hello -e value2=3 -e value3=0.75`

Alternatively, you can create a csv file with the contents

```
value1,hello
value2,3
value3,0.75
```

Let's call the file "values.csv". You can then load these values into the external parameters with `tap run My.TapPlan -e values.csv`.

### Metadata

Analogous to external settings, resources settings can be marked as "Metadata". This could be 
the address of a DUT, for instance. Set this with `tap run My.TapPlan 
--metadata dut1=123 --metadata dut2=456`.

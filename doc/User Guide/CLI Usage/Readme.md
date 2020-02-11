# CLI Usage

Although this section primarily targets users, developers will likely find it helpful as well. The purpose of this
section is twofold:
1. To familiarize you with the built-in features of the OpenTAP CLI, and get started installing plugins.
2. To introduce you to useful tools in constructing and managing test plans.

Since the core value of OpenTAP comes from its extensibility through plugins, the application
itself ships with a few essential components:

1. A package manager to browse and install packages
2. The capability to execute test plans.

This keeps the core engine fast, lean, and enables easy deployment in container solutions such as Docker.
The CLI help of a clean OpenTAP install looks something like this:

```
> tap

OpenTAP Command Line Interface (9.5.2)
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

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.
```

The `sdk` subcommand is targeted at developers, and will not be covered in this section.

Every CLI action, whether package subcommands or user provided, share three CLI flags:

| flag        | description                                                                                                                                   |
|-------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `--help`    | Output help text for the given command                                                                                                        |
| `--verbose` | Send all debug output to standard output. The additional output shown here is always available in the [session logs](../Introduction/#session-logs) |
| `--color`   | Color standard output according to their severity                                                                                             |

## Using the package manager

The package manager is meant for installing, uninstalling, and creating packages containing plugins. It is capable of
listing available packages and versions based on CPU architecture and operating system, but it does not provide any
other information. For a package description, dependencies, and a list of files and plugins included in it, please visit
[our repository](http://packages.opentap.io/index.html#/?name=OpenTAP).

The package manager has 7 subcommands, which you can verify by running `tap package`

Sample output:

```
> tap package

OpenTAP Command Line Interface (9.5.2)
Usage: tap <command> [<subcommand>] [<args>]

Valid commands for 'package':
  create                Creates a package based on an XML description file.
  download              Downloads one or more packages.
  install               Install one or more packages.
  list                  List installed packages.
  test                  Runs tests on one or more packages.
  uninstall             Uninstall one or more packages.
  verify                Verifies installed packages by checking their hashes.

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.
```


The `create` and `test` options are geared towards developers, and will not be covered in this section.

The OpenTAP package manager assumes [semantic versioning](https://semver.org/) is honored in the likely event dependency
resolution is needed, and OpenTAP itself uses semantic versioning.

### Common `package` Flags

There are a few CLI flags which most `package` subcommands have in common:
| flag             | description                                                                                                 |
|------------------|-------------------------------------------------------------------------------------------------------------|
| `--os`           | Override which OS to target                                                                                 |
| `--architecture` | Override which CPU architecture to target                                                                   |
| `--repository`   | Override which repository to look for packages in (can be a URL, IP address, file path, or a network drive) |
| `--target`       | Override which directory the operation is applied in                                                        |

The default values of `os` and `architecture` are automatically configured according to the machine where OpenTAP is
installed, and the default repository is the official OpenTAP repository, [packages.opentap.io](packages.opentap.io).
This is usually what you want, but there are some situations where it may be useful to modify them. For example, you can
install OpenTAP onto a linux machine from a Windows machine with `tap package install OpenTAP --os Linux --target
C:\path\to\linux\install`

By default, all package commands apply operations in the directory where the `tap.exe` file is located. The `--target`
option makes it possible to manage multiple `tap` versions on the same machine.

### list

The `list` command is used to view information about plugins in your local OpenTAP installation, and available packages
in the repository.

In order to check what packages are available, run `tap package list`. To see what versions are available of a package,
such as OpenTAP itself, run `tap package list OpenTAP`.

To see what packages you currently have installed, use the `tap package list --installed` option. You can view what
packages are in a specific install directory with `tap package list --installed --target <install path>`. By default, `list` only
shows packages compatible with your OS and CPU architecture, and your currently installed packages. To see all packages, use the `--all` flag.


### install
Basic usage of `install` is quite simple, but there are flags for advanced usage that you may find interesting.
Before moving on to esoteric usage, a few pitfalls must be clarified:

1. You can install multiple packages at the same time. `tap package install OpenTAP "Editor CE"`.

2. Package names sometimes contain spaces. If they do, they must be quoted when referenced in any CLI action, as shown
above. Without the quotes, the package manager will interpret Editor CE as two different package names.

3. When running the install action, the package manager looks in the `%TAP_PATH%\PackageCache` directory first, and then
   in the specified repository.
4. You can install a local *.TapPackage* file, acquired with the `download` subcommand for example, with `tap package
   install <filename>`.

By default, the `install` action installs the latest release of a given package for your platform. Updating any package,
including OpenTAP itself, is easy. Just run `tap package install <package>`. Installing a specific version of any
package is also simple:

`tap package install OpenTAP --version 9.5.1` installs version 9.5.1;

` ... --version beta` installs the latest beta;

` ... --version rc` installs the latest release candidate.

Whenever you install a package, the package manager will attempt to resolve the dependencies. If you are missing a
package dependency, the package manager will prompt you, and install it automatically if you confirm. To avoid this
behavior, you may install a package with the `-y` flag to automatically confirm all prompts. If you are trying to
install a package which is incompatible with your current install, the package manager will stop. This could happen if
you have a package installed which depends on OpenTAP >= 9.5.1, and you try to install OpenTAP 9.4. You can override this
behavior by using the `--force` option, but this can lead to a non-functional installation.

Using `--os` and `--architecture`, you can install packages built for different operating systems and architectures. If
you specify values different from your system, they will likely not work. However, used in conjunction with the
`--target` option, you can use them to install packages on a different machine. The `--target` flag allows you to
specify an installation directory. This creates a new tap install in the specified directory with only the plugins
required for the packages you requested. This could be useful, for instance, if you need to install a package which is
incompatible with your current tap installation. You can also install a different version of OpenTAP in another location
with `tap package install OpenTAP --version 9.4.2 --target C:\path\to\other\install`.

New plugins may provide their own CLI actions, thus increase the number of options. OpenTAP keeps track of installed
plugins for you, so you can always verify available CLI actions by running `tap`.

### uninstall

Uninstall a package. The package manager will warn you if you attempt to uninstall a package which other packages in
your installation depend on. Uninstalling dependencies in spite of warnings may break your installation. However, unless
you removed OpenTAP, you can repair your install by reinstalling the uninstalled dependency.

Like the above two commands, `uninstall` supports targeting a different directory.

### download

Download a package without installing it. The downloaded package is placed in your `%TAP_PATH%`. As with the `install`
action, dependencies can be automatically added with `--dependencies`, and the os, version, and architecture can all be
specified.

### verify

Verify the integrity of a package by computing a hash of locally installed package, and comparing it to the hash in the
repository.

## Running test plans

The `run` commands executes a test plan.

> `tap run <file path> [<args>]`

### External settings

Step settings can be marked as "External". This means they can be set from the CLI, or from a file. This makes it
possible to reuse the same test plan for a variety of tests.

To see what external steps a test plan contains, try

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

Let's call the file "values.csv". You can then load these values into the external parameters with `tap run My.TapPlan
-e values.csv`.

### Metadata

Analogous to external settings, resources settings can be marked as "Metadata". This could be the address of a DUT, for
instance. Set this with `tap run My.TapPlan --metadata dut1=123 --metadata dut2=456`.

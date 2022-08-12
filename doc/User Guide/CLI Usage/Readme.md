# CLI Usage

Although this section primarily targets users, developers will likely find it helpful as well. The purpose of this
section is twofold:

1. To familiarize you with the built-in features of the OpenTAP CLI, and get started installing plugins.
2. To introduce you to useful tools for running test plans.

Since the core value of OpenTAP comes from its extensibility through plugins, the application
itself ships with a few essential components:

1. A package manager to browse and install packages.
2. The capability to execute test plans.

This keeps the core engine fast, lean, and enables easy deployment in container solutions such as Docker.
The CLI help of a clean OpenTAP installation looks something like this:

```
> tap

OpenTAP Command Line Interface (9.9.0)
Usage: tap <command> [<subcommand(s)>] [<args>]

Valid commands are:
run                    Run a test plan.
package
   create              Create a package based on an XML description file.
   download            Download one or more packages.
   install             Install one or more packages.
   list                List locally installed packages and browse the online package repository.
   show                Show information about a package.
   test                Run tests on one or more packages.
   uninstall           Uninstall one or more packages.
   verify              Verify the integrity of one or all installed packages by checking their fingerprints.
sdk
   gitversion          Calculate the semantic version number for a specific git commit.

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.
```

The `sdk` subcommand is targeted at developers, and will not be covered in this section.

Every CLI action, whether built-in or user-provided, shares three CLI options:

| Option            | Description                                                                                                                                   |
|-------------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `-h`, `--help`    | Output help text for the given command                                                                                                        |
| `-v`, `--verbose` | Send all debug-level log messages to standard output. The additional output shown here is always available in the [session logs](../Introduction/#session-logs) |
| `-c`, `--color`   | Color standard output messages according to their severity                                                                                    |

## Using the Package Manager

The package manager is meant for installing, uninstalling, and creating packages containing plugins. It is capable of
listing the available packages and their versions based on the CPU architecture and the operating system on which it is running.
Package names are case sensitive, and package names containing spaces, such as "Developer's System CE", must be
surrounded by quotation marks.

The package manager has several subcommands, which you can see by running `tap package`.

Sample output:

```
> tap package

OpenTAP Command Line Interface (9.9.0)
Usage: tap <command> [<subcommand(s)>] [<args>]

Valid subcommands of package
   create              Create a package based on an XML description file.
   download            Download one or more packages.
   install             Install one or more packages.
   list                List locally installed packages and browse the online package repository.
   show                Show information about a package.
   test                Run tests on one or more packages.
   uninstall           Uninstall one or more packages.
   verify              Verify the integrity of one or all installed packages by checking their fingerprints.

Run "tap.exe <command> [<subcommand>] -h" to get additional help for a specific command.
```

The `create` and `test` options are geared towards developers, and will not be covered in this section.

The OpenTAP package manager assumes [semantic versioning](https://semver.org/) is honored in the likely event dependency
resolution is needed, and OpenTAP itself uses semantic versioning.

### Common `package` Options

There are a few CLI options, which most `package` subcommands have in common:

| Option                 | Description                                                                                                  |
|------------------------|--------------------------------------------------------------------------------------------------------------|
| `--os`                 | Override the OS (Linux, Windows) to target                                                                   |
| `--architecture`       | Override the CPU architecture (x86, x64, AnyCPU) to target                                                   |
| `-r`, `--repository`   | Override the repository to look for packages in (it can be a URL, IP address, file path, or a network drive) |
| `-t`, `--target`       | Override the directory where the command is applied. The default is the OpenTAP installation directory.      |

The default values of `os` and `architecture` are automatically configured according to the machine where OpenTAP is
installed, and the default repository is the official OpenTAP repository, [packages.opentap.io](http://packages.opentap.io).

By default, all package commands apply operations in the OpenTAP installation directory, where the `tap.exe` file is located. The `--target`
option makes it possible to manage multiple `tap` versions on the same machine.

This is usually what you want, but there are some situations where it may be useful to modify them. For example, you can
install OpenTAP onto a Linux machine from a Windows machine with `tap package install OpenTAP --os Linux --target
C:\path\to\linux\installation`.

### list

The `list` command is used to view information about packages in your local OpenTAP installation, and available packages
in the online repository.

In order to check what packages are available, run `tap package list`. To see what versions of a package are available,
such as OpenTAP itself, run `tap package list OpenTAP`.

To see what packages are currently installed, use the `tap package list --installed` option. You can view what
packages are in a specific installation directory with `tap package list --installed --target <installation dir>`.

By default, `list` only shows packages compatible with your OS and CPU architecture, and your currently installed
packages. To see also packages incompatible with your OS and CPU architecture, use the `--all` option.

If a version is specified, `list` displays all sub-versions of that version. `tap package list OpenTAP --version 9.4`
lists OpenTAP versions 9.4.0, 9.4.1, and 9.4.2.

### show

Show details about a package. If `--offline` is specified, OpenTAP will only search the local installation for
packages. `--include-files` and `--include-plugins` display all files and plugins included in the package:
`tap package show OpenTAP --include-files`.

### install

The basic usage of `install` is quite simple, but there are options for advanced usage that you may find interesting.
Before moving on to esoteric usage, a few pitfalls must be clarified:

1. You can install multiple packages at the same time. `tap package install OpenTAP "Editor CE"`.

2. Package names sometimes contain spaces. If they do, they must be quoted when referenced in any CLI action, as shown
   above. Without the quotes, the package manager will interpret `Editor CE` as two different package names.

3. When running the install action, the package manager looks in the `<installation dir>/PackageCache` directory first, and then
   in the specified repository.

4. You can install a local *.TapPackage* file, acquired with the `download` subcommand for example, with `tap package
   install <filename>`.

By default, the `install` action installs the latest release of a given package for your platform. Updating any package,
including OpenTAP itself, is easy. Just run `tap package install <package>`.

Installing a specific version of any package is also simple:

`tap package install OpenTAP --version 9.5.1` installs version 9.5.1;

` ... --version beta` installs the latest beta;

` ... --version rc` installs the latest release candidate.

Whenever you install a package, the package manager will attempt to resolve the dependencies. If you are missing a
package dependency, the package manager will prompt you, and install it automatically if you confirm. To avoid this
behavior, you may install a package with the `-y` option to automatically confirm all prompts. If you are trying to
install a package that is incompatible with your current installation, the package manager will stop. This could happen if
you have a package installed that depends on OpenTAP >= 9.5.1, and you try to install OpenTAP 9.4. You can override this
behavior by using the `--force` option, but this can lead to a non-functional installation.

Using `--os` and `--architecture`, you can install packages built for different operating systems and architectures. If
you specify values different from your system, they will likely not work. However, used in conjunction with the
`--target` option, you can use them to install packages on a different machine. The `--target` option allows you to
specify an installation directory. This creates a new tap installation in the specified directory with only the plugins
required for the packages you requested. This could be useful, for instance, if you need to install a package that is
incompatible with your current tap installation. You can also install a different version of OpenTAP in another location
with `tap package install OpenTAP --version 9.4.2 --target C:\path\to\other\installation`.

New plugins may provide their own CLI actions, thus increase the number of options. OpenTAP keeps track of the installed
plugins for you, so you can always verify the available CLI actions by running `tap`.

### uninstall

Uninstall one or more packages, e.g., `tap package uninstall Demonstration Python`. The package manager will warn you if you attempt to uninstall a package other packages in
your installation depend on. Uninstalling dependencies in spite of warnings may break your installation. However, unless
you removed OpenTAP, you can repair your installation by reinstalling the uninstalled dependency.

Like the above two commands, `uninstall` supports targeting a different directory.

### download

Download one or more packages without installing them. Dependencies can be automatically downloaded by including the `--dependencies` flag. 
All downloaded files are placed in the OpenTAP installation directory. You can specify a different destination using the `--out` parameter.
If it points to a directory, all downloaded packages are placed in that directory instead.
If it specifies a filename, this will be the name of the first package specified, and the remaining packages will be placed in the same directory with their default name. 
Like the install action, the download action also supports specifying the desired os, version, and architecture.

`tap package download Python`.

### verify

Verify the integrity of a given package by computing the fingerprints of its locally installed files, and comparing them
with the fingerprints stored in the local XML package description file (`package.xml`).
If no package name is provided, all installed packages are verified.
This only works when run from the OpenTAP installation directory.

## Running Test Plans

The `run` commands executes a test plan.

```
> tap run -h

Usage: run [-h] [-v] [-c] [--settings <arg>] [--search <arg>] [--metadata <arg>] [--non-interactive] [-e <arg>] [-t <arg>] [--list-external-parameters] [--results <arg>] <Test Plan>
  -h, --help             Write help information.
  -v, --verbose          Show verbose/debug-level log messages.
  -c, --color            Color messages according to their severity.
  --settings             Specify a bench settings profile from which to load
                         the bench settings. The parameter given here should correspond
                         to the name of a subdirectory of <OpenTAP installation dir>/Settings/Bench.
                         If not specified, <OpenTAP installation dir>/Settings/Bench/Default is used.
  --search               Additional directories to be searched for plugins.
                         This option may be used multiple times, e.g., --search dir1 --search dir2.
  --metadata             Set a resource metadata parameter.
                         Use the syntax parameter=value, e.g., --metadata dut-id=5.
                         This option may be used multiple times.
  --non-interactive      Never prompt for user input.
  -e, --external         Set an external test plan parameter.
                         Use the syntax parameter=value, e.g., -e delay=1.0.
                         This option may be used multiple times, or a .csv file containing a
                         "parameter, value" pair on each line can be specified as -e file.csv.
  -t, --try-external     Try setting an external test plan parameter,
                         ignoring errors if it does not exist in the test plan.
                         Use the syntax parameter=value, e.g., -t delay=1.0.
                         This option may be used multiple times.
  --list-external-parameters List the available external test plan parameters.
  --results              Enable a subset of the currently configured result listeners
                         given as a comma-separated list, e.g., --results SQLite,CSV.
                         To disable all result listeners use --results "".
```

### Bench Settings

Specify a bench settings profile for the test plan being run. This refers to the configuration of DUTs, connections, and
instruments. The `--settings` parameter should be the name of a subdirectory of `<installation dir>/Settings/Bench`.
If not specified, `<installation dir>/Settings/Bench/Default` is used:

`tap run MyTestPlan.TapPlan --settings RadioTestSetup`.

### Plugin Search Path

By default, OpenTAP searches for plugins in the installation directory and in the `<installation dir>/Packages` directory.
More directories to be searched for plugins may be provided with multiple occourrences of the `--search` option,
one for each additional directory:

`tap run MyTestPlan.TapPlan --search C:\Users\Me\MyDut --search C:\Users\Me\MyInstrument`.

### Result Listeners

When running a test plan, OpenTAP enables by default all configured result listeners.
A subset of them may be enabled by giving their names in a comma-separated list in the `--results` option:

`tap run MyTestPlan.TapPlan --results SQLite,CSV`.

To disable all result listeners use `--results ""`.

### Non-Interactive Mode

Never prompt for user input:

`tap run MyTestPlan.TapPlan --non-interactive`.

### External Settings

Test step settings can be *parameterized* on a parent. This way, multiple child test steps in
a test plan can share the same setting, which can then be configured in one place. 

Because the test plan itself is the parent of all test steps, settings can naturally be
parameterized on that as well. Settings that are parameterized on the test plan are considered
*external*, and can be modified from outside the test plan. This makes it possible to reuse the
same test plans and easily run them with different parameters.

Imagine the following testplan:

```
Test Plan
   Delay -> Parameterize Time Delay on TestPlan as 'Parameters \ Time Delay 1'
   Delay -> Parameterize Time Delay on TestPlan as 'Parameters \ Time Delay 2'
   Delay -> Parameterize Time Delay on TestPlan as 'Parameters \ Time Delay 3'
```

Let's see how these external settings can be modified.

#### Modifying settings from the CLI

To see what external parameters the plan contains, run:

`tap run VariableDelay.TapPlan --list-external-parameters`

This should output something like:

```
Test Plan: VariableDelay
Listing 3 External Test Plan Parameters:
  Parameters \ Time Delay 1 = 0.1 s
  Parameters \ Time Delay 2 = 0.1 s
  Parameters \ Time Delay 3 = 0.1 s
```

Here we see they all have the default value `0.1 s`. Those values can be overriden when running the
test plan with the `-e` (`--external`) flag:

`tap run VariableDelay.TapPlan -e "Parameters \ Time Delay 1=0.2" -e "Parameters \ Time Delay 2=0.3"`

To verify that the parameters are set correctly, `--list-external-parameters` can be used in conjunction with `-e`:

`tap run VariableDelay.TapPlan -e "Parameters \ Time Delay 1=0.2" -e "Parameters \ Time Delay 2=0.3" --list-external-parameters`

```
Test Plan: VariableDelay
Listing 3 External Test Plan Parameters:
  Parameters \ Time Delay 1 = 0.2 s
  Parameters \ Time Delay 2 = 0.3 s
  Parameters \ Time Delay 3 = 0.1 s
```

Note that each parameter along with its assignment and value are quoted. This is required if the parameter name or the assigned value contains a space. 
In addition, there must be no space between the assignment operator (`=`) and the assigned value. In addition, units such as `s` in this case, should
be omitted.


Alternatively, if the [CSV](https://packages.opentap.io/index.html#/?name=CSV) plugin is installed,
you can create a csv file named `MyExternalParameters.csv` containing the parameters and values:

```csv
Parameters \ Time Delay 1,0.7
Parameters \ Time Delay 2,1.4
Parameters \ Time Delay 3,2.1
```

and then use it with:

`tap run VariableDelay.TapPlan -e MyExternalParameters.csv`.

#### Modifying settings from a Test Plan Reference

If a test plan with external parameters is used in a `Test Plan Reference` step,
its parameters will appear as regular step settings, and they can be easily modified.
This means that a collection of test steps can easily be abstracted away to appear as a single,
fully configurable test step.


### Metadata

Similarly to *External* settings, resource settings can be marked as *Metadata*. This could be the serial number of a DUT, for
instance. This option may be specified multiple times:

`tap run My.TapPlan --metadata dut1=123 --metadata dut2=456`.

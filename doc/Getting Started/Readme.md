# Getting Started


## Install OpenTAP


### Windows
1. Download OpenTAP from our homepage [here](https://www.opentap.io/download.html). 
2. Start the installer.

### Linux
When installing on Linux there are a few options:

#### Option 1 - .tar
1. Download the OpenTAP .tar package from our homepage [here](https://www.opentap.io/download.html). 
2. Untar the package in you home directory `tar -xf OpenTAP*.tar`
3. Change the permission of the INSTALL.sh file to be executable: `chmod u+x INSTALL.sh`

#### Option 2 - .dep
1. Download the OpenTAP .dep package from our homepage [here](https://www.opentap.io/download.html). 
2. `sudo apt install ./OpenTAP*.deb`

#### Option 3 - .rpm
1. Download the OpenTAP .dep package from our homepage [here](https://www.opentap.io/download.html). 
2. `sudo dnf install ./OpenTAP*.rpm`


## Cli Commands

Usage: `tap <command> [<subcommand>] [<args>]`

| Command | Description |
|------|--------|
| `tap` | Lists valid commands |
| `tap <command>` | Lists valid subcommands for the `<command>` commands |
| `tap <command> -h` | Writes help information for the `<command>` |

Below is a description of some common commands.


### run
The `run` commands executes a test plan. 

`tap run <file path> [<args>]`


### package install
The `install` commands installs one or more packages.

`tap package install <package name> [<args>]`

> Note: Upgrading OpenTAP is simple, just run `tap package install OpenTAP`


### sdk new
OpenTAP includes tools to generate new projects, project files (e.g. new TestStep or Instrument) and integration with other tools (e.g. VSCode or GitLab).

> Note: This command uses the sdk package that can be install with `tap package install SDK`.

`tap sdk new <command> [<subcommand>] [<args>]`

List valid subcommands with: `tap sdk new`.

> Note: Create a new plugin project with `tap sdk new project <project name>`

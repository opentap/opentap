# User Guide

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

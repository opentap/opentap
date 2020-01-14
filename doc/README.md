# OpenTAP

OpenTAP is an Open Source project for fast and easy development and execution of automated tests. 

OpenTAP is built with simplicity, scalability and speed in mind, and is based on an extendable architecture that leverages .NET Core. 
OpenTAP offers a range of sequencing functionality and infrastructure that makes it possible for you to quickly develop plugins tailored for your automation needs – plugins that can be shared with the OpenTAP community through the OpenTAP package repository. 


## Cli Commands


Usage: `tap <command> [<subcommand>] [<args>]`

| Command | Description |
|------|--------|
| `tap` | Lists valid commands |
| `tap <command>` | Lists valid subcommands for the `<command>` commands |
| `tap <command> -h` | Writes help information for the `<command>` |

Below is a non complete list of commmands.

### run
The `run` commands executes a test plan. 

`tap run <file path> [<args>]`

### package install
The `install` commands installs one or more packages.

`tap package install <package name> [<args>]`


> Note: Upgrading OpenTAP is simple, just run `tap package install OpenTAP`

### sdk new
OpenTAP includes tools to generate new projects, project files (e.g. new TestStep or Instrument) and integration with other tools (e.g. VSCode or GitLab).

`tap sdk new <command> [<subcommand>] [<args>]`

List valid subcommands with: `tap sdk new`.

> Note: Create a new plugin project with `tap sdk new project <project name>`

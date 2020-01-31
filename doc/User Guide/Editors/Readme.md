# Editors

As you may have noticed, a test plan consists of XML. You *could* modify and extend test plans by hand, but we recommend using the tools we have developed.

You currently have two options:
1. [Developer’s System Community Edition](https://www.opentap.io/download.html) - mature, feature-rich GUI (recommended)
2. [TUI](https://gitlab.com/OpenTAP/Plugins/opentap-tui/opentap-tui) - open source cross-platform text-based user interface for usage in terminals (beta)

## The responsibility of an editor
Editing test plans, providing a clear overview of what steps are available, safely making changes,
running testplans, clearly organizing the output of a testplan. E.g.
1. Provide a clear overview of what steps passed, and what steps failed
2. Display log output at various points throughout a run
3. Breaking down the data associated with a test run in a way that empowers the user to analyze the results (results viewer)

## Developer’s System Community Edition

Install the community edition version of the editor with the following command:

> tap package install "Editor CE"

and run it with `tap editor`.

## TUI

As mentioned in the previous section, `tap package install` installs the latest stable version. Since TUI is still in beta, there is no stable version.
Use the following command to install the latest version:
> tap package install TUI --version any

and run the program with `tap tui`.
# Editors

As previously noted, test plans are implemented in XML. You *could* modify and extend test plans manually, but we recommend
using available creation and editing tools.

There currently exist two options:
1. [Developer System](https://www.keysight.com/find/tapinstall) - mature, feature-rich GUI available with a commercial or community license (recommended)
2. [TUI](https://packages.opentap.io/index.html#/?name=TUI) - open source cross-platform text-based user interface for usage in terminals (beta)

## The function of an editor

Editing test plans, providing a clear overview of available steps, safely making changes,
running testplans, and clearly organizing the output of a testplan, i.e.,
1. Providing a clear overview of which steps passed, and which failed
2. Displaying log output at various points throughout a run
3. Outputting data associated with a test run to enable analysis of the results (results viewer)

## Developer System

Install the Developer System (commercial or community license) from the following page:

[https://www.keysight.com/find/tapinstall](https://www.keysight.com/find/tapinstall)

and run it with `tap editor` or `tap editorx` or launch from the app menu.

## TUI

As mentioned in the previous section, `tap package install` installs the latest stable version of OpenTAP and other packages. 
Use the following command to install the latest available version of TUI:

> tap package install TUI --version any

and run the program with `tap tui`.


<!-- Result viewers -->

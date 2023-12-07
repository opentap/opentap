# Editors

As you may have noticed, a test plan consists of XML. You *could* modify and extend test plans by hand, but we recommend
using the tools we have developed.

You currently have three options:
1. [Developer’s System](https://www.keysight.com/find/tapinstall) - mature, feature-rich GUI available with a commercial or community license (recommended)
2. [TUI](https://packages.opentap.io/index.html#/?name=TUI) - open source cross-platform text-based user interface for usage in terminals
3. [Editor X](https://packages.opentap.io/#name=%2FPackages%2FEditor+X) - cross-platform GUI similar to Developer's System with a commercial or community license

## The responsibility of an editor
Editing test plans, providing a clear overview of what steps are available, safely making changes,
running testplans, clearly organizing the output of a testplan. E.g.
1. Provide a clear overview of what steps passed, and what steps failed
2. Display log output at various points throughout a run
3. Breaking down the data associated with a test run in a way that empowers the user to analyze the results (results viewer)

## Developer’s System

Install the Developer's System (commercial or community license) from the following page:

[https://www.keysight.com/find/tapinstall](https://www.keysight.com/find/tapinstall)

and run it with `tap editor` or launch from the app menu.

## TUI

Use the following command to install the latest release:

> tap package install TUI

and run the program with `tap tui`.

## Editor X

Use the following command to install the latest release:

> tap package install 'Editor X'

and run the program with `tap editorx`.


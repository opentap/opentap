# Overview
This section introduces essential OpenTAP terminology, concepts, and tools. It is intended to provide users with an
 understanding of OpenTAP and its ecosystem to get started. For a more technical description, see the [Developer
 Guide](../../Developer%20Guide/Introduction/Readme.md). For a reference of CLI options, see the [documentation](../CLI%20Usage/Readme.md) on CLI usage.

OpenTAP consists of multiple tools, including:
-	OpenTAP - core engine
-	CLI - command line interface for installed plugins, and
-  Package Manager - a tool to manage installed plugins

This overview is dedicated to OpenTAP itself. A detailed description, along with common usage scenarios, of the latter
two tools will be given in the following chapters.

## Test Plans

A *test plan* is a sequence of test steps and their associated data. They are stored as XML files, and use the
".TapPlan" file extension. Test plans are created with an [editor](../Editors/Readme.md). They can be executed either in an
editor, or by using the `tap run` [CLI action](../CLI%20Usage/Readme.md#running-test-plans). A test step can be a child step, a parent step, or both.
Therefore, a test plan is a recursive structure of test steps. This hierarchy is illustrated in the figure below.

![](./TestPlan.svg)

The step sequence is ordered from top to bottom, and child steps are indented to indicate that they belong to a parent step.

The execution order of child steps is decided by the parent step. For example, two typical child execution orderings are *Parallel*
and *Sequential*, shown below. *MyStep* is given as an example to illustrate that the topology of step execution can be
controlled entirely by a parent step. Notice that the behavior of MyStep can be determined by putting a *Parallel* step in
a *Sequence* step.

![](./Flow.svg)

## Test Steps

A test step is an element that encapsulates some piece of functionality. It should perform a single *step* of the test
being run. The definition given is intentionally vague, as a step can perform myriad actions. It could make a
measurement using an instrument, or control a piece of hardware, such as adjusting fan speed or voltage. It could also
pause test execution, open a dialog window, make a web request, run a different program, or control the execution of
other steps.

The aforementioned *associated data* of test steps refers to *step settings* and
*resources*. Step settings affect the behavior of a step. All steps have three *common* settings: 
1. *Enabled* enables or disables the step. This setting is useful for temporarily disabling steps without making
destructive changes to a plan.
2. *Step Name* sets the name of the step. This name will be reflected in the test plan as well.
3. *Break Conditions* allows a step to override the break behavior specified in the engine settings.

In addition to these common settings, a step may provide its own settings. These vary widely in functionality. For a
*dialog step* that opens a dialog box, the message in the dialog box is controlled by a step setting. The buttons in
the dialog box (Yes / No / Cancel), and what happens when clicking those buttons are also controlled by step settings.

On the other hand, *Session Log* and *Result Listeners* are situated outside of the test plan because they interpret the
output of the test and do not influence it directly. Ensuring this decoupling between generation and interpretation of
results guarantees that, say, a new database can be added to store results without making any changes to test steps,
or their execution.

<!-- Result listeners are discussed in more detail in the [editor section](../Editors). -->

<!-- For further discussion of test steps, see the [test step discussion section](../Test%20Steps). -->

## Verdicts

Every test step has a verdict. However, this verdict is not a part of the actual test plan. Rather, it is *set* during
the execution of the test plan.

*NotSet* is the default verdict for all test steps. A step can *set* a verdict during execution to indicate success. A
test plan ouputs a verdict according to the verdicts of its steps. The verdict of a test plan is the most *severe*
verdict of its child steps. A verdict has one of 6 severities, detailed in the following table:

| Severity | Verdict      | Description                                                        |
|----------|--------------|--------------------------------------------------------------------|
| 1        | NotSet       | No verdict was set                                                 |
| 2        | Pass         | Step or plan passed                                                |
| 3        | Inconclusive | Insufficient information to make a decision either way             |
| 4        | Fail         | Step or plan failed                                                |
| 5        | Aborted      | User aborted test plan                                             |
| 6        | Error        | An error occurred. Check [session logs](#session-logs) for more information |

This hierarchy determines that, for a test plan to output a *Pass* verdict, at least one step must set its verdict to *Pass*, and
the rest must either set their verdict to *Pass* or *NotSet*. This choice represents the most typical behavior for parent steps
containing child steps; a *Sequential* step passes if all of its children pass. However, this is not a hard and fast rule: parent
steps decide overide the verdicts of their children and set their own verdict conditions.

*Break Conditions* operate based on step verdicts. The default *engine setting* is to abort the test plan if an *Error*
verdict is encountered, and continue otherwise. The following break conditions can be toggled independently:

1. *Break On Error*
2. *Break On Fail*
3. *Break On Inconclusive*

Overriding break conditions in a given step affects all child steps in the step's hierarchy.
The break conditions of a child step can be further overridden, affecting its child hierarchy, and so on.

When a break condition is triggered, execution of the current *hierachy* is interrupted. The verdict that triggered the break condition is
propagated upwards through the parent hierarchy until the break condition is no longer met. In other words, until the test plan is stopped,
or until a parent step is reached which does **not** break on the given verdict. 
In the latter case, test plan execution will resume from the next step in the sequence.

The following figure illustrates how break conditions affect test execution, and how verdicts are propagated
from child steps to parent steps:

![](./break_conditions.svg)

Steps run sequentially from top to bottom, and verdicts from child steps are propagated upwards to
their immediate parent. In this figure, the break condition on each step is specified on the right along with the source.
**Bold text** means the break condition is explicitly set for that specific step, which is then inherited by child
steps. Due to the *Break On Error* break condition, the last two steps are never executed in this example.

## Resources

OpenTAP is intended for software as well as hardware testing. The concept of Instruments and DUTs (Devices
Under Test) is central to OpenTAP. Most commonly, a DUT is a device under test, calibration, or control, and
an instrument is software or hardware that makes measurements. To integrate resources into test plans, whether to control them or
read their measurements, they must be connected to test steps that know how to communicate with them. In other words, a
resource driver is required. Creation of such a driver is described in the [Developer
Guide](../../Developer%20Guide/Introduction/Readme.md).

OpenTAP is quite flexible regarding resources. Typically, resources are local physical equipment. But they can easily
be more abstract, such as a virtual resource, or even remote ones. Depending on the use case, all of the
following scenarios are valid:

 1. Having no DUTs or instruments
 2. Using a single device as a DUT and an instrument simultaneously
 3. Using software resources as DUTs or instruments
 4. Using many DUTs and instruments

Out of the box, OpenTAP does not provide any resources for hardware control. For that, you need plugins.

## Result Listeners

Result listeners are notified whenever a test step generates log output, or publishes results.

OpenTAP ships with a simple result listener called *Text Log*, which saves log information from test plan execution to a
file. Its behavior can be modified via settings, including changing the file name and location of the log, as well as
filtering the log information. Logged information falls into 4 message categories: Errors, Warnings, Information, and Debug, each of which
can be checked and unchecked.

Analogous to *Text Log*, there is a result listener plugin named *CSV*. Rather than logging information, this plugin
saves all results published by test steps to a CSV file, which can also be changed via settings.

There are also PostgreSQL and SQLite result listeners that store all results and logs from a test plan run, as well as a
copy of the test plan itself, making it possible to restore and run old versions of a test plan. The PostgreSQL
plugin be configured to use either a local database, or publish to a remote one.

There is no limit to the number of active result listeners, and OpenTAP supports multiple result listeners of the same type.
For example, you could create two different *Text Log* listeners collecting Debug and Information log messages,
respectively. There are more result listeners available in the package repository, and developers can [make new result
listener plugins](../../Developer%20Guide/Result%20Listener/Readme.md#custom-result-listeners) to suit their needs, such as
integrating with a different database technology, or uploading test results to a server.

Result listeners are tied to an OpenTAP installation, and not a test plan. Result listener settings are stored
in `<install dir>/Settings/Results.xml`. As with test plans, editing these by hand is not recommended. However, result listener settings can be
generated and edited using a test plan editor.
<!-- Creation and further usage of result listeners will be covered in more detail in the [editor section](../Editors). -->

## Session Logs

OpenTAP retains extensive session logs for debugging purposes. Session logs are kept from the 20 latest OpenTAP sessions, and are created independently of result listeners. The maximum amount of storage space used for session log files is 2GB, with the largest size of a single file being 100MB. When this limit is reached, a new log file is created. There is a maximum of 20 files kept at any given time. If the number of session log files exceeds 20 or the total file size surpasses 2GB, log files will be removed starting with the oldest file.

The content of the session log files is similar to the logs collected by *Text Log*, but these files also diagnostic messages from OpenTAP and any plugins installed. Session logs can be found in `<install dir>/SessionLogs`, and are named with the time and date at which they were created.

The session logs contain the same information you would see in a terminal when running OpenTAP with the `--verbose` flag. If you encounter errors, the logs can contain information for discovering the cause of the error. If you think you discovered a bug in OpenTAP, please file an issue on [GitHub](https://github.com/opentap/opentap). If applicable, please include instructions on how to
replicate the issue, as well as relevant logs.

## Packages and Plugins

OpenTAP can be extended with plugins. A plugin usually provides a single piece of functionality. For example, a plugin could
provide a new test step, or a new result listener. Plugins come in packages. A package is a versioned collection of
plugins. Plugins vary widely in the functionality they provide.

Some examples are:
 - [GUI editors](../Editors/Readme.md#developer’s-system-community-edition) for creating and running test plans
 - SDK plugins to aid in developing and debugging plugins
 - Tools for analyzing test plans in real time to discover performance bottlenecks (Timing Analyzer)
 - REST interfaces to OpenTAP to allow remote control (REST-API)

Installing, uninstalling, upgrading, downgrading, and managing dependencies are all handled by the OpenTAP package manager. A
package declares its own dependencies and OS / CPU compatibility. The package manager resolves those dependencies
among the packages in your current installation, when possible. Usage of the package manager is described in detail in [the
next section](../CLI%20Usage/Readme.md#using-the-package-manager).

The figure below shows the relation between plugins and OpenTAP. OpenTAP is at the center, and plugins, providing a
variety of functionality, can be added and removed at will. 

![](./OpenTAP_plugins.svg)

Check out the public OpenTAP package repository [here](http://packages.opentap.io/index.html#/?name=OpenTAP) to browse available
plugins.

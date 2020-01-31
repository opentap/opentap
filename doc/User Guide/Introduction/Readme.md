# Overview
This section introduces essential OpenTAP terminology, concepts, and tools. It is intended to provide users (non-developers) with an understanding of OpenTAP and its ecosystem to get started.

For a quick reference of CLI options, see the [comprehensive reference](../CLI%20Reference). For a more technical description, see the developer guide.

OpenTAP consists of multiple tools, including:
-	OpenTAP - core engine
-	CLI - command line interface for installed plugins, and
-   Package Manager - a tool to manage installed plugins

This overview is dedicated to OpenTAP itself. A detailed description, along with common usage scenarios, of the latter two tools will be given in the following chapters.

### Test plans

The below figure highlights the key elements of a test plan, all of which will be covered shortly.
![](./TestPlanIllustration.png)

A *test plan* is a sequence of test steps and their associated data. They are stored as XML files, and use the ".TapPlan" file extension. Test plans are created with an [editor](../Editors). They can be executed either in an editor, or by using the `tap run` [CLI action](../CLI%20Usage).

The verdict of the test plan is set based on the verdicts of its individual test steps, each of which generate their own verdict.
It may be useful to consider a test plan as a tree, and test steps as branches off the tree, that may themselves have branches. Steps added to the top level of a test plan are executed sequentially. 
However, steps may be added to the test plan which can have embedded steps themselves. Consider, for instance, a "sequential step". This test step runs all of its child steps in sequence, and selects the most "severe" verdict of its child steps. 
Typically, but not always, verdicts are propagated upwards in the tree, prioritizing the most severe verdicts. In other words, a test plan outputs the "Fail" verdict if any of its immediate test steps fail, and likewise, a sequential test step fails if any of its immediate children fail.

Like OpenTAP itself, test plans are designed for reuse, and minimizing the amount of work required of the user. For example, it is possible to run a test plan with a range of values using the [sweep loop](todosweep_loop) test step. In addition, many test step attributes can be marked as [external parameters](../cli%20usage/#external-settings), allowing you to assign their values when the plan is loaded, instead of editing the plan itself. It is also possible to add a [Test Plan Reference](todotest-plan-reference-link) as a test step, essentially allowing you to embed another test plan within your plan. This is intended to minimize complexity, and encourage modular, self-contained test plans.

The way verdicts are propagated can of course be modified by plugins. For instance, a custom sequential step which passes if any of its child steps pass can be implemented. This is covered in the [developer guide](../../developer%20guide/test%20step). 

A verdict has one of 6 values with varying "severity", detailed in the table below. 
| Severity | Verdict      | Description                                                        |
|----------|--------------|--------------------------------------------------------------------|
| 1        | NotSet       | No verdict was set                                                 |
| 2        | Pass         | Step or plan passed                                                |
| 3        | Inconclusive | Insufficient information to make a decision either way             |
| 4        | Fail         | Step or plan failed                                                |
| 5        | Aborted      | User aborted test plan                                             |
| 6        | Error        | An error occurred. Check [logs](#log%20files) for more information |


### Resources

OpenTAP is intended for software as well as hardware testing. The concept of Instruments and DUTs are essential for OpenTAP, 
In the classical case, the DUT is the device being tested, calibrated, or controlled, and an instrument is anything that makes measurements.
However, OpenTAP is quite flexible, and these entities can therefore be considered more abstractly.
Depending on your use case, the following scenarios are valid:

 1. Having no DUTs or instruments
 2. Using a single device as a DUT and an instrument simultaneously
 3. Using software resources as DUTs or instruments
 4. Using many DUTs and instruments

Out of the box, OpenTAP does not support any hardware. For that, you need plugins.

### Result listeners
> Coming soon
### Test plan editors
> Coming soon
### Result viewers
> Coming soon
### Plugins

OpenTAP can be extended by installing plugins. Plugins range widely in the additional functionality they provide. Some examples are:
 - GUI editors for creating and running test plans
 - SDK plugins to aid in developing plugins
 - Tools for analyzing test plans in real time to discover performance bottlenecks
 - REST interface to OpenTAP to allow you to control it remotely

Installing, uninstalling, upgrading, downgrading, and dependencies are all managed by the OpenTAP package manager. 
Usage of the package manager is described in detail in [the next section](../cli%20guide/package%manager). 

The below figure shows the relation between plugins and OpenTAP. OpenTAP is at the center, and plugins, providing a variety of functionality, can be added and removed painlessly.
![](./TAParchitecture.png#width=600)

All plugins depend on OpenTAP, and plugins may or may not depend on each other. The package manager automatically resolves these dependencies, if a resolution exists.

Check out our public package repository [here](http://packages.opentap.io/index.html#/?name=OpenTAP) to browse available plugins.


### Log files

OpenTAP keeps extensive logs for debugging purposes. Logs are kept from the 10 latest OpenTAP instances launched. They can be found in `%TAP_PATH/SessionLogs%`, and are named after the time and date at which they were created. They contain the same information you would see in your terminal when running tap with the `--verbose` flag. If you encounter errors, the logs may contain useful information in discovering what went wrong. If you think you discovered a bug in OpenTAP, please file an issue on [gitlab](https://gitlab.com/OpenTAP/opentap). If applicable, please include instructions on how to replicate the issue, as well as relevant logs.

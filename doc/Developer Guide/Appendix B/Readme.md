Appendix B: Macro Strings
=========================

Sometimes certain elements need customizable text that can dynamically change depending on circumstances. These are known as macros and are identifiable by the use of the `<` and `>` symbols in text. Macros can be expanded by the plugin developer to use other macros with different values depending on the context. The class used for macro string properties is called `OpenTAP.MacroString`.

One example is the `<Date>` macro that is available to use in many Result Listeners, like the log or the CSV result listeners. Another example is the `<Verdict>` macro. These are both examples of macros that can be inserted into the file name of a log or CSV file like so: `Results/<Date>-<Verdict>.txt`. If you insert `<Date>` in the file name the macro will be replaced by the start date and time of the test plan execution.

When used with the MetaData attribute, a property of the ComponentSettings can be used to define a new macro. For example, all DUTs have an ID property that has been marked with the attribute `[MetaData("DUT ID")]`. This means that you can put `<DUT ID>` into the file path of a Text Log Listener to include the DUT ID in the log file's name.

In addition to macros using `<>`, environment variables such as `%USERPROFILE%` will also be expanded.

There are a few different contexts in which macro strings can be used.

## Test Steps

MacroStrings can be used in test steps. In this context the following macros are available:

- `<Date>`: The start date of the test plan execution
- `<TestPlanDir>`: The directory of the currently executing test plan
- MetaData attribute: Defines macro properties on parent test steps

Verdict is not available as a macro in the case of test steps, because at the time of execution the step does not yet have a verdict. However, it can be manually added by the developer if needed. In this case it is up to the plugin developer to provide documentation.

Below is an example of MacroString used with the `[FilePath]` attribute in a test step. This attribute provides the information that the text represents: a file path. In the GUI Editor this results in the `"..."` browse button being shown next to the text box.

```cs
public class MyTestStep: TestStep {

  [FilePath] // A MacroString that is also a file path.
  public MacroString Filename { get; set; }

  public MyTestStep(){
    // 'this' useful for TestStep instances.
    // otherwise a MacroString can be created without constructor arguments.
    Filename = new MacroString(this) { Text = "MyDefaultPath" };
  }
  public override void Run(){
     Log.Info("The full path was '{0}'.", Path.GetFullPath(Filename.Expand(PlanRun)));
  }
}
```

## Result Listeners

Result listeners have access to TestStepRun and TestPlanRun objects which contain variables that can be used as macros. An example is the previously mentioned DUT ID property, which is available if a DUT is used in the test plan. The following macros are available in the case of result listeners:

- `<Date>`: The start date and time of the executing test plan.
- `<Verdict>`: The verdict of the executing test plan. Only available in OnTestPlanRunCompleted.
- `<DUT ID>`: The ID (or ID's) of the DUT (or DUTs) used in the test plan.
- `<OperatorName>`: Normally the name of the user on the test station.
- `<StationName>`: The name of the test station.
- `<TestPlanName>`: The name of the executing test plan.
- `<ResultType>` (CSV only): The type of result being registered. This macro can be used if it is required to create multiple files, one for each type of results per test plan run.

## Other Uses

Macro strings can also be used in custom contexts defined by a plugin developer. In this case it is up to the plugin developer to provide documentation of the available macros.

One example is the session log. It can be configured in the **Engine** pane in the **Settings** panel. The session log only supports the `<Date>` macro, which is defined as the start date and time of the OpenTAP instance and not the test plan run. This is because the session is active for multiple test plan runs and needs to be loaded when OpenTAP starts, therefore, most macros are not applicable.

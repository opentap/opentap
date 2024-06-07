Macro Strings
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

# Threading and Parallel Processing

Threading and parallelism are essential tools for enhancing the performance of test plan execution. By default, a test plan executes in a single thread, but it can branch off into multiple parallel threads as it progresses. Utilizing logging or Result Listeners can cause certain actions to execute in separate threads.

However, parallelism comes with some limitations due to the inherent complexity of managing multiple threads. 

In OpenTAP and C#, parallelism can be implemented in several ways:

- **Parallel Steps**: The simplest form of parallelism, allowing test steps to be executed concurrently.
- **Deferred Processing**: Enables the processing of results in a separate thread.
- **TapThreads**: OpenTAP's own thread pool, which manages parent and child threads.
- **.NET Threads**: Basic threading provided by the .NET framework.
- **.NET Tasks**: Lightweight threads, also provided by the .NET framework.

## Parallel Steps

The parallel step is a test step from the OpenTAP basic plugins. It runs all the child steps in parallel threads. 

In the below screen shot, you can see four delay steps running in parallel inside a Parallel step:

![Four Delays](./4delays.png)

It is best to use this step in a configuration where the resources used are not interferring with each other. 


For example, you can have one branch setting up an instrument and one configuring a DUT. If threads needs to access the same instrument at the same time you might get unexpected behavior due to race conditions. 

In order to control this, you can use the Lock step, which locks a named local or system-wide mutex.

In the below screenshot you can see how parallel steps with locks are evaluated. Notice that the total time was 3x the delay because none of the steps were executed in parallel.

![alt text](3lockedDelays.png)


## Deferred Processing

Deferred results processing makes it possible to do post-processing of results while the test plan execution continues. This is a limited kind of parallelism that is useful when the performance is bound by data acquisition and data processing sequentially and you have spare computational resources. The effects are greatest when the amount of processing time is considerably greater than the measurement time, but for shorter processing times it can be useful too.

The following illustrates the order of operations in traditional sequential processing:

![](Sequential.svg)

This diagram illustrates the order of operations when defered processing is used:

![](parallel.svg)


In KS8400 a visualization of the parallelism has been added so that you can get a bit of insight into the speed improvements. In the below picture you can see three Measurement + Process steps which has a blocking measurement part and a non-blocking processing part. Notice the bars in the Flow column. The visualization shows the blocking part of the executing in blue and the non-blocking part in dark gray.

![Deferred parallelism](DeferredParallelism.png)

To add deferred processing inside a test step Run method, use the Results.Defer method as shown in the example below:

```cs
// ... This goes inside a Test Step implementation ...
public override void Run()
{
    // execute the blocking part of the test step
    double[] data = instrument.DoMeasurement();

    Results.Defer(() => {
      // Inside this anonymous function the non-blocking part of the execution will be executed.
      var processedData = ProcessData(data);
      Results.Publish(processedData);
      var limitsPassed = CheckLimits(processedData);
      if(limitsPassed)
        UpgradeVerdict(Verdict.Pass);
      else
        UpgradeVerdict(Verdict.Fail);
    });
}
```




## OpenTAP Threads

OpenTAP Threads are analogous to .NET Threads, but have some key differences that makes them easier to use in a OpenTAP Plugin.

- Thread Pools: When somebody requests to execute a function, it either takes a thread from the pool and uses that or starts a new thread. When the function is complete the thread is added back into the pool. Compared to .NET Thread Pools, they are much quicker to start.
- They are heirarchical: When the thread is activated, thread-local storage is used to keep track of which thread started it. That means that various kinds of data can be shared between heirarchies of threads. The class ThreadHeirarchyLocal is a way to share data between threads. When a OpenTAP thread is aborted, the child threads also gets the signal to abort. This is also used for Sessions.

To start a TapThread, simply call `TapThread.Start()`.

```c#
TapThread.Start(() =>
{
    // Do something time consuming
    TapThread.Sleep(100);
    // thread finishes now. (Thread is donated back to the pool)
});
```

Since the thread is taken from a pool of live threads, they will normally start almost instantly.

If you want some result from your thread thread, we strongly recommend to use the TaskCompletionSource object. Note, there are many ways this can be done, but TaskCompletionSource is a very flexible way to do it.

```c#
// this is called a promise in many programming languages.
var promise = new TaskCompletionSource<double>();
TapThread.Start(() =>
{
    // do something time consuming.
    try
    {
       TapThread.Sleep(100);
       promise.SetResult(9000.0);
    }
    catch (Exception e)
    {
       promise.SetException(e);
    }
});

// wait for the result and possibly throw an exception on failure.
double resultValue = promise.Task.Result;

```


## .NET Threads and Tasks

Generally speaking, we don't recommend using the default .NET Threads and Tasks. Threads are expensive to start and tasks have surprising behaviors that make them unsuitable for many use cases.

For OpenTAP plugins, We recommend using the other techniques for parallelism unless strictly necessary. 
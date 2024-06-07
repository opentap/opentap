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

Deferred results processing enables post-processing of results while the test plan execution continues. This approach is a form of limited parallelism, beneficial when performance is constrained by sequential data acquisition and processing, and there are spare computational resources available. Deferred processing is most effective when processing time significantly exceeds measurement time, but it can also be useful for shorter processing tasks.

### Sequential Processing

In traditional sequential processing, the order of operations is as follows:

![Sequential Processing](Sequential.svg)

### Deferred Processing

When deferred processing is used, operations are handled in parallel, as shown in the diagram below:

![Parallel Processing](parallel.svg)

### Visualization in KS8400

In KS8400, you can visualize the parallelism to gain insights into the performance improvements. The image below shows three Measurement + Process steps with a blocking measurement part and a non-blocking processing part. The Flow column's bars indicate the blocking part of the execution in blue and the non-blocking part in dark gray.

![Deferred Parallelism](DeferredParallelism.png)

### Implementing Deferred Processing

To incorporate deferred processing within a test step's `Run` method, use the `Results.Defer` method. The example below demonstrates this:

```csharp
// This goes inside a Test Step implementation
public override void Run()
{
    // Execute the blocking part of the test step
    double[] data = instrument.DoMeasurement();

    Results.Defer(() => {
      // The non-blocking part of the execution is handled inside this anonymous function
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

In this example:
1. **Blocking Measurement**: The test step performs a measurement that blocks further execution.
2. **Deferred Processing**: The `Results.Defer` method queues the non-blocking processing operations to be executed concurrently.
3. **Processing**: Inside the deferred anonymous function, data is processed, results are published, and limits are checked.
4. **Verdict Upgrading**: Based on the processed data, the test verdict is upgraded to `Pass` or `Fail`.

By using deferred processing, you can optimize test execution, reducing the overall test plan duration and improving resource utilization.


## TapThreads

TapThreads function similarly to .NET Threads but include additional features tailored for OpenTAP plugins, enhancing their efficiency and manageability within the OpenTAP environment.

- **Thread Pools**: When a function is requested for execution, a thread is either retrieved from the pool or a new one is started. Once the function completes, the thread is returned to the pool. Unlike .NET Thread Pools, TapThreads are more proactive in starting new threads and are optimized for IO-bound applications, ensuring minimal latency and high performance in handling asynchronous tasks.

- **Hierarchical Structure**: TapThreads utilize thread-local storage to track the initiating thread, enabling data sharing across thread hierarchies. The `ThreadHierarchyLocal` class facilitates this data sharing. If an OpenTAP thread is aborted, its child threads also receive the abort signal. This mechanism is crucial for managing Sessions and maintaining data integrity across different layers of the thread hierarchy.

### Starting a TapThread

To start a TapThread, use `TapThread.Start()`, which provides an easy-to-use interface for running tasks asynchronously.

```csharp
TapThread.Start(() =>
{
    // Perform a time-consuming task
    TapThread.Sleep(100);
    // Thread finishes and is returned to the pool
});
```

Threads from the pool start almost instantly due to the pre-allocation and management of live threads, ensuring efficient task execution.

### Obtaining Results

To obtain results from your thread, use the `TaskCompletionSource` object. This approach provides a robust and flexible way to handle asynchronous operations and their outcomes.

```csharp
var promise = new TaskCompletionSource<double>();
TapThread.Start(() =>
{
    try
    {
        TapThread.Sleep(100); // Throws an exception if the thread is aborted.
        promise.SetResult(9000.0);
    }
    catch (Exception e)
    {
        promise.SetException(e);
    }
});

double resultValue = promise.Task.Result;
```

Using `TaskCompletionSource` ensures that your asynchronous code can handle both successful completion and exceptions in a structured manner.

Note, you can also use other synchronization mechanisms for getting the result, but this method is very robust and performant.

### Sleeping

You can make the current thread sleep with `TapThread.Sleep(TimeSpan duration)` or `TapThread.Sleep(int milliseconds)`. This pauses the thread for at least the specified time but may take a few extra milliseconds to wake up, making it unsuitable for highly precise waits.

```csharp
TapThread.Sleep(100); // Sleep for 100 milliseconds
```

If the thread is aborted while sleeping, an `OperationCancelledException` will be thrown. To ensure the thread sleeps for the specified duration regardless of abort signals, use `System.Threading.Thread.Sleep()`:

```csharp
System.Threading.Thread.Sleep(100); // Uninterruptible sleep
```

### Aborting

TapThreads can be aborted using the `TapThread.Abort()` method. This event propagates to all child threads, which also receive the abort notification. This is a 'soft' abort, meaning the threads must cooperate to be aborted. To detect if a thread has been aborted, you have several options:

1. **Throw an Exception**: Use `TapThread.ThrowIfAborted()` to throw an exception if the current TapThread has been aborted.
   ```csharp 
   TapThread.ThrowIfAborted();
   ```
2. **Check Abort Status**: Check if the thread is aborted via `TapThread.Current.AbortToken.IsCancellationRequested`.
   ```csharp
   if (TapThread.Current.AbortToken.IsCancellationRequested)
   {
       // Handle abort
   }
   ```
3. **Use AbortToken**: Use the `TapThread.Current.AbortToken` with calls that support it. Various .NET APIs accept a `CancellationToken` to enable canceling long-running operations.
   ```csharp
   var token = TapThread.Current.AbortToken;
   // Example with Task.Delay
   await Task.Delay(1000, token);
   ```
4. **Register an Event**: Register an event to occur when the thread is aborted:
   ```csharp
   using (TapThread.Current.AbortToken.Register(() =>
   {
       Log.Info("The thread was aborted!");
   }))
   {
       // Do something that takes time.
   }
   ```

These options provide flexibility in managing thread termination and ensuring resources are cleaned up properly.

### Creating New Thread Contexts

In rare cases, you might need to run code in a context that cannot be aborted or can be aborted separately. For this, use `TapThread.WithNewContext`. It runs inside the same physical thread but creates a new temporary context where some code can be executed. You can also control which parent thread the new context has.

```csharp
var firstThread = TapThread.Current;
TapThread.WithNewContext(() =>
{
    var secondThread = TapThread.Current;
    // firstThread != secondThread
}, 
// Specify the parent thread. Null means the root thread of the application.
null);
```

Creating new thread contexts allows for isolated execution environments within the same physical thread, providing greater control over task execution and abortion behavior.

## .NET Threads and Tasks

Generally, using the default .NET Threads and Tasks is not recommended. Threads are expensive to start, and tasks can exhibit unexpected behaviors that make them unsuitable for many use cases.

For OpenTAP plugins, it is recommended to use other parallelism techniques unless .NET Threads or Tasks are strictly necessary.
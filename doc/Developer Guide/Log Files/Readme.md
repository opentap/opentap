Log Files
=========
OpenTAP produces two kinds of log files: **session logs**, which cover everything that happens while an OpenTAP process is running, and **test plan run logs**, which capture the log activity for a single run of a test plan. Both are built on the same logging system that plugins use to emit log messages and that custom log listeners can subscribe to.

## Using Logging in Code

OpenTAP plugins emit log messages through a `TraceSource`. Any class deriving from `Resource` (such as `Instrument` and `Dut`) or `TestStep` already has a `Log` property whose source name reflects the resource or step name, so you can log directly without creating a source:

```csharp
using OpenTap;

public class MyInstrument : Instrument
{
    public void DoWork()
    {
        Log.Debug("Starting work");
        Log.Info("Connected to {0}", Address);
        Log.Warning("Voltage near limit");

        try
        {
            // ...
        }
        catch (Exception ex)
        {
            Log.Error(ex); // logs the message and stack trace
        }
    }
}
```

For classes that do not derive from `Resource` or `TestStep`, create your own `TraceSource` with `Log.CreateSource`. The convention is a `static readonly` source per class, named after the component:

```csharp
static readonly TraceSource log = Log.CreateSource("MyComponent");
```

The source name appears in every log line produced by that source, which makes it easy to identify where a message came from when reading the logs.

### Log levels

Messages are logged at one of four levels, defined by `LogEventType`:

| Level | Method | Use for |
| ---- | ---- | ---- |
| Error | `log.Error(...)` | Recoverable errors. |
| Warning | `log.Warning(...)` | Noncritical problems. |
| Information | `log.Info(...)` | General informational messages. |
| Debug | `log.Debug(...)` | Detailed debugging traces. |

All level methods accept a format string with arguments (`log.Info("value = {0}", x)`). Overloads that take a `Stopwatch` or `TimeSpan` append the elapsed time to the message, which is useful for timing operations:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... work ...
log.Debug(sw, "Measurement complete"); // appends e.g. "[1.2 s]"
```

> The logging system runs asynchronously by default. Call `Log.Flush()` to block until all pending log events have reached every listener.

## Reading the Logs

Each log line written to a file has the following format, with fields separated by semicolons:

```
<timestamp> ; <source> ; <level> ; <message>
```

For example:

```
2026-05-29 14-30-00.123 ; MyInstrument ; Information ; Connected to GPIB0::1::INSTR
```

In a session log the timestamp is absolute, while in a test plan run log it is relative to the start of the run (`hh:mm:ss.ffffff`). The most recent session log is always available through the `Latest.txt` hardlink in the `SessionLogs` folder, which is the quickest way to find the current log while OpenTAP is running.

> When a message was logged with an elapsed time (using a `Stopwatch` or `TimeSpan` overload), the duration is appended to the message in square brackets, for example `Measurement complete [1.2 s]`. This makes it easy to spot how long individual operations took when reading the logs.

## Session Logs

A session log records all events that occur during the startup and shutdown of an OpenTAP session, as well as the log output of every test plan run executed during that session. A session starts when an OpenTAP process is launched and ends when it closes. A new session log is created for each session.

### Location and naming

By default, session logs are written to the `SessionLogs` folder of the OpenTAP installation directory. The file name is based on the entry executable and the process start time, for example:

```
<TapDir>/SessionLogs/tap 2026-05-29 14-30-00.txt
```

If the chosen file name is already in use by another concurrent OpenTAP process, that file is locked, so OpenTAP appends an integer and tries again (`tap 2026-05-29 14-30-00_1.txt`, and so on) until it finds a free name. This is separate from size rollover: when a single log file exceeds its size limit during a running session, logging rolls over to a new file with a `__N` suffix (`tap__1.txt`). A hardlink named `Latest.txt` always points at the most recent session log for convenience.

## Test Plan Run Logs

In addition to the session log, OpenTAP captures the log output of each individual test plan run. This per-run log is collected while the plan executes and is delivered to every result listener through the `logStream` argument of `OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)`. Unlike session logs, the timestamps in a run log are relative to the start of the run.

Unlike the session log, the test plan run log has no size limit and is stored on the drive while the run is in progress.

The built-in **Log** result listener (`OpenTap.LogResultListener`, displayed as "Text Log") writes this stream to a text file, creating one file per test plan run. Its file path supports macros such as `<Date>` and `<Verdict>` and defaults to:

```
Results/<Date>-<Verdict>.txt
```

The Log result listener can also filter the output by log level (Verbose/Debug, Info, Warnings, Errors). If no result listeners are configured for a run, OpenTAP adds a single Log result listener by default. The produced file is published as an artifact of the run.

To customize how run logs are stored, implement a custom `ResultListener` and read the `logStream` in `OnTestPlanRunCompleted`. See the [Result Listener](../Result%20Listener/Readme.md) guide for details.

## Session Log Retention and Size Limits

OpenTAP automatically removes old session logs to keep disk usage bounded. The behavior is controlled by three limits. When the number of files or the total size exceeds the configured limit, the oldest files are deleted. The two most recent log files are always kept, even if they exceed the limits.

Each limit can be overridden using an environment variable.

| Limit | Description | Default | Override Environment variable |
| ---- | ---- | ---- | ---- | 
| Max number of files | Maximum number of session log files kept at a time. | 100 | `OPENTAP_SESSION_LOG_MAX_FILES` |
| Max total size | Maximum combined size (in bytes) of all session log files. | 10,000,000,000 (10 GB) | `OPENTAP_SESSION_LOG_MAX_TOTAL_SIZE` |
| Max file size | Maximum size (in bytes) of a single session log file before it rolls over. | 100,000,000 (100 MB) | `OPENTAP_SESSION_LOG_MAX_FILE_SIZE` |

## Initializing Session Logging

Session logging is normally initialized automatically by the OpenTAP startup process. When hosting OpenTAP in your own application you can control it through the static `OpenTap.SessionLogs` class:

```csharp
// Initialize using the default file name (SessionLogs/<exe> <timestamp>.txt).
SessionLogs.Initialize();

// Or initialize with a specific path.
SessionLogs.Initialize("MyApp.log");

// Get the path of the current session log file.
string path = SessionLogs.GetSessionLogFilePath();

// Move/rename the current session log to a new path.
SessionLogs.Rename("logs/MyApp-final.txt");

// Flush buffered log output to disk (useful as a last action before exiting).
SessionLogs.Flush();
```

## Creating a Log Listener

To process log events yourself - for example to forward them to an external system or read the logs programmatically - implement the `OpenTap.Diagnostic.ILogListener` interface and register it with `Log.AddListener`.

```csharp
using System.Collections.Generic;
using OpenTap;
using OpenTap.Diagnostic;

public class CountingListener : ILogListener
{
    public int ReceivedMessages;

    public void EventsLogged(IEnumerable<Event> events)
    {
        foreach (var evt in events)
        {
            ReceivedMessages++;
            // evt.Source     - the log source name
            // evt.EventType  - the level (Error=10, Warning=20, Information=30, Debug=40)
            // evt.Message    - the formatted message
            // evt.Timestamp  - in ticks
            // evt.DurationNS - event duration in nanoseconds (0 if none)
        }
    }

    public void Flush() { }
}
```

Register and unregister the listener with the static `Log` class:

```csharp
var listener = new CountingListener();
Log.AddListener(listener);
try
{
    // ... run code that produces log output ...
    Log.Flush(); // ensure all events have been delivered
}
finally
{
    Log.RemoveListener(listener);
}
```

`EventsLogged` is called with a batch of one or more `Event` values; `Flush` is called when the listener is asked to flush its output.

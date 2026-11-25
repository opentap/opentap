# Opting into a separate OpenTAP assembly graph

Existing entry points keep their normal assembly-loading behavior. On .NET 9 or
newer, an application can explicitly reload itself and the engine into a shared
OpenTAP load context:

```csharp
static void Main(string[] args)
{
    var options = new OpenTap.ApplicationHostOptions.Builder()
        .WithEntryPoint(typeof(EditorEntryPoint), nameof(EditorEntryPoint.EntryPoint))
        .WithArgument(serializedConfiguration)
        .Build();
    using (OpenTap.Hosting.ApplicationHost.Run(options))
    {
        // The instance remains alive. This body still executes in the caller.
    }
}

public static class EditorEntryPoint
{
    public static object EntryPoint(object argument)
    {
        // Deserialize inside this context, using its selected dependency versions.
        var configuration = (string)argument;
        // Configure and run the application here using normal OpenTAP APIs.
        return null; // Or return a shared-runtime result to the caller.
    }
}
```

`tap/Program.cs` is the reference implementation. `Main` only performs the
handoff; it locates `OpenTap.Cli.dll` by path and invokes `OpenTap.Cli.TapEntry.Go(object)`
in the new graph, without loading the CLI assembly into the caller.

## Deployment and initialization

The OpenTAP package ships `OpenTap.Runtime.dll` under `Packages/OpenTAP/`.
ApplicationHost looks there first, with a fallback beside `OpenTap.dll` for plain
MSBuild outputs. Reference `OpenTap.Runtime.dll` to use
`OpenTap.Hosting.ApplicationHost.Run`, `ApplicationHostOptions`, and `ApplicationHostContext`;
these APIs target .NET 9+ and are not present in the .NET Standard engine.
The engine retains `OpenTap.ApplicationContext` for context-local environment and lifecycle
notifications. Hosting is never enabled automatically by detecting the runtime.
When the runtime DLL lives under Packages/OpenTAP and no deps.json is used, load it
before JITting code that calls the hosting API (as demonstrated in tap/Program.cs).

The engine and entry assembly must be file-backed. `ApplicationHostOptions.Builder`
produces immutable snapshots; later changes to a builder do not alter existing options.
The argument itself is not copied or serialized. Run accepts options only; both entrypoint forms are selected through the builder.

To avoid loading the entry assembly in the parent at all, use the file-based entrypoint:

```csharp
var options = new ApplicationHostOptions.Builder()
    .WithEntryPoint(editorAssemblyPath, "MyEditor.EditorEntryPoint", "Run")
    .WithArgument(serializedConfiguration)
    .Build();
using (OpenTap.Hosting.ApplicationHost.Run(options)) { }
```

The type name is resolved only within that exact child-loaded assembly, not through
process-wide type lookup. Both forms require a public or internal static, non-generic
`object Run(object)` method. Paths are made absolute by Build; file existence is
checked when Run starts. Shared assemblies are not configurable.

The engine and entry assembly must be file-backed. By default, the installation
base is the directory containing the calling engine assembly. Select a deployed
image explicitly with builder options:

```csharp
var options = new ApplicationHostOptions.Builder()
    .WithEntryPoint(typeof(EditorEntryPoint), nameof(EditorEntryPoint.EntryPoint))
    .WithArgument(serializedConfiguration)
    .WithBaseDirectory(imageDirectory)
    .Build();
using (OpenTap.Hosting.ApplicationHost.Run(options))
{
}
```

This loads `OpenTap.dll` from the selected installation,
sets its base directory before engine initialization, and discovers plugins only
in that installation's search tree. The entry assembly is still reloaded from
the supplied type's location. `Installation.Current` uses the selected directory,
not process-wide installation environment variables. Place application/plugin
managed dependencies in that installation's search tree (including Dependencies
subdirectories). The entry assembly itself is loaded from the supplied type's
assembly location, even when outside that tree.

The supplied Type only identifies what to reload. The original type is never
invoked. Run reloads the engine and application, establishes instance state,
configures the resolver, and invokes a **public or internal static, non-generic object-returning
method with exactly one object parameter**, synchronously on the calling thread under
contextual reflection. Invocation exceptions are unwrapped; failed startup
releases the hosting lifetime.

Do not initialize engine services, discover plugins, or construct application
objects before the handoff. Prefer a plain static entry class with no
application-defined base types/interfaces. All real application work belongs in
the reloaded entry method, not the using body or the original Main afterward.

The argument and result cross the boundary unchanged, with no serialization or
conversion. Both can be null. An object[] can be supplied as the single argument
when multiple values are needed. Prefer a single string XML/JSON payload.
Do not pass OpenTAP/application objects or objects from privately loaded libraries
(including JSON libraries). A BCL container holding private-context objects is
not safe either. This is a caller contract, not an exhaustive object-graph
validation guarantee.

## Results and tuple arrays

Read `ApplicationHostContext.Result` before disposing the handle. Access after
disposal throws ObjectDisposedException; disposal releases the handle's result
reference. A caller can explicitly retain the result afterward.

```csharp
object result;
using (var context = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder()
    .WithEntryPoint(typeof(SessionEntryPoint), "Run").WithArgument(configuration).Build()))
    result = context.Result;

// Example entry method result:
// new object[] { "restart-session",
//     new[] { ("remove", "CSV:9.13.0"), ("add", "CSV:9.14.0") } }
var response = (object[])result;
var commands = ((string operation, string package)[])response[1];
```

Regression tests verify that both ValueTuple<string, string>[] (C# tuple arrays)
and Tuple<string, string>[] can be returned and cast by callers on .NET 9.
These tuple definitions live in shared CoreLib. Tuple elements must still be
compatible across contexts: wrapping a hosted custom type does not make it shared.
Retaining a hosted result, delegate, or exception can prevent future collection.

## Logical application identity

Inside a bootstrapped graph:

- `ApplicationContext.GetEntryAssembly()` returns the **reloaded application assembly**.
- `ApplicationContext.BaseDirectory` returns that instance's installation directory.

Without bootstrap, these fall back to Assembly.GetEntryAssembly() and the
application-domain base directory. Internal engine/CLI/package uses of entry
assembly now use the logical accessor. The actual process entry assembly, command
line, environment, and working directory are not changed by these accessors.

## Lifetime and limitations

Run returns a sealed ApplicationHostContext with an internal constructor. Disposal
is idempotent, clears the handle's result reference, releases the adapter's
resolver delegate, and requests unloading. Accessing Result afterward throws
ObjectDisposedException.

Disposal raises the context-local `ApplicationContext.Disposing` event once, synchronously
on the disposing thread with hosted contextual reflection active. Handlers cannot cancel
disposal; exceptions are reported to `System.Diagnostics.Trace` without preventing
remaining handlers or engine cleanup. The notification also runs when hosted startup
fails. It is not a process-wide `ProcessExit` notification. UI handlers must marshal to
their dispatcher if necessary, and handlers must not block indefinitely.

After notification, the engine cancels its root ThreadManager. Cancellation propagates
to TapThread hierarchies, waking idle workers and cancellation-aware work such as
`TapThread.Sleep` and package monitoring. Disposal does not wait for arbitrary plugin
work to finish. Logging services subscribe to an internal final cleanup phase: queued
logging is flushed without a timeout, the processor is signalled to stop, and the engine-owned
session-log file is closed and its process event subscriptions removed.

Contexts are collectible, but disposal does not guarantee collection. Plugins remain
responsible for their resources and non-cooperative background work can retain the
graph indefinitely. Applications can use `ApplicationContext.Disposing` to release
instance-owned services; full collection is not a supported requirement. Do not use an
instance after disposal.

The adapter keeps two narrowly scoped safeguards: selected framework-path
candidates use Default (newer local packages still load privately), and its
resolver delegate is cleared before Unload to avoid retaining the collectible
engine through the runtime's temporary unloading handle. No general-purpose
root chasing or service teardown is performed.

Nested hosting is supported: an outer CLI action can start an inner host, read
its result, dispose it, and resume in the outer context. These are independent
assembly graphs, not an assembly-resolution inheritance hierarchy. Contextual
reflection is restored on return. Regression tests verify collectible outer and
inner contexts, unloading notifications, and successful test-plan execution with
measurement results and completion callbacks. They deliberately do not assert
that the graphs are collected.

The `ApplicationHostDeploymentTest` deploys two local images and runs their
entry points concurrently, discovering/executing different plugins with the
same assembly name and different versions. It also verifies both engine locations
and `Installation.Current` roots. Synchronization uses a shared CoreLib `Action`
callback. Callbacks let the application define communication without exposing
its own types across the boundary.

Separate contexts can select separate managed dependency versions, but this is
not yet a promise of operationally isolated multiple OpenTAP instances. Logging,
settings, environment variables, native libraries, process-exit subscriptions,
threads, and external resources still need a full lifecycle/isolation audit.

This patch does not change the existing exact-then-highest dependency selection
policy. Assemblies already bound in a context cannot be upgraded; restart to use
a newly installed version.

`tap.deps.json` is neither generated nor shipped; debug and deployment use the
same resolver path. Third-party hosts should similarly keep the bootstrap's
dependency graph minimal and test without relying on application deps.json
probing inside the OpenTAP context.

## Regression tests

```sh
dotnet test Engine.UnitTests/Tap.Engine.UnitTests.csproj \
  --filter 'FullyQualifiedName~ApplicationHostTest|FullyQualifiedName~PluginManagerTest'

dotnet build tap/tap.csproj -p:SkipInstallOpenTapAsPackage=true
dotnet build tests/ApplicationHost.Json/ApplicationHost.Json.csproj
dotnet bin/ApplicationHost.Json/ApplicationHost.Json.dll

# Concurrent deployed-image plugin graphs:
dotnet test Package.UnitTests/Tap.Package.UnitTests.csproj \
  --filter FullyQualifiedName~ApplicationHostDeploymentTest
```

The latter test requires .NET 9 and has no deps.json. It loads JSON 9 in Default,
requests version 9 in the new context, verifies version 10 wins, and deserializes
a string payload using the privately loaded JSON 10 implementation.

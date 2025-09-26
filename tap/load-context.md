# Assembly Load Context

Dev notes containing learnings about AssemblyLoadContext. The documentation
doesn't really go in depth about how to use it. This text aims to detail the
limitations / learnings / potential of AssemblyLoadContext specifically for
OpenTAP.

## Why now

OpenTAP historically targeted .NET Framework on Windows, and .NET 6 on Linux /
Mac. This forced us to work within netstandard2.0 to ensure compatibility
between the three platforms.

We now target .NET 9 on all platforms, but for compatibility, we still target
.netstandard2.0 in all projects except tap.exe. This allows (most) existing
plugins targeting .NET Framework on Windows to continue working with no
changes. Unfortunately, this also prevents us from taking advantage of new
runtime features because they are not available in .NET Framework.

Due to our recent discovery that we can update in place by moving DLLs around
during updates, we can now be sure that `tap.exe` and `tap.dll` are always the
version we expect. That means we can start putting business logic in here. And
conveniently, that means we have an entrypoint that we control, which can make
use of modern .NET runtime features, such as AssemblyLoadContext.

We were previously very constrained in what we could actually do inside
`tap.dll`; in order to support isolation, we had to take special care to avoid
loading any other OpenTAP DLLs. Calling the entry point was done by
memory-loading the assembly and calling via reflection for example. Without
this limitation, we are free to treat `tap.exe` as any OpenTAP host. That means
we can implement interfaces from OpenTAP and call its public APIs however we
like.

More concretely, we can interface out the way OpenTAP resolves and loads
assemblies and hook a .NET 9 implementation using AssemblyLoadContext from
tap.exe in order to support more advanced plugin functionality.

## Motivation

Here is a non-exhaustive list of limitations with our current system which can
be solved with an AssemblyLoadContext approach:

### Plugins cannot upgrade OpenTAP dependencies

This is a problem with OpenTAP dependencies such as `Repository Client` and
`System.Text.Json`. Plugins which require a newer version of `System.Text.Json`
must resort to tricks in order to ensure the newer variant is loaded. 

The repository client cannot be upgraded at all because the public API has
changed. 

By loading and executing plugin code in a dedicated load context, it may be
possible to work around this limitation; if the load context is aware of the
plugin it is meant to host, it could attempt to load specific more specific
dependencies for the context.

### Loading native dependencies

The AppDomain API we currently use does not provide a hook for loading native
dependencies; to get around this, we currently copy the `libgit` dll to the
root of the installation. AssemblyLoadContext allows us to load the variant
from the `Dependencies\` subfolder without copying.

### Problems loading e.g. System.IO.Ports

This is a somewhat breaking changed introduced when we switched runtime to .NET
9; because the default load context uses a probing strategy which executes
before OpenTAP receives an assembly resolve callback, we cannot automatically
load the correct DLL when using e.g. the NuGet package for System.IO.Ports. 

By implementing our own load context, we can handle the resolve event properly.

### More restrictive plugin loading

We currently scan every dll in the installation directory. This makes it easy
to drag-and-drop plugins into an installation, but it also makes it very easy
to pollute an installation. When an installation is polluted, it becomes hard
to know what exact versions of plugins were used to achieve some result.

By decoupling searching and loading we can create different search and load
strategies. One example would be an "installation lock". An image containing
the exact plugin versions, similar to an image produced by the image resolver.
By loading the exact DLLs provided by an exact set of plugin packages, we can
ensure more reproducible results.

### New feature: slim install

A slim install is an installation that only has OpenTAP installed; by coupling
load contexts with OpenTAP sessions, it should be possible to create a new
context with a specific image, without extracting the packages. This could be
achieved by detecting plugins from the package cache / configured repositories,
and loading the required assemblies in the load context without extracting the
content to the installation folder. There are obviously some limitations to
this approach since some packages expect their content to exist on disk in the
installation directory, and install actions cannot be executed, but it would
enable interesting features such as executing a test plan from a slim install
and automatically loading in the exact dependencies of the test plan.

### In-process sessions

This feature could be of interest to plugins such as `Runner`, which creates
sessions by installing the required packages to a new directory and
communicating via nats.

The same OpenTAP process could host multiple sessions with different plugins
installed, all executing different test plans at the same time.

Launching a session in-process would be much faster, and use much fewer
resources compared to deploying a new installation.

As before, there are limitations to this approach; process isolation means
different sessions are much less likely to interfere with each other. Test plan
logic may assume exclusive access to an installation directory.

## Resources

 - [Introduction](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
 - [API overview](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
 - [Default Probing](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/default-probing)
 - [Plugin Tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)

## Findings

This section details random discoveries made during development.

### Use EnableDynamicLoading over CopyLocalLockFileAssemblies.

EnableDynamicLoading is designed for plugin-based solutions, and enables
CopyLocalLockFileAssemblies in addition to other things (??). The documentation
is a spotty.

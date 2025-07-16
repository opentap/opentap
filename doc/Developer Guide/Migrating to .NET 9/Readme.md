# Migrating to .NET 9

With the release of **OpenTAP 9.29**, the runtime has been upgraded from **.NET Framework 4.7.2** to **.NET 9**.  
This change brings significant performance improvements, better cross-platform support, and many new APIs.
Unfortunately, it also introduces subtle behavior changes, and the removal of legacy APIs.

This guide highlights common problems and workarounds specifically related to
OpenTAP, and is not meant to be comprehensive. For a comprehensive list of
breaking changes, see [the official
documentation](https://github.com/dotnet/docs/blob/main/docs/core/compatibility/breaking-changes.md).

---

## ProcessStartInfo Defaults Changed

**Symptoms**

ProcessStartInfo behaves differently when starting processes, causing unexpected behavior in some applications.

**Cause**  
Several default properties of `ProcessStartInfo` have changed in .NET 9 compared to .NET Framework 4.7.2. For example, `UseShellExecute` now defaults to `false`, which affects how processes are launched.

**Solution**  
Explicitly set important properties like `UseShellExecute`, `RedirectStandardOutput`, or `CreateNoWindow` to ensure consistent behavior across .NET versions.

**Example**

```csharp
var startInfo = new ProcessStartInfo
{
    UseShellExecute = true,  // Explicitly set to match .NET Framework behavior
};
Process.Start(startInfo);
```
---

## Missing `System.IO.Ports` API

**Symptoms**

```text
The type or namespace name 'IO' does not exist in the namespace 'System' (are you missing an assembly reference?)
```

or

```text
Could not load file or assembly 'System.IO.Ports, Version=0.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'. The system cannot find the file specified.
```

**Cause**  
The `System.IO.Ports` namespace has been removed from .NET 9 and is no longer included by default.

**Solution**  
Install the OpenTAP [Serial Ports](https://packages.opentap.io/#name=%2FPackages%2FSerial+Ports) package, which provides serial port functionality compatible with .NET 9:

```xml
<ItemGroup>
  <OpenTapPackageReference Include="Serial Ports" Version="9.0.7" />
</ItemGroup>
```

> This package restores the `System.IO.Ports` API functionality for OpenTAP users on .NET 9.
---

## Other Missing .NET Framework APIs

**Symptoms**

```text
The type or namespace name 'Drawing' does not exist in the namespace 'System' (are you missing an assembly reference?)
```

or

```text
Could not load for or assembly 'name, Version=x.y.z.w, ...
```

**Cause**  
Many APIs that were part of .NET Framework are not included by default in .NET 9.

**Solution**  
Check if Microsoft provides a compatibility package. This is the case for most
removed APIs. There is a meta package called
[Microsoft.Windows.Compatibility](https://www.nuget.org/packages/Microsoft.Windows.Compatibility)
which restores many removed APIs, but there are also more targetted
compatibility packages such as
[System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common/)
which restores specific APIs. The specific packages should be preferred if
possible.

> Hint: Check the Dependencies tab of the meta package for a list of all
> compatibility packages provided by Microsoft.

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Windows.Compatibility" Version="9.0.7" />
</ItemGroup>
```

> This enables use of legacy APIs such as `System.Drawing`, `System.ServiceModel`, and others.
---

## Assembly Side-Loading Not Supported

**Symptoms**

Unexpected behavior of certain assemblies.

**Cause**

.NET Framework supports automatic [side-by-side
execution](https://learn.microsoft.com/en-us/dotnet/standard/assembly/side-by-side-execution)
of strong named assemblies. This is a powerful feature, but can lead to subtle
bugs and dependency issues, and has been removed in .NET 9.

In .NET Framework, attempting to load two assemblies with the same name but
different versions results in two different instances of the same assembly. In
.NET 9, the second load call will return a handle to the first assembly.

**Solution**

Side-loading is still possible, but must be manually managed using `AssemblyLoadContext`.

---

## AppDomain Changes

**Symptoms**

Some `AppDomain` APIs are missing or behave differently in .NET 9, causing compatibility issues.

**Cause**  
Several `AppDomain` APIs related to assembly isolation and unloading have been
removed or altered in .NET 9. The runtime now uses `AssemblyLoadContext` for
dynamic assembly loading and unloading.

**Solution**  
Migrate code depending on `AppDomain` to use `AssemblyLoadContext` instead.
`AssemblyLoadContext` is too big a topic to cover in this document. Please
refer to Microsoft's documentation about
[AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)

---

## Global Assembly Cache (GAC) Not Supported

**Symptoms**

Attempts to use or reference assemblies from the GAC fails in .NET 9.

**Cause**  
.NET 9 does not support GAC.

**Solution**  
Distribute required assemblies as part of your plugin if permitted by the
software license. Alternatively, instruct your users to install relevant
software and manually load dependencies from the installation directory. 

If you know have a specific set of DLLs you need to load, you can load them
manually with `Assembly.LoadFrom(@"C:\Absolute\Path\To\Assembly.dll");`.

Alternatively, OpenTAP can be instructed to look for dependencies in a specific
directory by calling
`PluginManager.DirectoriesToSearch.Add(@"C:\Path\To\Library");`

---

## CET Enabled by Default

**Symptoms**

Application crashes when calling functions from shared libraries.

**Cause**

.NET 9 enables CET by default. This is a security feature which sets some
limitations to how a low level library can manipulate return addresses on the
stack, and can cause issues in low level libraries using custom exception
handling. For more information, see [CET supported by
default](https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/9.0/cet-support)
and [Shadow
Stack](https://learn.microsoft.com/en-us/windows-server/security/kernel-mode-hardware-stack-protection#use-shadow-stacks-to-enforce-integrity-of-control-flow).

**Solution**

For compatibility reasons, OpenTAP disables CET, so if you are starting the
process with `tap.exe` or `Editor.exe`, no steps are needed.

If you are in control of the shared library, you can update it to ensure it
does not violate shadow stack protection rules. This is the preferred solution
since it is the most secure solution, and it solves the problem regardless of
how the library is used.

If you are in control of the executable with the problem, you can disable CET by setting a build property:
```xml
<PropertyGroup>
  <!-- Disable Control-flow Enforcement Technology -->
  <CetCompat>false</CetCompat>
</PropertyGroup>
```

If you are not in control of either the library or the binary, you can disable
stack protections for your system or for a particular process.

---

## BinaryFormatter Removed

**Symptoms**

Code using `BinaryFormatter` for serialization fails to compile or throws runtime errors.

**Cause**  
`BinaryFormatter` has been fully removed in .NET 9 due to long-standing security concerns.

**Solution**  
Migrate to safer serialization alternatives such as `System.Text.Json`.

---

## Assembly.LoadWithPartialName Removed

**Symptoms**

Code using `Assembly.LoadWithPartialName` fails to compile or throws runtime errors.

**Cause**  
`Assembly.LoadWithPartialName` was deprecated and is removed in .NET 9 due to its unreliable behavior.

**Solution**  
Use `Assembly.Load` or `AssemblyLoadContext` with the full assembly name or path instead.

---

## Thread.Suspend and Thread.Resume Removed

**Symptoms**

Calls to `Thread.Suspend` or `Thread.Resume` result in compilation errors or runtime exceptions in .NET 9.

**Cause**  
These APIs have been removed because there is no way to use them safely.

**Solution**  
Refactor code to use safer synchronization primitives such as the `lock` keyword, `Monitor`, `Mutex`, `Semaphore` or `Event` for synchronization.

---

## Thread.Abort Removed

**Symptoms**

Calls to `Thread.Abort` cause compilation errors or runtime failures in .NET 9.

**Cause**  
`Thread.Abort` has been removed due to its unsafe nature and potential to leave application state inconsistent.

**Solution**  
Use cooperative cancellation patterns with `CancellationToken` to gracefully stop threads.

---

## Registry API Changes

**Symptoms**

Code using certain Windows-specific Registry APIs fails to compile or run in .NET 9.

**Cause**  
Some Registry APIs have been removed or now require additional NuGet packages (like `Microsoft.Win32.Registry`) to be referenced explicitly.

**Solution**  
Add the `Microsoft.Win32.Registry` package to your project and update your code to use the supported APIs.

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Win32.Registry" Version="5.0.0" />
</ItemGroup>
```

---

## System.IO.FileInfo.Length Exception on Missing File

**Symptoms**

Accessing `FileInfo.Length` throws a `FileNotFoundException` if the file does not exist.

**Cause**  
In .NET 9, `FileInfo.Length` no longer returns 0 for nonexistent files and instead throws an exception.

**Solution**  
Check `FileInfo.Exists` before accessing `FileInfo.Length`.

---

## TLS 1.0/1.1 Disabled by Default

**Symptoms**

Connections using TLS 1.0 or 1.1 fail to establish, causing communication errors.

**Cause**  
.NET 9 disables TLS 1.0 and 1.1 by default for improved security. Only TLS 1.2 and higher are enabled.

**Solution**  
Update your applications and servers to use TLS 1.2 or higher. Explicitly configure `SslProtocols` if needed.

**Example**

```csharp
var handler = new HttpClientHandler
{
    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
};

using var client = new HttpClient(handler);
```

---

## HttpWebRequest & WebClient Deprecated

**Symptoms**

Code using `HttpWebRequest` or `WebClient` issues warnings or may not behave optimally in .NET 9.

**Cause**  
`HttpWebRequest` and `WebClient` are deprecated in favor of the newer, more flexible `HttpClient` API.

**Solution**  
Migrate to using `HttpClient` for HTTP operations.

---

## CodeDomProvider Removed for Dynamic Compilation

**Symptoms**

Code using `CodeDomProvider` or `CSharpCodeProvider` to compile code at runtime fails to compile or run in .NET 9.

**Cause**  
`CodeDomProvider` and related runtime compilation APIs have been removed in .NET 9.

**Solution**  
Migrate to the Roslyn compiler APIs (`Microsoft.CodeAnalysis.CSharp`) for runtime code compilation.

---

## Remoting Removed

**Symptoms**

Code using .NET Remoting APIs fails to compile or run after upgrading to .NET 9.
This affects the following APIs:
* MarshalByRefObject.GetLifetimeService
* MarshalByRefObject.InitializeLifetimeService

**Cause**  
.NET Remoting has been completely removed in .NET 9. These APIs are no longer supported or available.

**Solution**  
There is no direct replacement. You must migrate to a different communication technology.

---

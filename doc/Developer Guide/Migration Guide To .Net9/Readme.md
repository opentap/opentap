# Migration Guide: OpenTAP 9.28 → 9.29 (.NET 4.7.2 → .NET 9)

With the release of **OpenTAP 9.29**, the runtime has been upgraded from **.NET Framework 4.7.2** to **.NET 9**.  
This change introduces several breaking changes due to API removals and behavior differences in .NET Core/.NET 9.

This guide highlights key migration issues and how to resolve them.

---

## Missing `System.IO.Ports` API

**Symptoms**

```csharp
The type or namespace name 'IO' does not exist in the namespace 'System' (are you missing an assembly reference?)
```

**Cause**  
The `System.IO.Ports` namespace has been removed from .NET 9 and is no longer included by default.

**Solution**  
Install the OpenTAP `SerialPorts` package, which provides serial port functionality compatible with .NET 9:

```xml
<ItemGroup>
  <OpenTapPackageReference Include="OpenTAP.SerialPorts" Version="x.y.z" />
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
The type or namespace name 'ServiceModel' does not exist in the namespace 'System' (are you missing an assembly reference?)
```

**Cause**  
Many APIs that were part of the full .NET Framework are not included by default in .NET 9.

**Solution**  
Install the compatibility pack to restore many of the missing types and APIs:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Windows.Compatibility" Version="10.0.0-preview.5.25277.114" />
</ItemGroup>
```

> This package enables use of legacy namespaces like `System.Configuration`, `System.Drawing`, `System.ServiceModel`, and others.
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

## AppDomain Changes

**Symptoms**

Some `AppDomain` APIs are missing or behave differently in .NET 9, causing compatibility issues.

**Cause**  
Several `AppDomain` APIs related to application domain isolation and unloading have been removed or altered in .NET 9. The runtime now uses `AssemblyLoadContext` for dynamic assembly loading and unloading.

**Solution**  
Update code to replace unsupported `AppDomain` APIs with `AssemblyLoadContext` where appropriate.

**Example**

```csharp
var context = new AssemblyLoadContext("MyContext", true);
var assembly = context.LoadFromAssemblyPath("path/to/assembly.dll");
```

---

## Remoting Removed

**Symptoms**

Code using .NET Remoting APIs fails to compile or run after upgrading to .NET 9.

**Cause**  
.NET Remoting has been completely removed in .NET 9. These APIs are no longer supported or available.

**Solution**  
Migrate to supported communication technologies such as gRPC, WCF (Windows Communication Foundation), or REST-based services depending on your use case.

> No direct replacement is provided; this requires code refactoring.

---

## Global Assembly Cache (GAC) Not Supported

**Symptoms**

Attempts to use or reference assemblies from the GAC fail in .NET 9.

**Cause**  
.NET 9 does not support the Global Assembly Cache (GAC). The GAC model was removed in favor of package-based assembly management.

**Solution**  
Distribute assemblies via NuGet packages or include them directly in your application’s deployment. Adjust your build and deployment processes accordingly.

---

## Code Access Security (CAS) Removed

**Symptoms**

Code that relies on Code Access Security (CAS) policies or permissions fails to work as expected.

**Cause**  
CAS has been removed in .NET 9. Security is now expected to be managed through OS-level mechanisms and other modern security practices.

**Solution**  
Refactor your security model to rely on operating system security features, such as user permissions and sandboxing.

---

## BinaryFormatter Removed

**Symptoms**

Code using `BinaryFormatter` for serialization fails to compile or throws runtime errors.

**Cause**  
`BinaryFormatter` has been fully removed in .NET 9 due to long-standing security concerns.

**Solution**  
Migrate to safer serialization alternatives such as `System.Text.Json`.

**Example**

```csharp
// Old BinaryFormatter code (no longer supported)
// var formatter = new BinaryFormatter();
// formatter.Serialize(stream, myObject);

// New System.Text.Json code
var jsonString = System.Text.Json.JsonSerializer.Serialize(myObject);
System.IO.File.WriteAllText("data.json", jsonString);
```

---

## Assembly.LoadWithPartialName Removed

**Symptoms**

Code using `Assembly.LoadWithPartialName` fails to compile or throws runtime errors.

**Cause**  
`Assembly.LoadWithPartialName` was deprecated and is removed in .NET 9 due to its unreliable behavior.

**Solution**  
Use `Assembly.Load` or `AssemblyLoadContext` with the full assembly name or path instead.

**Example**

```csharp
// Old code using LoadWithPartialName (no longer supported)
// var assembly = Assembly.LoadWithPartialName("MyAssembly");

// New recommended approach using Assembly.Load
var assembly = Assembly.Load("MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
```

---

## Reflection.Emit Changes

**Symptoms**

Code using `System.Reflection.Emit` APIs to generate dynamic assemblies or types fails or behaves differently.

**Cause**  
While `Reflection.Emit` is still supported in .NET 9, some APIs and behaviors have changed or require different permissions, especially around dynamic assembly saving and debugging.

**Solution**  
Review and update your use of `Reflection.Emit` to align with .NET 9 requirements.

---

## CodeDomProvider Removed for Dynamic Compilation

**Symptoms**

Code using `CodeDomProvider` or `CSharpCodeProvider` to compile code at runtime fails to compile or run in .NET 9.

**Cause**  
`CodeDomProvider` and related runtime compilation APIs have been removed in .NET 9.

**Solution**  
Migrate to the Roslyn compiler APIs (`Microsoft.CodeAnalysis.CSharp`) for runtime code compilation.

**Example**

```csharp
// Old CodeDomProvider usage (no longer supported)
// var provider = new CSharpCodeProvider();

// Instead, use Roslyn:
// See Microsoft.CodeAnalysis.CSharp for runtime compilation options
```

---

## Thread.Suspend and Thread.Resume Removed

**Symptoms**

Calls to `Thread.Suspend` or `Thread.Resume` result in compilation errors or runtime exceptions in .NET 9.

**Cause**  
These APIs have been removed due to their unsafe and unreliable behavior.

**Solution**  
Refactor code to use safer synchronization primitives such as `ManualResetEvent`, `Mutex`, or `CancellationToken` for thread control.

**Example**

```csharp
// Old unsafe approach (no longer supported)
// thread.Suspend();
// thread.Resume();

// New approach using CancellationToken
var cts = new CancellationTokenSource();

void ThreadWork()
{
    while (!cts.Token.IsCancellationRequested)
    {
        // Do work here
    }
}

// To stop the thread:
cts.Cancel();
```

---

## Thread.Abort Removed

**Symptoms**

Calls to `Thread.Abort` cause compilation errors or runtime failures in .NET 9.

**Cause**  
`Thread.Abort` has been removed due to its unsafe nature and potential to leave application state inconsistent.

**Solution**  
Use cooperative cancellation patterns with `CancellationToken` to gracefully stop threads.

**Example**

```csharp
// Old unsafe code (no longer supported)
// thread.Abort();

// New approach using CancellationToken
var cts = new CancellationTokenSource();

void ThreadWork()
{
    while (!cts.Token.IsCancellationRequested)
    {
        // Perform work here
    }
}

// To request cancellation:
cts.Cancel();
```

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
  <PackageReference Include="Microsoft.Win32.Registry" Version="6.0.0" />
</ItemGroup>
```

**Example**

```csharp
var key = Registry.CurrentUser.OpenSubKey("Software\\MyApp");
```

---

## System.IO.FileInfo.Length Exception on Missing File

**Symptoms**

Accessing `FileInfo.Length` throws a `FileNotFoundException` if the file does not exist, instead of returning 0.

**Cause**  
In .NET 9, `FileInfo.Length` no longer returns 0 for nonexistent files and instead throws an exception to reflect the file's absence more accurately.

**Solution**  
Check for file existence before accessing `Length` or handle the exception accordingly.

**Example**

```csharp
var fileInfo = new FileInfo("path/to/file.txt");

if (fileInfo.Exists)
{
    long length = fileInfo.Length;
}
else
{
    // Handle missing file scenario
}
```

---

## HttpWebRequest & WebClient Deprecated

**Symptoms**

Code using `HttpWebRequest` or `WebClient` issues warnings or may not behave optimally in .NET 9.

**Cause**  
`HttpWebRequest` and `WebClient` are deprecated in favor of the newer, more flexible `HttpClient` API.

**Solution**  
Migrate to using `HttpClient` for HTTP operations.

**Example**

```csharp
// Old approach with WebClient (deprecated)
// using var client = new WebClient();
// string result = client.DownloadString("https://example.com");

// New approach with HttpClient
using var httpClient = new HttpClient();
string result = await httpClient.GetStringAsync("https://example.com");
```

---

## Default Hashing Algorithms Changed

**Symptoms**

Hashing outputs differ between .NET Framework 4.7.2 and .NET 9, causing mismatches or failures in verification.

**Cause**  
Some hashing algorithms now use more secure defaults or updated implementations in .NET 9.

**Solution**  
Review your hashing code to ensure it explicitly specifies the algorithm and parameters, or update to use recommended secure algorithms.

**Example**

```csharp
byte[] ComputeHash(string input)
{
    using var sha256 = SHA256.Create();
    return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
}
```

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


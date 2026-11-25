using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using OpenTap;

public static class Program
{
    static void Main()
    {
        var runtime = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("System.Text.Json"));
        if (Environment.Version.Major != 9 || runtime.GetName().Version.Major != 9)
            throw new Exception("Run this regression test on .NET 9.");
        using (var host = OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(Program), nameof(EntryPoint)).WithArgument("[\"payload\"]").Build()))
            if ((string)host.Result != "verified") throw new Exception("Unexpected session result.");
        if (AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly) != AssemblyLoadContext.Default)
            throw new Exception("Caller engine should remain in Default.");
        Console.WriteLine("PASS: external bootstrap retains JSON 9 in Default and uses JSON 10 in OpenTAP.");
    }

    public static object EntryPoint(object argument)
    {
        var context = AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly);
        if (context == AssemblyLoadContext.Default || ApplicationContext.GetEntryAssembly() != typeof(Program).Assembly)
            throw new Exception("Engine/application were not reloaded with a logical entry assembly.");
        // A plugin request for JSON 10 must not bind to the Default context's JSON 9.
        var selected = context.LoadFromAssemblyName(new AssemblyName(
            "System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));
        if (selected.GetName().Version.Major != 10)
            throw new Exception("Plugin dependency selection failed.");
        VerifyJson((string)argument);
        return "verified";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void VerifyJson(string payload)
    {
        var json = typeof(JsonSerializer).Assembly;
        if (json.GetName().Version.Major != 10 || AssemblyLoadContext.GetLoadContext(json).Name != "OpenTAP")
            throw new Exception("JSON is not version 10 in OpenTAP.");
        var values = JsonSerializer.Deserialize<string[]>(payload);
        if (values.Length != 1 || values[0] != "payload") throw new Exception("Payload round-trip failed.");
        Console.WriteLine($"JSON {json.GetName().Version} in {AssemblyLoadContext.GetLoadContext(json).Name}: {JsonSerializer.Serialize(values)}");
    }
}

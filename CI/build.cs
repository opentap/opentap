#:property TargetFramework=net10.0
#:property PublishAot=false

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

try
{
    return await Build.RunAsync(args);
}
catch (BuildFailure ex)
{
    WriteError(ex.Message);
    return 1;
}
catch (Exception ex)
{
    WriteError(ex.ToString());
    return 1;
}

static void WriteError(string message) =>
    Console.Error.WriteLine($"\e[1;31mERROR: {message}\e[0m");

static class Build
{
    private static bool verbose;
    private static bool requireDevGuide;
    private static string repoRoot = "";
    private static string ciDir = "";
    private static string artifactsDir = "";
    private static string hostPlatform = "";
    private static string hostTapPlatform = "";
    private static string hostArch = "";
    private static string gitVersion = "";

    private static readonly (string Rid, string Platform, string Architecture)[] Targets =
    [
        // win-arm64 intentionally not packaged.
        ("win-x64", "Windows", "x64"),
        ("win-x86", "Windows", "x86"),
        ("linux-arm", "Linux", "arm"),
        ("linux-arm64", "Linux", "arm64"),
        ("linux-x64", "Linux", "x64"),
        ("osx-arm64", "MacOS", "arm64"),
        ("osx-x64", "MacOS", "x64"),
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        ParseArguments(args);
        repoRoot = FindRepoRoot(Environment.CurrentDirectory);
        ciDir = Path.Combine(repoRoot, "CI");
        artifactsDir = Path.Combine(ciDir, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        DetectHost();

        Info($"Detected host: {hostPlatform} {hostArch}");

        // Bootstrap with the base version so the initial assemblies are
        // compatible with the full version calculated by OpenTAP.
        var versionLine = File.ReadLines(Path.Combine(repoRoot, ".gitversion"))
            .FirstOrDefault(line => line.StartsWith("version", StringComparison.Ordinal));
        var shortVersion = versionLine?.Split('=', 2).ElementAtOrDefault(1)?.Trim();
        if (string.IsNullOrEmpty(shortVersion))
            throw new BuildFailure($"Unable to read version from {Path.Combine(repoRoot, ".gitversion")}");

        SetVersionEnvironment(shortVersion, shortVersion);

        Debug("Building OpenTAP");
        await RunAsync("Building OpenTAP failed", "dotnet",
            ["build", Path.Combine(repoRoot, "OpenTAP.slnx"), "--configuration", "Release"], repoRoot);

        var bootstrapTap = Path.Combine(repoRoot, "bin", "Release", ExecutableName("tap"));
        MakeExecutable(bootstrapTap);
        gitVersion = await CaptureAsync(bootstrapTap, ["sdk", "gitversion"], repoRoot,
            new() { ["OPENTAP_COLOR"] = "never", ["OPENTAP_WARNINGS_TO_STDERR"] = "true" });
        shortVersion = await CaptureAsync(bootstrapTap, ["sdk", "gitversion", "--fields", "3"], repoRoot,
            new() { ["OPENTAP_COLOR"] = "never", ["OPENTAP_WARNINGS_TO_STDERR"] = "true" });
        SetVersionEnvironment(shortVersion, gitVersion);

        /* templates can build independently of all other jobs */
        var templateTask = PackageTemplatesAsync();

        Debug($"Building OpenTAP {gitVersion} for all targets");
        await RunAsync("Building OpenTAP targets failed", "dotnet",
            ["build", Path.Combine(ciDir, "multi-target.slnx"), "--configuration", "Release"], repoRoot);

        PrepareDocumentation();

        var token = Environment.GetEnvironmentVariable("KS8500_REPO_TOKEN");
        var certificate = Environment.GetEnvironmentVariable("TAP_SIGN_CERT");
        var signingEnabled = !string.IsNullOrEmpty(token) &&
                             !string.IsNullOrEmpty(certificate) &&
                             new FileInfo(certificate).Exists && new FileInfo(certificate).Length > 0;
        if (signingEnabled)
            Info("Signing enabled");
        else
            Warn("Signing disabled");

        File.WriteAllText(Path.Combine(repoRoot, "bin", "Release", ".OpenTapIgnore"), "");

        if (signingEnabled)
            await RunTapAsync("Installing signing package failed",
                ["package", "install", "Sign", "-f", "-v", "--repository",
                 $"https://internal.automation.keysight.com/api/repository;token={token}"], repoRoot);

        await Task.WhenAll(Targets.Select(target => PackageOpenTapAsync(target, signingEnabled)));
        /* PackageSdk requires OpenTAP to be installed as a package. Wait for packages to finish building. */
        var sdkTask = PackageSdkAsync(token);
        /* PackageNuget needs all OpenTap.TapPackages before it can start. */
        var nugetTask = PackageNuGetAsync();
        await Task.WhenAll([templateTask, sdkTask, nugetTask]);
        return 0;
    }

    private static void ParseArguments(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--verbose" or "-v":
                    verbose = true;
                    break;
                case "--require-devguide":
                    requireDevGuide = true;
                    break;
                default:
                    throw new BuildFailure($"Unknown argument: {arg}");
            }
        }
    }

    private static void DetectHost()
    {
        if (OperatingSystem.IsWindows())
        {
            hostPlatform = "win";
            hostTapPlatform = "Windows";
        }
        else if (OperatingSystem.IsMacOS())
        {
            hostPlatform = "osx";
            hostTapPlatform = "MacOS";
        }
        else if (OperatingSystem.IsLinux())
        {
            hostPlatform = "linux";
            hostTapPlatform = "Linux";
        }
        else
        {
            throw new BuildFailure("Unsupported host operating system.");
        }

        hostArch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => throw new BuildFailure($"Unsupported host architecture: {RuntimeInformation.OSArchitecture}")
        };
    }

    private static void SetVersionEnvironment(string shortVersion, string fullVersion)
    {
        Environment.SetEnvironmentVariable("ShortVersion", shortVersion);
        Environment.SetEnvironmentVariable("GitVersion", fullVersion);
        gitVersion = fullVersion;
    }

    private static void PrepareDocumentation()
    {
        /* delete files that should not be shipped in the package doc folder. */
        var destination = Path.Combine(ciDir, "doc");
        if (!Directory.Exists(destination))
            CopyDirectory(Path.Combine(repoRoot, "doc"), destination);

        foreach (var relativePath in new[]
                 {
                     "API Documentation", ".vitepress", "public", "node_modules",
                     "package-lock.json", "package.json"
                 })
            DeletePath(Path.Combine(destination, relativePath));
    }

    private static async Task PackageOpenTapAsync(
        (string Rid, string Platform, string Architecture) target,
        bool signingEnabled)
    {
        Debug($"Building OpenTAP package for {target.Rid}");
        var binDir = Path.Combine(repoRoot, "bin", "Release", target.Rid);
        var docsDestination = Path.Combine(binDir, "Packages", "OpenTAP", "doc");
        Directory.CreateDirectory(Path.GetDirectoryName(docsDestination)!);
        CopyDirectory(Path.Combine(ciDir, "doc"), docsDestination, overwrite: true);

        await RunTapAsync($"Packaging {target.Rid} failed",
            ["package", "create", "-v", "-c", Path.Combine(repoRoot, "package.xml"), "-o",
             Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage")],
            binDir,
            new()
            {
                ["Platform"] = target.Platform,
                ["Sign"] = signingEnabled ? "true" : "false",
                ["Architecture"] = target.Architecture
            });
    }

    private static async Task InstallOpenTapAsPackageAsync()
    {
        Debug("Preparing OpenTAP installation");
        await RunTapAsync("Preparing OpenTAP installation failed",
            ["package", "install", "-f",
             Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{hostTapPlatform}.{hostArch}.TapPackage")],
            repoRoot);
    }

    private static async Task PackageTemplatesAsync()
    {
        Debug("Building template package");
        var destination = Path.Combine(ciDir, "templates");
        DeletePath(destination);
        CopyDirectory(Path.Combine(repoRoot, "templates"), destination);
        File.Copy(Path.Combine(repoRoot, "LICENSE.txt"), Path.Combine(ciDir, "LICENSE.txt"), true);

        ReplaceInFiles("$(GitVersion)", gitVersion,
        [
            Path.Combine(destination, "Templates.csproj"),
            Path.Combine(destination, "src", "project", "ProjectName.csproj"),
            Path.Combine(destination, "src", "solution", "Directory.Build.props"),
            Path.Combine(destination, "src", "solution", ".github", "workflows", "ci.yml")
        ]);

        await RunAsync("Building Templates failed", "dotnet",
            ["pack", "--configuration", "Release", "--output", artifactsDir], destination);
    }

    private static async Task PackageSdkAsync(string? token)
    {
        Debug("Building SDK package");
        await InstallOpenTapAsPackageAsync();
        var binDir = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}");
        var sdkDir = Path.Combine(binDir, "Packages", "SDK");
        Directory.CreateDirectory(sdkDir);

        CopyDirectory(Path.Combine(repoRoot, "sdk", "Examples"), Path.Combine(sdkDir, "Examples"), true);
        File.Copy(Path.Combine(repoRoot, "Package", "PackageSchema.xsd"), Path.Combine(sdkDir, "PackageSchema.xsd"), true);
        ReplaceInFiles("$(GitVersion)", gitVersion,
            [Path.Combine(sdkDir, "Examples", "Directory.Build.props")]);

        var templatePackage = Directory.GetFiles(artifactsDir, "OpenTap.Templates.*.nupkg").Single();
        File.Copy(templatePackage, Path.Combine(sdkDir, "OpenTap.Templates.nupkg"), true);

        await RunTapAsync("Installing DocumentationGeneration failed",
            ["package", "install", "DocumentationGeneration", "-f", "--version", "1.0.2", "--os", "Linux",
             "--repository", $"https://internal.automation.keysight.com/api/repository;token={token ?? ""}"],
            binDir);

        var guide = Path.Combine(sdkDir, "Examples", "OpenTAP Developer Guide.pdf");
        if (OperatingSystem.IsLinux())
        {
            await RunTapAsync("Generating developer guide failed",
                ["generate-pdf", Path.Combine(repoRoot, "doc", "Developer Guide", "Readme.md"), "--toc",
                 "--skip-first-file", "--out", guide,
                 "--frontpage", Path.Combine(repoRoot, "doc", "Developer Guide", "Frontpage.html"),
                 "--frontpage-file", Path.Combine(repoRoot, "doc", "Developer Guide", "Frontpage.png")],
                binDir);
        }
        else if (requireDevGuide)
        {
            throw new BuildFailure($"Cannot generate developer guide PDF on {hostTapPlatform}.");
        }
        else
        {
            Warn($"Cannot generate developer guide PDF on {hostTapPlatform}. Skipping.");
            File.WriteAllBytes(guide, []);
        }

        File.Copy(Path.Combine(repoRoot, "bin", "Release", "Keysight.OpenTap.Sdk.MSBuild.dll"),
            Path.Combine(binDir, "Keysight.OpenTap.Sdk.MSBuild.dll"), true);
        File.Copy(Path.Combine(repoRoot, "bin", "Release", "OpenTap.Sdk.New.dll"),
            Path.Combine(binDir, "OpenTap.Sdk.New.dll"), true);

        await RunTapAsync("Building SDK package failed",
            ["package", "create", "-v", "-c", Path.Combine(repoRoot, "sdk", "sdk.package.xml"), "-o",
             Path.Combine(artifactsDir, "SDK.TapPackage")], binDir);
    }

    private static async Task PackageNuGetAsync()
    {
        Debug("Building NuGet package");
        var buildDir = Path.Combine(repoRoot, "bin", "Release");
        var nugetDir = Path.Combine(ciDir, "nuget");
        DeletePath(nugetDir);
        CopyDirectory(Path.Combine(repoRoot, "nuget"), nugetDir);
        ReplaceInFiles("$(GitVersion)", gitVersion, [Path.Combine(nugetDir, "OpenTAP.nuspec")]);

        var docs = Path.Combine(nugetDir, "build", "docs");
        Directory.CreateDirectory(Path.Combine(docs, "Packages", "OpenTAP"));
        File.Copy(Path.Combine(buildDir, "OpenTap.Package.xml"), Path.Combine(docs, "OpenTap.Package.xml"), true);
        File.Copy(Path.Combine(buildDir, "OpenTap.xml"), Path.Combine(docs, "OpenTap.xml"), true);
        File.Copy(Path.Combine(buildDir, "OpenTap.Plugins.BasicSteps.xml"),
            Path.Combine(docs, "Packages", "OpenTAP", "OpenTap.Plugins.BasicSteps.xml"), true);
        File.Copy(Path.Combine(buildDir, "Keysight.OpenTap.Sdk.MSBuild.dll"),
            Path.Combine(nugetDir, "build", "Keysight.OpenTap.Sdk.MSBuild.dll"), true);
        File.Copy(Path.Combine(buildDir, "DotNet.Glob.NetStandard1.1.dll"),
            Path.Combine(nugetDir, "build", "DotNet.Glob.dll"), true);

        foreach (var target in Targets.Where(t => t.Rid != "linux-arm"))
        {
            var runtime = target.Rid.Replace("osx-", "macos-", StringComparison.Ordinal);
            var package = Path.Combine(artifactsDir,
                $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage");
            ExtractZip(package, Path.Combine(nugetDir, "build", "runtimes", runtime));
        }

        await RunAsync("Building NuGet package failed", "dotnet",
            ["pack", Path.Combine(nugetDir, "OpenTAP.Pack.csproj"), "--configuration", "Release", "--output", artifactsDir],
            repoRoot);
    }

    private static async Task RunTapAsync(string failureMessage, string[] arguments, string workingDirectory,
        Dictionary<string, string>? environment = null)
    {
        var tap = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}", ExecutableName("tap"));
        MakeExecutable(tap);
        await RunAsync(failureMessage, tap, arguments, workingDirectory, environment);
    }

    private static async Task<string> CaptureAsync(string executable, string[] arguments, string workingDirectory,
        Dictionary<string, string>? environment = null)
    {
        var result = await StartAsync(executable, arguments, workingDirectory, environment, capture: true);
        if (result.ExitCode != 0)
            throw new BuildFailure($"Command failed ({executable}):\n{result.Output}");
        return result.StandardOutput.Trim();
    }

    private static async Task RunAsync(string failureMessage, string executable, string[] arguments,
        string workingDirectory, Dictionary<string, string>? environment = null)
    {
        var result = await StartAsync(executable, arguments, workingDirectory, environment, capture: !verbose);
        if (result.ExitCode != 0)
            throw new BuildFailure(verbose ? failureMessage : $"{failureMessage}:\n{result.Output}");
    }

    private static async Task<ProcessResult> StartAsync(string executable, string[] arguments,
        string workingDirectory, Dictionary<string, string>? environment, bool capture)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var pair in environment)
                start.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(start) ?? throw new BuildFailure($"Failed to start {executable}.");
        if (!capture)
        {
            await process.WaitForExitAsync();
            return new(process.ExitCode, "", "");
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await stdout, await stderr);
    }

    private static void ReplaceInFiles(string oldValue, string newValue, IEnumerable<string> files)
    {
        foreach (var file in files)
            File.WriteAllText(file, File.ReadAllText(file).Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    private static void ExtractZip(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        ZipFile.ExtractToDirectory(archive, destination, true);

        if (OperatingSystem.IsWindows())
            return;

        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
        {
            var mode = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0xFFF);
            if (mode != 0)
                File.SetUnixFileMode(Path.Combine(destination, entry.FullName), mode);
        }
    }

    private static void DeletePath(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        else if (File.Exists(path))
            File.Delete(path);
    }

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) |
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    private static string ExecutableName(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    private static string FindRepoRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".gitversion")) &&
                File.Exists(Path.Combine(directory.FullName, "OpenTAP.slnx")))
                return directory.FullName;

        throw new BuildFailure("Could not locate the OpenTAP repository root.");
    }

    private static void CopyDirectory(string source, string destination, bool overwrite = false)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }

    private static void Info(string message) => Console.WriteLine(message);
    private static void Debug(string message) => Console.WriteLine($"\e[90mDEBUG: {message}\e[0m");
    private static void Warn(string message) => Console.Error.WriteLine($"\e[1;33mWARN: {message}\e[0m");

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Output => string.Concat(StandardOutput, StandardError);
    }

}

sealed class BuildFailure(string message) : Exception(message);

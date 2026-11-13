using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http.Headers;
using System.Text;

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

static void WriteError(string message) => Build.Error(message);

static class Build
{
    private static string smartInstallerVersion = "3.4.0-alpha.5.1+1619c20e.package-sources";
    private static bool verbose;
    private static bool skipDevGuide;
    private static bool skipSign;
    private static bool publishPackages;
    private static bool publishInstallers;
    private static TimeSpan publishRetryWindow = TimeSpan.FromMinutes(10);
    private static string repoRoot = "";
    private static string ciDir = "";
    private static string artifactsDir = "";
    private static string hostPlatform = "";
    private static string hostTapPlatform = "";
    private static string hostArch = "";
    private static string gitVersion = "";
    private static string templatePackagePath = "";
    private static readonly Lazy<string> RepoToken = new(() =>
        Environment.GetEnvironmentVariable("KS8500_REPO_TOKEN") ??
        throw new BuildFailure("KS8500_REPO_TOKEN is not configured."));

    private static readonly Lazy<bool> SigningEnabled = new(() =>
    {
        var certificate = Environment.GetEnvironmentVariable("TAP_SIGN_CERT");
        return !skipSign && !string.IsNullOrEmpty(RepoToken.Value) &&
               !string.IsNullOrEmpty(certificate) &&
               new FileInfo(certificate).Exists && new FileInfo(certificate).Length > 0;
    });

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
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        ParseArguments(args);
        repoRoot = FindRepoRoot(Environment.CurrentDirectory);
        ciDir = Path.Combine(repoRoot, "CI");
        artifactsDir = Path.Combine(ciDir, "artifacts");

        var graph = new BuildGraph(Info);
        var signing = graph.Node("signing");
        if (!skipSign)
            signing.RequiresSecrets("KS8500_REPO_TOKEN", "TAP_SIGN_CERT", "TAP_SIGN_ADDRESS", "TAP_SIGN_AUTH")
                .Requires(() => SigningEnabled.Value,
                    "Signing certificate is missing or empty. Use --skip-sign for an unsigned build.");

        var setup = graph.Node("setup")
            .DependsOn(
                graph.Job("workspace", PrepareWorkspace),
                graph.Job("version", CalculateVersionAsync),
                signing);
        var templates = graph.Job("templates", PackageTemplatesAsync)
            .DependsOn(setup);
        var crossBuild = graph.Job("cross-build", CrossBuildAsync)
            .DependsOn(setup);
        var documentation = graph.Job("documentation", PrepareDocumentation)
            .DependsOn(setup);
        var tools = graph.Job("build-tools", InstallBuildToolsAsync)
            .DependsOn(crossBuild)
            .RequiresSecrets("KS8500_REPO_TOKEN");

        var packages = Targets.ToDictionary(target => target.Rid, target =>
            graph.Job($"package-{target.Rid}", () => PackageOpenTapAsync(target))
                .DependsOn(crossBuild, documentation, tools));
        var packagesComplete = graph.Node("packages-complete")
            .DependsOn(packages.Values.ToArray());

        // SDK packaging mutates the shared host installation. Keep it behind ALL
        // package creation jobs, not just the host package, to avoid discovery races.
        var sdk = graph.Job("sdk", PackageSdkAsync)
            .DependsOn(templates, packagesComplete)
            .Requires(() => skipDevGuide || !OperatingSystem.IsMacOS(),
                "Cannot generate developer guide PDF on macOS. Use --skip-devguide.");
        var nuget = graph.Job("nuget", PackageNuGetAsync)
            .DependsOn(packagesComplete);

        var windowsInstaller = graph.Job("installer-windows", () => BuildInstallerAsync(
            "win", "Windows", "opentap.installer.xml", $"OpenTAP.{gitVersion}.exe"))
            .DependsOn(sdk, packages["win-x64"]);
        // Each installer gets a private tooling copy, so both can run concurrently.
        var linuxInstaller = graph.Job("installer-linux", () => BuildInstallerAsync(
            "linux", "Linux", "opentap.installer.linux.xml", "OpenTAP"))
            .DependsOn(sdk, packages["linux-x64"]);

        var buildComplete = graph.Node("build-complete")
            .DependsOn(templates, sdk, nuget, windowsInstaller, linuxInstaller);

        if (publishPackages)
            graph.Job("publish-packages", PublishPackagesAsync)
                .DependsOn(buildComplete)
                .Retry(publishRetryWindow, TimeSpan.FromSeconds(30))
                .RequiresSecrets("PUBLIC_REPO_TOKEN");
        if (publishInstallers)
            graph.Job("publish-installers", PublishInstallersAsync)
                .DependsOn(buildComplete)
                .Retry(publishRetryWindow, TimeSpan.FromSeconds(30))
                .RequiresSecrets("INSTALLER_PUBLISH_KEY", "INSTALLER_PUBLISH_KEY_ARCHIVE",
                    "TAP_SIGN_CERT", "TAP_SIGN_ADDRESS", "TAP_SIGN_AUTH")
                .Requires(() => SigningEnabled.Value,
                    "Installer publishing requires a signed build; --skip-sign cannot be used.")
                .Requires(() => RequireSecret("INSTALLER_PUBLISH_KEY_ARCHIVE").Contains(':'),
                    "INSTALLER_PUBLISH_KEY_ARCHIVE must have the form username:password.");

        await graph.RunAsync();
        return 0;
    }

    private static void PrepareWorkspace()
    {
        Directory.CreateDirectory(artifactsDir);
        DetectHost();
        // Rebuilding locally fails if .OpenTapIgnore exists in bin/Release.
        DeletePath(Path.Combine(repoRoot, "bin", "Release", ".OpenTapIgnore"));
        Info($"Detected host: {hostPlatform} {hostArch}");
    }

    private static async Task CalculateVersionAsync()
    {
        // Calculate once, in-process, so every build uses the same version.
        var calculator = new GitVersionCalculator.VersionCalculator(repoRoot)
        {
            Warning = message => Warn($"Warning: {message}")
        };
        var version = calculator.GetVersion();
        var shortVersion = $"{version.Major}.{version.Minor}.{version.Patch}";
        SetVersionEnvironment(shortVersion, version.ToString());

        // Preserve get-gitversion's output formats for downstream workflow jobs.
        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(githubOutput))
        {
            var longVersion = shortVersion +
                (string.IsNullOrEmpty(version.PreRelease) ? "" : "-" + version.PreRelease);
            await File.AppendAllTextAsync(githubOutput,
                $"ShortVersion={shortVersion}\nLongVersion={longVersion}\nGitVersion={gitVersion}\n");
        }
    }

    private static Task CrossBuildAsync() => RunAsync("dotnet",
        ["build", Path.Combine(ciDir, "multi-target.slnx"), "--configuration", "Release"], repoRoot);

    private static async Task InstallBuildToolsAsync()
    {
        File.WriteAllText(Path.Combine(repoRoot, "bin", "Release", ".OpenTapIgnore"), "");
        var ks8500 = $"https://internal.automation.keysight.com/api/repository;token={RepoToken.Value}";
        if (SigningEnabled.Value)
            await RunTapAsync(["package", "install", "Sign", "-f", "--repository", ks8500], repoRoot);
        await RunTapAsync(["package", "install", "SmartInstaller", "-f", "--version",
            smartInstallerVersion, "--repository", ks8500], repoRoot);
    }

    private static async Task BuildInstallerAsync(string targetOs, string packagePlatform,
        string configuration, string outputName)
    {
        var installerDir = Path.Combine(ciDir, "installers", targetOs);
        DeletePath(installerDir);
        CopyDirectory(Path.Combine(repoRoot, "Installer", "Assets"), installerDir);
        File.Copy(Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{packagePlatform}.x64.TapPackage"),
            Path.Combine(installerDir, "OpenTAP.TapPackage"), true);
        File.Copy(Path.Combine(artifactsDir, "SDK.TapPackage"),
            Path.Combine(installerDir, "SDK.TapPackage"), true);

        // SmartInstaller writes Bundle/ beside tap. Isolate the entire host installation
        // for each target, rather than only giving it a separate working directory.
        var toolsDir = Path.Combine(installerDir, "tools");
        CopyDirectory(Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}"), toolsDir);
        var tap = Path.Combine(toolsDir, ExecutableName("tap"));
        MakeExecutable(tap);

        var output = Path.Combine(artifactsDir, outputName);
        // Remove stale output so a failed creation cannot leave an old installer behind.
        DeletePath(output);
        var arguments = new List<string>
        {
            "installer", "create", configuration, "-v",
            "--target-os", targetOs, "--arch", "x64", "--out", output
        };
        if (targetOs == "win" && !SigningEnabled.Value)
            arguments.Add("--no-sign");

        await RunAsync(tap, arguments.ToArray(), installerDir);
        if (!File.Exists(output) || new FileInfo(output).Length == 0)
            throw new BuildFailure($"{packagePlatform} installer was not created: {output}");
        Info($"Created {packagePlatform} installer: {output}");
    }

    private static void PrintHelp() => Console.WriteLine("""
        Usage: ./CI/build.sh [options]

        Builds OpenTAP for all configured targets, SDK and NuGet packages,
        templates, and Windows/Linux x64 installers. Publishing is opt-in.

        Options:
          -h, --help                    Show this help and exit without building.
          -v, --verbose                 Show subprocess output (captured during publishing).
          --skip-devguide               Skip PDF generation; include an empty guide placeholder.
                                        Required for local builds on macOS.
          --skip-sign                   Disable signing, even when credentials are available.
          --publish-packages            Publish OpenTAP and SDK TapPackages to packages.opentap.io.
                                        Existing package versions are skipped.
          --publish-installers          Publish installers to public downloads and the archive.
                                        Requires signing; incompatible with --skip-sign.
          --publish-retry-minutes N     Publishing retry window in minutes (default: 10).
                                        Accepts 0-1440, including fractions; 0 disables retries.
                                        Fixed 30-second waits. In-flight uploads may outlast
                                        the window. Build jobs are never retried.

        Environment:
          KS8500_REPO_TOKEN             Required to install internal build tools.
          TAP_SIGN_CERT                 Path to a non-empty signing certificate file.
          TAP_SIGN_ADDRESS              Signing service address.
          TAP_SIGN_AUTH                 Signing service credentials.
                                        Signing variables are required unless --skip-sign.
          PUBLIC_REPO_TOKEN             Required with --publish-packages.
          INSTALLER_PUBLISH_KEY         Public download credentials; required with
                                        --publish-installers.
          INSTALLER_PUBLISH_KEY_ARCHIVE  Archive credentials as username:password;
                                        required with --publish-installers.

        Missing requirements are checked before any build job starts.
        NuGet publishing remains in a separate CI job, not this script.

        Examples:
          ./CI/build.sh --skip-devguide --skip-sign
          ./CI/build.sh --publish-packages --publish-installers --publish-retry-minutes 15
        """);

    public static void ParseArguments(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--verbose" or "-v":
                    verbose = true;
                    break;
                case "--skip-devguide":
                    skipDevGuide = true;
                    break;
                case "--publish-retry-minutes":
                    if (++index >= args.Length ||
                        !double.TryParse(args[index], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var minutes) ||
                        !double.IsFinite(minutes) || minutes < 0 || minutes > 1440)
                        throw new BuildFailure("--publish-retry-minutes requires a number between 0 and 1440.");
                    publishRetryWindow = TimeSpan.FromMinutes(minutes);
                    break;
                case "--publish-packages":
                    publishPackages = true;
                    break;
                case "--publish-installers":
                    publishInstallers = true;
                    break;
                case "--skip-sign":
                    skipSign = true;
                    break;
                default:
                    throw new BuildFailure($"Unknown argument: {arg}");
            }
        }
    }

    private static string RequireSecret(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new BuildFailure($"Required secret {name} is not configured.");
        return value;
    }

    private static async Task PublishPackagesAsync()
    {
        Info("Publishing OpenTAP and SDK packages");
        // The repository upload CLI is a separate plugin, not part of core OpenTAP.
        await RunTapAsync(["package", "install", "Repository Client", "--version",
            "1.0.3-beta.9+15a03b2b", "--force"], repoRoot);
        var packages = Targets.Select(target => Path.Combine(artifactsDir,
            $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage"))
            .Append(Path.Combine(artifactsDir, "SDK.TapPackage")).ToArray();
        foreach (var package in packages)
            RequireArtifact(package);
        var arguments = new List<string>
        {
            "repo", "upload", "--repository", "https://packages.opentap.io",
            "--token", RequireSecret("PUBLIC_REPO_TOKEN"), "--parallel", "--skip-existing"
        };
        arguments.AddRange(packages);
        await RunTapAsync(arguments.ToArray(), repoRoot);
    }

    private static void RequireArtifact(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new BuildFailure($"Publishing artifact is missing or empty: {path}");
    }

    private static async Task PublishInstallersAsync()
    {
        var windows = Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.exe");
        var linux = Path.Combine(artifactsDir, "OpenTAP");
        RequireArtifact(windows);
        RequireArtifact(linux);
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var longVersion = gitVersion.Split('+', 2)[0];
        await Task.WhenAll(
            PublishInstallerAsync(client, windows, "OpenTAP.exe", "Windows", "WindowsX32", ".exe", longVersion),
            PublishInstallerAsync(client, linux, "OpenTAP", "Linux", "LinuxX64", "", longVersion));
    }

    private static async Task PublishInstallerAsync(HttpClient client, string path, string publicName,
        string os, string archivePlatform, string extension, string longVersion)
    {
        Info($"Publishing {os} installer");
        // Preserve the existing public endpoint and metadata contract.
        using (var request = new HttpRequestMessage(HttpMethod.Post,
            "http://packages.opentap.io/4.0/Objects/www/"))
        {
            request.Headers.TryAddWithoutValidation("Metadata-Version", $"\"{longVersion}\"");
            request.Headers.TryAddWithoutValidation("Metadata-OS", $"\"{os}\"");
            request.Headers.TryAddWithoutValidation("Authorization", "Token: " + RequireSecret("INSTALLER_PUBLISH_KEY"));
            using var file = File.OpenRead(path);
            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(file), "file", publicName);
            request.Content = form;
            await SendUploadAsync(client, request, $"{os} public installer");
        }

        var archiveUrl = "https://artifactory.it.keysight.com/artifactory/generic-local-automation/" +
            $"OpenTAP/{archivePlatform}/OpenTAP.{Uri.EscapeDataString(longVersion)}{extension}";
        using (var request = new HttpRequestMessage(HttpMethod.Put, archiveUrl))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(RequireSecret("INSTALLER_PUBLISH_KEY_ARCHIVE"))));
            using var file = File.OpenRead(path);
            request.Content = new StreamContent(file);
            await SendUploadAsync(client, request, $"{os} installer archive");
        }
    }

    private static async Task SendUploadAsync(HttpClient client, HttpRequestMessage request, string description)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // HttpClient timeouts are retryable failures, not user cancellation.
            throw new HttpRequestException($"Upload timed out: {description}.", ex);
        }
        using var responseScope = response;
        if (!response.IsSuccessStatusCode)
            throw new BuildFailure($"Failed to publish {description}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        Info($"Published {description}");
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
        (string Rid, string Platform, string Architecture) target)
    {
        var binDir = Path.Combine(repoRoot, "bin", "Release", target.Rid);
        var docsDestination = Path.Combine(binDir, "Packages", "OpenTAP", "doc");
        Directory.CreateDirectory(Path.GetDirectoryName(docsDestination)!);
        CopyDirectory(Path.Combine(ciDir, "doc"), docsDestination, overwrite: true);

        await RunTapAsync(["package", "create", "-v", "-c", Path.Combine(repoRoot, "package.xml"), "-o",
             Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage")],
            binDir,
            new()
            {
                ["Platform"] = target.Platform,
                ["Sign"] = SigningEnabled.Value ? "true" : "false",
                ["Architecture"] = target.Architecture
            });
    }

    private static async Task InstallOpenTapAsPackageAsync()
    {
        await RunTapAsync(["package", "install", "-f",
             Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{hostTapPlatform}.{hostArch}.TapPackage")],
            repoRoot);
    }

    private static async Task PackageTemplatesAsync()
    {
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

        // Select only this invocation's output, not packages left in artifacts by older builds.
        var outputDir = Path.Combine(ciDir, "template-package");
        DeletePath(outputDir);
        await RunAsync("dotnet", ["pack", "--configuration", "Release", "--output", outputDir], destination);
        var package = Directory.GetFiles(outputDir, "OpenTap.Templates.*.nupkg").Single();
        templatePackagePath = Path.Combine(artifactsDir, Path.GetFileName(package));
        File.Copy(package, templatePackagePath, true);
    }

    private static async Task PackageSdkAsync()
    {
        await InstallOpenTapAsPackageAsync();
        var binDir = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}");
        var sdkDir = Path.Combine(binDir, "Packages", "SDK");
        Directory.CreateDirectory(sdkDir);

        CopyDirectory(Path.Combine(repoRoot, "sdk", "Examples"), Path.Combine(sdkDir, "Examples"), true);
        File.Copy(Path.Combine(repoRoot, "Package", "PackageSchema.xsd"), Path.Combine(sdkDir, "PackageSchema.xsd"), true);
        ReplaceInFiles("$(GitVersion)", gitVersion,
            [Path.Combine(sdkDir, "Examples", "Directory.Build.props")]);

        File.Copy(templatePackagePath, Path.Combine(sdkDir, "OpenTap.Templates.nupkg"), true);

        var guide = Path.Combine(sdkDir, "Examples", "OpenTAP Developer Guide.pdf");
        if (skipDevGuide)
        {
            File.WriteAllBytes(guide, []);
        }
        else
        {
            await RunTapAsync(["package", "install", "DocumentationGeneration", "-f", "--version", "1.0.2",
                    "--repository", $"https://internal.automation.keysight.com/api/repository;token={RepoToken.Value}"],
                    binDir);

            await RunTapAsync(["generate-pdf", Path.Combine(repoRoot, "doc", "Developer Guide", "Readme.md"), "--toc",
                 "--skip-first-file", "--out", guide,
                 "--frontpage", Path.Combine(repoRoot, "doc", "Developer Guide", "Frontpage.html"),
                 "--frontpage-file", Path.Combine(repoRoot, "doc", "Developer Guide", "Frontpage.png")],
                binDir);
        }

        File.Copy(Path.Combine(repoRoot, "bin", "Release", "Keysight.OpenTap.Sdk.MSBuild.dll"),
            Path.Combine(binDir, "Keysight.OpenTap.Sdk.MSBuild.dll"), true);
        File.Copy(Path.Combine(repoRoot, "bin", "Release", "OpenTap.Sdk.New.dll"),
            Path.Combine(binDir, "OpenTap.Sdk.New.dll"), true);

        await RunTapAsync(["package", "create", "-v", "-c", Path.Combine(repoRoot, "sdk", "sdk.package.xml"), "-o",
             Path.Combine(artifactsDir, "SDK.TapPackage")], binDir);
    }

    private static async Task PackageNuGetAsync()
    {
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

        foreach (var target in Targets)
        {
            var runtime = target.Rid.Replace("osx-", "macos-", StringComparison.Ordinal);
            var package = Path.Combine(artifactsDir,
                $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage");
            ExtractZip(package, Path.Combine(nugetDir, "build", "runtimes", runtime));
        }

        await RunAsync("dotnet", ["pack", Path.Combine(nugetDir, "OpenTAP.Pack.csproj"), "--configuration", "Release", "--output", artifactsDir],
            repoRoot);
    }

    private static async Task RunTapAsync(string[] arguments, string workingDirectory,
        Dictionary<string, string>? environment = null)
    {
        var tap = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}", ExecutableName("tap"));
        MakeExecutable(tap);
        await RunAsync(tap, arguments, workingDirectory, environment);
    }

    private static async Task RunAsync(string executable, string[] arguments,
        string workingDirectory, Dictionary<string, string>? environment = null)
    {
        var failureMessage = $"[{executable}]({string.Join(", ", arguments)})";
        var result = await StartAsync(executable, arguments, workingDirectory, environment, capture: !verbose || publishPackages || publishInstallers);
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

    private static void Write(TextWriter w, string message)
    {
        // Redact credentials without resolving lazy secrets, so preflight errors remain loggable.
        foreach (var name in new[] { "KS8500_REPO_TOKEN", "PUBLIC_REPO_TOKEN",
            "INSTALLER_PUBLISH_KEY", "INSTALLER_PUBLISH_KEY_ARCHIVE", "TAP_SIGN_AUTH", "NUGET_KEY" })
        {
            var secret = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(secret))
                continue;
            message = message.Replace(secret, "****");
            message = message.Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)), "****");
        }
        w.WriteLine(message);
    }
    private static void Info(string message) => Write(Console.Out, message);
    private static void Warn(string message) => Write(Console.Error, $"\e[1;33mWARN: {message}\e[0m");
    public static void Error(string message) => Write(Console.Error, $"\e[1;31mERROR: {message}\e[0m");

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Output => string.Concat(StandardOutput, StandardError);
    }

}

sealed class BuildFailure(string message) : Exception(message);

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

// Experimental smartinstaller with some minor fixes needed for this script.
// https://github.com/opentap/smartinstaller/pull/385
string smartInstallerVersion = "3.4.0-alpha.5.2+b10989c9.package-sources";
string RepositoryClientVersion = "1.0.3-beta.9+15a03b2b";
string DocumentationGenerationVersion = "1.0.2";
BuildJob setupOptions = null!;
bool Verbose() => setupOptions.GetOption("verbose");
BuildDisplay? display = null;
BuildJob sdkOptions = null!;
BuildJob signingOptions = null!;
bool SkipSign() => signingOptions.GetOption("skip-sign");
string repoRoot = "";
string ciDir = "";
string artifactsDir = "";
string hostPlatform = "";
string hostTapPlatform = "";
string hostArch = "";
string gitVersion = "";
GitHubArtifactUploader? githubUploader = null;
Lazy<string> RepoToken = new(() =>
    Environment.GetEnvironmentVariable("KS8500_REPO_TOKEN") ??
    throw new BuildFailure("KS8500_REPO_TOKEN is not configured."));

Lazy<bool> SigningEnabled = new(() =>
{
    var certificate = Environment.GetEnvironmentVariable("TAP_SIGN_CERT");
    return !SkipSign() && !string.IsNullOrEmpty(RepoToken.Value) &&
           !string.IsNullOrEmpty(certificate) &&
           new FileInfo(certificate).Exists && new FileInfo(certificate).Length > 0;
});

(string Rid, string Platform, string Architecture)[] Targets =
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

var graph = new BuildGraph(Info);
var signing = graph.Node("signing", "Check signing requirements.")
    .Option("skip-sign", "Disable signing.");
signingOptions = signing;
bool SigningRequested() => !signing.GetOption("skip-sign");
signing.RequiresSecret("KS8500_REPO_TOKEN", when: SigningRequested)
    .RequiresSecret("TAP_SIGN_CERT", "Path to a non-empty signing certificate file.", when: SigningRequested)
    .RequiresSecret("TAP_SIGN_ADDRESS", "Signing service address.", when: SigningRequested)
    .RequiresSecret("TAP_SIGN_AUTH", "Signing service credentials.", when: SigningRequested)
    .Requires(() => !SigningRequested() || SigningEnabled.Value,
        "Signing certificate is missing or empty. Use --skip-sign for an unsigned build.");

var setup = graph.Node("setup", "Prepare the build environment.")
    .Option("verbose", "Show captured subprocess output.")
    .DependsOn(
        graph.Job("workspace", PrepareWorkspace, "Prepare output directories."),
        graph.Job("version", CalculateVersionAsync, "Determine the build version."),
        signing);
setupOptions = setup;
var templates = graph.Artifact("template-package", PackageTemplatesAsync)
    .Produces("template-package")
    .DependsOn(setup);
var crossBuild = graph.Job("cross-build", CrossBuildAsync, "Compile binaries for all configured platforms.")
    .DependsOn(setup);
var documentation = graph.Job("documentation", PrepareDocumentation, "Prepare documentation for packaging.")
    .DependsOn(setup);
var installation = graph.Job("prepare-installation", PrepareInstallation,
        "Prepare platform installations for package operations.")
    .DependsOn(crossBuild);
var tools = graph.Job("build-tools", InstallBuildToolsAsync, "Install signing tools when signing is enabled.")
    .DependsOn(installation)
    .RequiresSecret("KS8500_REPO_TOKEN", "Internal build-tools repository token.", when: SigningRequested);

var packages = Targets.ToDictionary(target => target.Rid, target =>
    graph.Artifact($"opentap-{target.Rid}", context => PackageOpenTapAsync(context, target))
        .DependsOn(crossBuild, documentation, tools));
// The aggregate publishes one artifact holding every platform's package.
var packagesComplete = graph.Artifact("opentap-packages", context =>
    {
        var files = packages.Values.SelectMany(job => context.OutputsOf<PackageFile>(job))
            .Select(package => package.Path).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        context.Artifact("opentap-packages", artifactsDir, files);
    }, "Build OpenTAP packages for all configured platforms.")
    .Produces("opentap-packages")
    .DependsOn([.. packages.Values]);

// SDK packaging installs the host OpenTAP package and embeds the templates.
// Previously SDK depended on packagesComplete to prevent its host-installation
// mutations from overlapping other package jobs, which use the same host tap.
// That race was not confirmed; clean/dirty full-build smoke tests passed with
// this narrower dependency, avoiding unrelated packages for individual installers.
// If concurrent packaging failures surface, revisit the packagesComplete barrier.
// Resolve the host dependency without throwing during graph construction.
// Unsupported hosts are reported during preflight, not when requesting help.
var packageHostPlatform = OperatingSystem.IsWindows() ? "win" :
    OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : "unsupported";
var hostRid = $"{packageHostPlatform}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
var hostPackages = packages.Where(entry => entry.Key == hostRid).Select(entry => entry.Value).ToArray();
var sdk = graph.Artifact("sdk-package", PackageSdkAsync)
    .Produces("sdk-package")
    .Option("dummy-devguide", "Include an empty developer guide instead of generating the PDF.")
    .DependsOn(templates)
    .DependsOn(hostPackages)
    .Requires(() => hostPackages.Length == 1, $"No OpenTAP package is configured for host {hostRid}.");
sdkOptions = sdk;
sdk.RequiresSecret("KS8500_REPO_TOKEN", when: () => !sdk.GetOption("dummy-devguide"));
sdk.Requires(() => sdk.GetOption("dummy-devguide") || !OperatingSystem.IsMacOS(),
    "Cannot generate developer guide PDF on macOS. Use --dummy-devguide.");
var nuget = graph.Artifact("nuget-package", PackageNuGetAsync)
    .Produces("nuget-package")
    .DependsOn(packagesComplete);

var windowsInstaller = graph.Artifact("installer-windows", context => BuildInstallerAsync(context, "installer-windows",
    "win", "Windows", "opentap.installer.xml", $"OpenTAP.{gitVersion}.exe"))
    .Produces("installer-windows")
    .DependsOn(sdk, packages["win-x64"])
    .RequiresSecret("KS8500_REPO_TOKEN");
// Each installer gets a private tooling copy, so both can run concurrently.
var linuxInstaller = graph.Artifact("installer-linux", context => BuildInstallerAsync(context, "installer-linux",
    "linux", "Linux", "opentap.installer.linux.xml", "OpenTAP"))
    .Produces("installer-linux")
    .DependsOn(sdk, packages["linux-x64"])
    .RequiresSecret("KS8500_REPO_TOKEN");

graph.Artifact("all", "Build OpenTAP, SDK, NuGet and template packages, and Windows/Linux installers. No publishing.")
    .DependsOn(templates, sdk, nuget, windowsInstaller, linuxInstaller);

var publishPackages = graph.Artifact("publish-packages", PublishPackagesAsync, "Publish OpenTAP and SDK packages.")
    .DependsOn(sdk, packagesComplete)
    .RequiresSecret("PUBLIC_REPO_TOKEN", "Public package repository token.");
ConfigurePublishingRetry(publishPackages);
var publishInstallers = graph.Artifact("publish-installers", PublishInstallersAsync, "Publish installers to public downloads and the archive.")
    .DependsOn(windowsInstaller, linuxInstaller)
    .RequiresSecret("INSTALLER_PUBLISH_KEY", "Public download credentials.")
    .RequiresSecret("INSTALLER_PUBLISH_KEY_ARCHIVE", "Archive credentials as username:password.")
    .RequiresSecret("TAP_SIGN_CERT")
    .RequiresSecret("TAP_SIGN_ADDRESS")
    .RequiresSecret("TAP_SIGN_AUTH")
    .Requires(() => SigningEnabled.Value,
        "Installer publishing requires a signed build; --skip-sign cannot be used.")
    .Requires(() => RequireSecret("INSTALLER_PUBLISH_KEY_ARCHIVE").Contains(':'),
        "INSTALLER_PUBLISH_KEY_ARCHIVE must have the form username:password.");

ConfigurePublishingRetry(publishInstallers);

// GitHub artifact uploads depend only on their own producer, so each starts as
// soon as its output is complete. Credentials are validated during preflight.
var githubTools = graph.Job("github-artifact-client", () =>
{
    int retention = int.TryParse(Environment.GetEnvironmentVariable("ARTIFACT_RETENTION_DAYS"), out var days) ? days : 14;
    githubUploader = new GitHubArtifactUploader(Path.Combine(ciDir, "github-upload-temp"), retention);
}, "Create the native GitHub artifact client.")
    .RequiresSecret("ACTIONS_RUNTIME_TOKEN", "Runner-issued artifact-service token.")
    .RequiresSecret("ACTIONS_RESULTS_URL", "GitHub artifact-service endpoint.");
// Only the artifacts of the jobs listed here go to GitHub; producing an artifact does
// not imply uploading it. Artifact names are the contract with downstream workflow jobs
// (download-artifact). The producers publish the files at runtime, so paths live there.
BuildJob[] githubArtifactJobs = [packagesComplete, sdk, nuget, templates, windowsInstaller, linuxInstaller];
var githubUploads = githubArtifactJobs.SelectMany(producer => producer.ProducedArtifacts.Select(name =>
    graph.Artifact($"upload-{name}-github-artifact", async context =>
    {
        var artifact = context.OutputsOf<BuildArtifact>(producer).Single(item => item.Name == name);
        foreach (var file in artifact.Files) RequireArtifact(file);
        context.Output(await githubUploader!.UploadAsync(new GitHubArtifact(artifact.Name, artifact.Files, artifact.RootDirectory)));
    }).DependsOn(producer, githubTools))).ToArray();
graph.Artifact("upload-github-artifacts", "Upload packages and installers as GitHub artifacts.")
    .DependsOn(githubUploads);

try
{
    var targets = graph.ParseArguments(args, defaultTarget: "all", command: "./CI/build.sh");
    if (targets.Length == 0) // Help was requested; no preflight or build side effects.
        return 0;
    var selectedJobs = graph.SelectTargets(targets);
    DetectHost();
    repoRoot = FindRepoRoot(Environment.CurrentDirectory);
    ciDir = Path.Combine(repoRoot, "CI");
    artifactsDir = Path.Combine(ciDir, "artifacts");

    // Preflight errors should be printed normally, without creating a live panel.
    graph.Validate(selectedJobs);
    using var status = new BuildDisplay(selectedJobs, !Verbose(), targets);
    display = status;
    try
    {
        await graph.RunAsync(selectedJobs);
    }
    finally
    {
        status.Dispose();
        display = null;
        githubUploader?.Dispose();
        PrintArtifactSummary();
    }
    return 0;
}
catch (Exception ex) when (ex is BuildFailure or ArgumentException)
{
    Error(ex.Message);
    return 1;
}
catch (Exception ex)
{
    Error(ex.ToString());
    return 1;
}

bool ValidRetryMinutes(double minutes) => double.IsFinite(minutes) && minutes >= 0 && minutes <= 1440;

void ConfigurePublishingRetry(BuildJob job) => job
    .Argument<double>("publish-retry-minutes", 10,
        "Retry window in minutes (0-1440); 0 disables retries.",
        ValidRetryMinutes, "--publish-retry-minutes requires a number between 0 and 1440.")
    .Retry(() => TimeSpan.FromMinutes(job.GetArgument<double>("publish-retry-minutes")), TimeSpan.FromSeconds(30));

void PrepareWorkspace()
{
    Directory.CreateDirectory(artifactsDir);
    // Rebuilding locally fails if .OpenTapIgnore exists in bin/Release.
    DeletePath(Path.Combine(repoRoot, "bin", "Release", ".OpenTapIgnore"));
}

async Task CalculateVersionAsync()
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

Task CrossBuildAsync() => RunAsync("dotnet",
    ["build", Path.Combine(ciDir, "multi-target.slnx"), "--configuration", "Release"], repoRoot);

void PrepareInstallation()
{
    // Cross-build leaves OpenTap.dll in bin/Release as well as in RID subdirectories.
    // The marker prevents bin/Release from being treated as a parent installation,
    // allowing package installs into the host RID directory (including SDK's
    // OpenTAP install). This is needed even when signing tools are skipped.
    // Write it after cross-build; PrepareWorkspace removes it before rebuilding.
    File.WriteAllText(Path.Combine(repoRoot, "bin", "Release", ".OpenTapIgnore"), "");
}

async Task InstallBuildToolsAsync()
{
    if (SigningEnabled.Value)
    {
        var ks8500 = $"https://internal.automation.keysight.com/api/repository;token={RepoToken.Value}";
        await RunTapAsync(["package", "install", "Sign", "-f", "--repository", ks8500], repoRoot);
    }
}

async Task BuildInstallerAsync(BuildContext context, string artifactName, string targetOs, string packagePlatform,
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
    // Only installer targets need SmartInstaller. Install it in their private
    // tooling copy rather than mutating the shared host used by package jobs.
    await RunAsync(tap, ["package", "install", "SmartInstaller", "-f", "--version",
        smartInstallerVersion, "--repository",
        $"https://internal.automation.keysight.com/api/repository;token={RepoToken.Value}"], toolsDir);

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

    await RunAsync(tap, [.. arguments], installerDir);
    if (!File.Exists(output) || new FileInfo(output).Length == 0)
        throw new BuildFailure($"{packagePlatform} installer was not created: {output}");
    context.Artifact(artifactName, artifactsDir, output);
}

string RequireSecret(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
        throw new BuildFailure($"Required secret {name} is not configured.");
    return value;
}

async Task PublishPackagesAsync()
{
    Info("Publishing OpenTAP and SDK packages");
    // Publishing may now overlap installer creation. Install the upload plugin
    // in a private copy rather than mutating the shared host installation.
    var toolsDir = Path.Combine(ciDir, "publish-tools");
    DeletePath(toolsDir);
    CopyDirectory(Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}"), toolsDir);
    var tap = Path.Combine(toolsDir, ExecutableName("tap"));
    MakeExecutable(tap);
    await RunAsync(tap, ["package", "install", "Repository Client", "--version",
        RepositoryClientVersion, "--force"], toolsDir);
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
    await RunAsync(tap, [.. arguments], toolsDir);
}

// Lists what this run actually produced, as published by the jobs themselves.
// Failed jobs publish nothing, so only completed artifacts appear.
void PrintArtifactSummary()
{
    var artifacts = graph.Outputs<BuildArtifact>().Select(entry => entry.Output)
        .OrderBy(artifact => artifact.Name, StringComparer.Ordinal)
        .Select(artifact => (artifact.Name, Files: artifact.Files.OrderBy(file => file, StringComparer.Ordinal).Select(file =>
        {
            var relative = Path.GetRelativePath(repoRoot, file);
            if (!File.Exists(file)) return (Path: relative, Size: -1L, Hash: "");
            using var stream = File.OpenRead(file);
            return (Path: relative, Size: stream.Length,
                Hash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant());
        }).ToArray())).ToArray();
    var uploads = graph.Outputs<GitHubArtifactResult>().Select(entry => entry.Output)
        .OrderBy(result => result.Name, StringComparer.Ordinal).ToArray();
    if (artifacts.Length > 0)
    {
        Info("");
        Info("Artifacts generated");
        foreach (var artifact in artifacts)
        {
            Info($"  {artifact.Name}");
            foreach (var file in artifact.Files)
            {
                if (file.Size < 0) { Info($"    {file.Path}  (missing)"); continue; }
                Info($"    {file.Path}  {FormatSize(file.Size)}");
                Info($"      SHA256: {file.Hash}");
            }
        }
    }
    if (uploads.Length > 0)
    {
        Info("");
        Info("GitHub artifacts uploaded");
        var width = uploads.Max(result => result.Name.Length);
        foreach (var result in uploads)
        {
            Info($"  {result.Name.PadRight(width)}  {FormatSize(result.Size),10}  ID: {result.Id}");
            Info($"    SHA256: {result.Digest}");
        }
    }

    // Mirror the same information into the GitHub job summary when running in Actions.
    var summaryFile = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
    if (string.IsNullOrWhiteSpace(summaryFile) || (artifacts.Length == 0 && uploads.Length == 0)) return;
    var md = new StringBuilder();
    if (artifacts.Length > 0)
    {
        md.AppendLine("### Artifacts generated\n\n| Artifact | File | Size | SHA256 |\n| --- | --- | ---: | --- |");
        foreach (var artifact in artifacts)
            foreach (var file in artifact.Files)
                md.AppendLine($"| {artifact.Name} | `{file.Path}` | {(file.Size < 0 ? "missing" : FormatSize(file.Size))} | `{file.Hash}` |");
        md.AppendLine();
    }
    if (uploads.Length > 0)
    {
        var server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL")?.TrimEnd('/');
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var run = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
        md.AppendLine("### GitHub artifacts uploaded\n\n| Artifact | Size | ID | SHA256 (ZIP) |\n| --- | ---: | --- | --- |");
        foreach (var result in uploads)
        {
            var id = string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(run)
                ? result.Id : $"[{result.Id}]({server}/{repository}/actions/runs/{run}/artifacts/{result.Id})";
            md.AppendLine($"| {result.Name} | {FormatSize(result.Size)} | {id} | `{result.Digest}` |");
        }
    }
    try { File.AppendAllText(summaryFile, md.ToString()); }
    catch (Exception ex) { Warn($"Could not write GitHub job summary: {ex.Message}"); }
}

string FormatSize(long bytes)
{
    string[] units = ["B", "KiB", "MiB", "GiB"];
    var value = (double)bytes;
    var unit = 0;
    while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
    return unit == 0 ? $"{bytes} B" : $"{value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} {units[unit]}";
}

void RequireArtifact(string path)
{
    if (!File.Exists(path) || new FileInfo(path).Length == 0)
        throw new BuildFailure($"Publishing artifact is missing or empty: {path}");
}

async Task PublishInstallersAsync()
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

async Task PublishInstallerAsync(HttpClient client, string path, string publicName,
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

async Task SendUploadAsync(HttpClient client, HttpRequestMessage request, string description)
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

void DetectHost()
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

void SetVersionEnvironment(string shortVersion, string fullVersion)
{
    Environment.SetEnvironmentVariable("ShortVersion", shortVersion);
    Environment.SetEnvironmentVariable("GitVersion", fullVersion);
    gitVersion = fullVersion;
}

void PrepareDocumentation()
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

async Task PackageOpenTapAsync(BuildContext context,
    (string Rid, string Platform, string Architecture) target)
{
    var binDir = Path.Combine(repoRoot, "bin", "Release", target.Rid);
    var docsDestination = Path.Combine(binDir, "Packages", "OpenTAP", "doc");
    Directory.CreateDirectory(Path.GetDirectoryName(docsDestination)!);
    CopyDirectory(Path.Combine(ciDir, "doc"), docsDestination, overwrite: true);

    var package = Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{target.Platform}.{target.Architecture}.TapPackage");
    await RunTapAsync(["package", "create", "-v", "-c", Path.Combine(repoRoot, "package.xml"), "-o", package],
        binDir,
        new()
        {
            ["Platform"] = target.Platform,
            ["Sign"] = SigningEnabled.Value ? "true" : "false",
            ["Architecture"] = target.Architecture
        });
    context.Output(new PackageFile(package));
}

async Task InstallOpenTapAsPackageAsync()
{
    await RunTapAsync(["package", "install", "-f",
         Path.Combine(artifactsDir, $"OpenTAP.{gitVersion}.{hostTapPlatform}.{hostArch}.TapPackage")],
        repoRoot);
}

async Task PackageTemplatesAsync(BuildContext context)
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
    var templatePackage = Path.Combine(artifactsDir, Path.GetFileName(package));
    File.Copy(package, templatePackage, true);
    context.Artifact("template-package", artifactsDir, templatePackage);
}

async Task PackageSdkAsync(BuildContext context)
{
    var templatePackage = context.OutputsOf<BuildArtifact>(templates).Single().Files.Single();
    await InstallOpenTapAsPackageAsync();
    var binDir = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}");
    var sdkDir = Path.Combine(binDir, "Packages", "SDK");
    Directory.CreateDirectory(sdkDir);

    CopyDirectory(Path.Combine(repoRoot, "sdk", "Examples"), Path.Combine(sdkDir, "Examples"), true);
    File.Copy(Path.Combine(repoRoot, "Package", "PackageSchema.xsd"), Path.Combine(sdkDir, "PackageSchema.xsd"), true);
    ReplaceInFiles("$(GitVersion)", gitVersion,
        [Path.Combine(sdkDir, "Examples", "Directory.Build.props")]);

    File.Copy(templatePackage, Path.Combine(sdkDir, "OpenTap.Templates.nupkg"), true);

    var guide = Path.Combine(sdkDir, "Examples", "OpenTAP Developer Guide.pdf");
    if (sdkOptions.GetOption("dummy-devguide"))
    {
        File.WriteAllBytes(guide, []);
    }
    else
    {
        await RunTapAsync(["package", "install", "DocumentationGeneration", "-f", "--version", DocumentationGenerationVersion,
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

    var sdkPackage = Path.Combine(artifactsDir, "SDK.TapPackage");
    await RunTapAsync(["package", "create", "-v", "-c", Path.Combine(repoRoot, "sdk", "sdk.package.xml"), "-o", sdkPackage], binDir);
    context.Artifact("sdk-package", artifactsDir, sdkPackage);
}

async Task PackageNuGetAsync(BuildContext context)
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
        var runtimeDir = Path.Combine(nugetDir, "build", "runtimes", runtime);
        ExtractZip(package, runtimeDir);
        // Use the archive's exact payload, including package metadata and hidden
        // installation markers, rather than enumerating a mutable installation.
        using var archive = ZipFile.OpenRead(package);
        WritePayloadManifest(runtimeDir, "OpenTapRuntimeFile",
            archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).Select(entry => entry.FullName));
    }

    // These XML documentation files are added by NuGet packaging, separately
    // from the runtime TapPackages. List the same three files explicitly.
    WritePayloadManifest(docs, "OpenTapDocumentationFile",
        ["OpenTap.Package.xml", "OpenTap.xml", "Packages/OpenTAP/OpenTap.Plugins.BasicSteps.xml"]);

    // Pack into a private directory so the output is exactly this build's package,
    // not whatever else sits in the artifacts directory.
    var outputDir = Path.Combine(ciDir, "nuget-package");
    DeletePath(outputDir);
    await RunAsync("dotnet", ["pack", Path.Combine(nugetDir, "OpenTAP.Pack.csproj"), "--configuration", "Release", "--output", outputDir],
        repoRoot);
    var nupkg = Directory.GetFiles(outputDir, "*.nupkg").Single();
    var published = Path.Combine(artifactsDir, Path.GetFileName(nupkg));
    File.Copy(nupkg, published, true);
    context.Artifact("nuget-package", artifactsDir, published);
}

void WritePayloadManifest(string directory, string itemName, IEnumerable<string> files)
{
    XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
    var items = files.Select(file => file.Replace('\\', '/'))
        .Distinct(StringComparer.Ordinal).OrderBy(file => file, StringComparer.Ordinal).Select(file =>
        {
            if (Path.IsPathRooted(file) || file.Split('/').Any(part => part is ".." or ".") ||
                file.IndexOfAny(['*', '?', ';', '$', '%']) >= 0 || file == "payload.props")
                throw new BuildFailure($"Invalid payload manifest path: {file}");
            return new XElement(ns + itemName,
                new XAttribute("Include", "$(MSBuildThisFileDirectory)" + file),
                new XElement(ns + "TargetPath", file));
        });
    new XDocument(new XElement(ns + "Project", new XElement(ns + "ItemGroup", items)))
        .Save(Path.Combine(directory, "payload.props"));
}

async Task RunTapAsync(string[] arguments, string workingDirectory,
    Dictionary<string, string>? environment = null)
{
    var tap = Path.Combine(repoRoot, "bin", "Release", $"{hostPlatform}-{hostArch}", ExecutableName("tap"));
    MakeExecutable(tap);
    await RunAsync(tap, arguments, workingDirectory, environment);
}

async Task RunAsync(string executable, string[] arguments,
    string workingDirectory, Dictionary<string, string>? environment = null)
{
    var failureMessage = $"[{executable}]({string.Join(", ", arguments)})";
    var result = await StartAsync(executable, arguments, workingDirectory, environment);
    if (result.ExitCode != 0)
        throw new BuildFailure($"{failureMessage}:\n{result.Output}");
    if (Verbose() && !string.IsNullOrEmpty(result.Output))
        Info(result.Output);
}

async Task<ProcessResult> StartAsync(string executable, string[] arguments,
    string workingDirectory, Dictionary<string, string>? environment)
{
    var start = new ProcessStartInfo(executable)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    if (environment is not null)
        foreach (var pair in environment)
            start.Environment[pair.Key] = pair.Value;

    using var process = Process.Start(start) ?? throw new BuildFailure($"Failed to start {executable}.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new(process.ExitCode, await stdout, await stderr);
}

void ReplaceInFiles(string oldValue, string newValue, IEnumerable<string> files)
{
    foreach (var file in files)
        File.WriteAllText(file, File.ReadAllText(file).Replace(oldValue, newValue, StringComparison.Ordinal));
}

void ExtractZip(string archive, string destination)
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

void DeletePath(string path)
{
    if (Directory.Exists(path))
        Directory.Delete(path, true);
    else if (File.Exists(path))
        File.Delete(path);
}

void MakeExecutable(string path)
{
    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) |
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
}

string ExecutableName(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

string FindRepoRoot(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, ".gitversion")) &&
            File.Exists(Path.Combine(directory.FullName, "OpenTAP.slnx")))
            return directory.FullName;

    throw new BuildFailure("Could not locate the OpenTAP repository root.");
}

void CopyDirectory(string source, string destination, bool overwrite = false)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (overwrite && OperatingSystem.IsWindows() && File.Exists(target))
        {
            // Windows rejects overwriting hidden/read-only destinations, including
            // .OpenTapIgnore markers left by a previous build or package install.
            var attributes = File.GetAttributes(target);
            var writableAttributes = attributes & ~(FileAttributes.Hidden | FileAttributes.ReadOnly);
            if (attributes != writableAttributes)
                File.SetAttributes(target, writableAttributes);
        }
        File.Copy(file, target, overwrite);
    }
}

void Write(TextWriter w, string message)
{
    // Redact credentials without resolving lazy secrets, so preflight errors remain loggable.
    foreach (var name in new[] { "KS8500_REPO_TOKEN", "PUBLIC_REPO_TOKEN",
        "INSTALLER_PUBLISH_KEY", "INSTALLER_PUBLISH_KEY_ARCHIVE", "TAP_SIGN_AUTH", "NUGET_KEY", "ACTIONS_RUNTIME_TOKEN" })
    {
        var secret = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(secret))
            continue;
        message = message.Replace(secret, "****");
        message = message.Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)), "****");
    }
    if (display is { } status)
        status.Write(w, message);
    else
        w.WriteLine(message);
}
void Info(string message) => Write(Console.Out, message);
void Warn(string message) => Write(Console.Error, $"\e[1;33mWARN: {message}\e[0m");
void Error(string message) => Write(Console.Error, $"\e[1;31mERROR: {message}\e[0m");


// A package file produced by one platform job, collected by the opentap-packages aggregate.
sealed record PackageFile(string Path);

sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => string.Concat(StandardOutput, StandardError);
}

sealed class BuildFailure(string message) : Exception(message);

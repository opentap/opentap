using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using OpenTap;
using OpenTap.Diagnostic;
using OpenTap.Package;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal interface IImageDeployer
    {
        void Install(ImageSpecifier spec, Installation install, CancellationToken cts);
    }

    internal class DefaultImageDeployer : IImageDeployer
    {
        public void Install(ImageSpecifier spec, Installation install, CancellationToken cts)
        {
            if (!spec.Repositories.Any(r => r.ToLower().Contains("https://packages.opentap.io") || r.ToLower().Contains("http://packages.opentap.io")))
                spec.Repositories.Add("https://packages.opentap.io");
            spec.MergeAndDeploy(install, cts);
        }
    }

    internal class OpenTapImageInstaller : IDisposable
    {
        public string TapDir { get; set; }
        public string RuntimeDir { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public PackageDef OpenTapNugetPackage { get; set; }
        public IImageDeployer ImageDeployer { get; set; }

        /// <summary>
        /// This object represents a trace listener of the type EventTraceListener from the <see cref="OpenTAP"/> assembly
        /// </summary>
        private EventTraceListener traceListener;

        void onEvent(IEnumerable<Event> events)
        {
            // These sources are really loud and not relevant to the image install.
            // In addition, messages logged at the error level are treated as build errors by MSBuild.
            // Skip these log sources.
            var mutedSources = new HashSet<string>()
            {
                "Searcher", "PluginManager", "TypeDataProvider", "Resolver", "Serializer"
            };

            foreach (var evt in events)
            {
                if (mutedSources.Contains(evt.Source)) continue;

                var msg = $"{evt.Source} : {evt.Message}";
                LogMessage(msg, evt.EventType, null);
            }
        }

        /// <summary>
        /// Instantiate an OpenTAP trace listener and create a delegate to handle log messages
        /// </summary>
        /// <exception cref="Exception"></exception>
        void attachTraceListener()
        {
            traceListener = new EventTraceListener();
            traceListener.MessageLogged += onEvent;
            Log.AddListener(traceListener);
        }

        public OpenTapImageInstaller(string tapDir, string runtimeDir, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            TapDir = tapDir;
            RuntimeDir = runtimeDir; 
            attachTraceListener();
        }

        /// <summary>
        /// Creates a merged image of currently installed packages and the packages contained in ITaskItem[]
        /// and deploys it to the output directory.
        /// </summary>
        /// <param name="packagesToInstall"></param>
        /// <param name="repositories"></param>
        /// <returns></returns>
        public bool InstallImage(ITaskItem[] packagesToInstall, List<string> repositories)
        {
            bool success = true;

            try
            {
                var install = new Installation(TapDir);

                OpenTapNugetPackage = install.GetOpenTapPackage();

                var imageSpecifier = ImageSpecifierFromTaskItems(packagesToInstall);
                imageSpecifier.Repositories.AddRange(repositories);

                var openTapSpec = new PackageSpecifier("OpenTAP",
                    VersionSpecifier.Parse(OpenTapNugetPackage.Version.ToString()));

                imageSpecifier.Packages.Add(openTapSpec);

                ImageDeployer ??= new DefaultImageDeployer();

                try
                {
                    ImageDeployer.Install(imageSpecifier, install, CancellationToken);
                }
                catch (ImageResolveException ex)
                {
                    LogMessage(ex.Message, (int)LogEventType.Error, null);
                    LogMessage("Unable to resolve image.", (int)LogEventType.Error, null);
                    success = false;
                }
            }
            catch (AggregateException aex)
            {
                LogMessage(aex.Message, (int)LogEventType.Error, null);
                foreach (var ex in aex.InnerExceptions)
                {
                    LogMessage(ex.Message, (int)LogEventType.Error, null);
                }

                success = false;
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, (int)LogEventType.Error, null);
                success = false;
            }
            finally
            {
                if (success == false)
                {
                    LogMessage($"Failed to install packages.", (int)LogEventType.Error, null);
                }
            }

            return success;
        }


        public Action<string, int, ITaskItem> LogMessage = (msg, evt, item) => { };

        public void Dispose()
        {
            Log.RemoveListener(traceListener);
        }

        private ImageSpecifier ImageSpecifierFromTaskItems(ITaskItem[] items)
        {
            var spec = new ImageSpecifier();
            foreach (var i in items)
            {
                var name = i.ItemSpec;
                var versionString = i.GetMetadata("Version");

                // The version of OpenTAP installed through nuget is added manually and should not be considered from task items
                if (name == "OpenTAP")
                {
                    // The version was omitted - this is fine
                    if (string.IsNullOrWhiteSpace(versionString)) continue;
                    if (VersionSpecifier.TryParse(versionString, out var versionSpecifier))
                    {
                        // The requested version is compatible with the installed version -- this is fine
                        if (versionSpecifier.IsCompatible(OpenTapNugetPackage.Version)) continue;
                    }

                    LogMessage(
                        $"This project was restored using OpenTAP version '{OpenTapNugetPackage.Version}', but version " +
                        $"'{versionString}' was requested. Changing the version of OpenTAP installed through nuget " +
                        $"can have unpredictable results and is not supported. Please omit the version from this element.",
                        (int)LogEventType.Warning, i);

                    continue;

                }

                var archString = i.GetMetadata("Architecture");
                var os = i.GetMetadata("OS");

                if (Enum.TryParse(archString, out CpuArchitecture arch) == false)
                    arch = CpuArchitecture.Unspecified;

                if (VersionSpecifier.TryParse(versionString, out var version) == false)
                {
                    LogMessage($"String '{versionString}' is not a valid version specifier." +
                               $" Falling back to latest release.", (int)LogEventType.Warning, i);
                    version = VersionSpecifier.Parse("");
                }

                spec.Packages.Add(new PackageSpecifier(name, version, arch, os));
            }

            return spec;
        }
    }
}

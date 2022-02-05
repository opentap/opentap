using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using OpenTap;
using OpenTap.Diagnostic;
using OpenTap.Package;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal class OpenTapImageInstaller : IDisposable
    {
        public string TapDir { get; set; }
        public CancellationToken CancellationToken { get; set; }

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

        public OpenTapImageInstaller(string tapDir, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            TapDir = tapDir;
            attachTraceListener();
        }

        /// <summary>
        /// Creates a merged image of currently installed packages and the packages contained in ITaskItem[]
        /// and deploys it to the output directory.
        /// </summary>
        /// <param name="packagesToInstall"></param>
        /// <param name="repositories"></param>
        /// <returns></returns>
        public bool InstallImage(ITaskItem[] packagesToInstall, string[] repositories)
        {
            bool success = true;

            try
            {
                var install = new Installation(TapDir);

                var imageSpecifier = ImageSpecifierFromTaskItems(packagesToInstall);
                imageSpecifier.Repositories.AddRange(repositories);
                var opentap = install.GetOpenTapPackage();

                imageSpecifier.Packages.Add(new PackageSpecifier(opentap.Name,
                    new VersionSpecifier(opentap.Version.Major, opentap.Version.Minor, opentap.Version.Patch,
                        opentap.Version.PreRelease, opentap.Version.BuildMetadata, VersionMatchBehavior.Exact)));

                imageSpecifier.MergeAndDeploy(install, CancellationToken);
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
            PackageSpecifier itemToPackageSpec(ITaskItem i)
            {
                var name = i.ItemSpec;
                var versionString = i.GetMetadata("Version");
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

                return new PackageSpecifier(name, version, arch, os);
            }

            var spec = new ImageSpecifier();
            spec.Packages.AddRange(items.Select(itemToPackageSpec));

            return spec;
        }
    }
}

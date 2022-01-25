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
            var logLevelMap = new Dictionary<int, string>()
            {
                [10] = "Error",
                [20] = "Warning",
                [30] = "Information",
                [40] = "Debug",
            };

            var mutedSources = new HashSet<string>()
            {
                "Searcher", "PluginManager", "TypeDataProvider", "Resolver", "Serializer"
            };

            foreach (var evt in events)
            {
                if (mutedSources.Contains(evt.Source)) continue;

                var msg = $"{evt.Source} : {evt.Message}";
                switch (logLevelMap[evt.EventType])
                {
                    case "Error":
                        OnError?.Invoke(msg);
                        break;
                    case "Warning":
                        OnWarning?.Invoke(msg);
                        break;
                    case "Information":
                        OnInfo?.Invoke(msg);
                        break;
                    case "Debug":
                        OnDebug?.Invoke(msg);
                        break;
                }
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
            var targetInstall = new Installation(TapDir);
            InstalledPackages = targetInstall.GetPackages()
                .Where(p => p.Class.Equals("system-wide", StringComparison.OrdinalIgnoreCase) == false).ToArray();
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
                OnError?.Invoke(aex.Message);
                foreach (var ex in aex.InnerExceptions)
                {
                    OnError?.Invoke(ex.Message);
                }

                success = false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                success = false;
            }
            finally
            {
                if (success == false)
                {
                    OnError?.Invoke($"Failed to install packages.");
                }
            }

            return success;
        }


        public Action<string> OnDebug;
        public Action<string> OnInfo;
        public Action<string> OnWarning;
        public Action<string> OnError;

        public void Dispose()
        {
            Log.RemoveListener(traceListener);
        }

        private PackageDef[] InstalledPackages;

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
                    version = VersionSpecifier.Any;

                return new PackageSpecifier(name, version, arch, os);
            }

            var spec = new ImageSpecifier();
            spec.Packages.AddRange(items.Select(itemToPackageSpec));

            return spec;
        }
    }
}

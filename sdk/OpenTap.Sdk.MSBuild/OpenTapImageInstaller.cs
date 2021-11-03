using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
                "Searcher", "PluginManager", "TypeDataProvider", "Resolver"
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

        public OpenTapImageInstaller(string tapDir)
        {
            TapDir = tapDir;
            attachTraceListener();
        }

        private string reflectionException = $"Error during reflection. This is likely a bug.";

        /// <summary>
        /// Takes an image specifier and returns an image identifier
        /// </summary>
        /// <param name="image">Image specifier</param>
        /// <returns></returns>
        private ImageIdentifier ResolveImage(string image)
        {
            try
            {
                var imageSpecifier = ImageSpecifier.FromString(image);
                var imageIdentifier = imageSpecifier.Resolve(CancellationToken.None);
                return imageIdentifier;
            }
            catch (TargetInvocationException ex)
            {
                // TargetInvocationException is not very helpful and usually just wraps the actual exception
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }

        /// <summary>
        /// Creates a merged image of currently installed packages and the packages contained in ITaskItem[]
        /// and deploys it to the output directory.
        /// </summary>
        /// <param name="packagesToInstall"></param>
        /// <returns></returns>
        public bool InstallImage(ITaskItem[] packagesToInstall)
        {
            bool success = true;
            var imageString = CreateJsonImageSpecifier(packagesToInstall);
            try
            {
                try
                {
                    var imageIdentifier = ResolveImage(imageString);
                    imageIdentifier.Deploy(TapDir, CancellationToken.None);
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
            }
            finally
            {
                if (success == false)
                {
                    OnError?.Invoke($"Failed to resolve image.");
                    OnDebug?.Invoke($"{string.Join('\n', imageString)}");
                }
            }

            return success;
        }

        class PackageObj
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string OS { get; set; }
            public string Architecture { get; set; }
        }

        /// <summary>
        /// Reflect the properties of an obj assumed to be a package specifier of some sort
        /// and return a <see cref="PackageObj"/> object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        PackageObj fromPackageDefObj(object obj)
        {
            var t = obj.GetType();
            var name = t.GetProperty("Name")?.GetValue(obj) as string;
            var version = t.GetProperty("Version")?.GetValue(obj)?.ToString();
            var architecture = t.GetProperty("Architecture")?.GetValue(obj)?.ToString();
            var os = t.GetProperty("OS")?.GetValue(obj) as string;

            var pkg = new PackageObj()
            {
                Name = name, Version = version, Architecture = architecture, OS = os
            };

            return pkg;
        }

        public Action<string> OnDebug;
        public Action<string> OnInfo;
        public Action<string> OnWarning;
        public Action<string> OnError;

        public void Dispose()
        {
            Log.RemoveListener(traceListener);
        }

        private List<PackageObj> installedPackages = null;
        private List<PackageObj> getInstalledPackages()
        {
            if (installedPackages == null)
            {
                var installation = new Installation(TapDir);
                var packages = installation.GetPackages();

                var result = new List<PackageObj>();

                if (packages is IEnumerable<object> e)
                {
                    result = e.Select(fromPackageDefObj).ToList();
                }

                installedPackages = result;
            }

            return installedPackages;

        }

        /// <summary>
        /// Convert the <see cref="ITaskItem[]"/> to a JSON image specifier
        /// </summary>
        /// <param name="taskItems"></param>
        /// <returns></returns>
        private string CreateJsonImageSpecifier(ITaskItem[] taskItems)
        {
            // OpenTAP should not be installed or updated through these package elements.
            // The version should only be managed through a NuGet <PackageReference/> tag.
            taskItems = taskItems.Where(t => t.ItemSpec != "OpenTAP").ToArray();
            var repositories = taskItems.Select(i => i.GetMetadata("Repository"))
                                        .Where(r => string.IsNullOrWhiteSpace(r) == false).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // packages.opentap.io is required for images to resolve in some cases. There's no harm in adding it.
            if (repositories.Any(r => r.IndexOf("packages.opentap.io", StringComparison.OrdinalIgnoreCase) > 0) == false)
                repositories.Add("packages.opentap.io");

            var items = taskItems.Select(i => new PackageObj()
            {
                Name = i.ItemSpec,
                Version = i.GetMetadata("Version"),
                OS = i.GetMetadata("OS"),
                Architecture = i.GetMetadata("Architecture")
            }).ToList();

            // Currently installed packages should be merged with the requested packages
            var installed = getInstalledPackages();
            foreach (var pkg in installed)
            {
                // .csproj elements should always take precedence over installed packages.
                if (items.Any(i => i.Name == pkg.Name) == false)
                    items.Add(pkg);
            }

            var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions() { Indented = true, SkipValidation = false }))
            {
                w.WriteStartObject();

                { // Write Packages element
                    w.WriteStartArray("Packages");
                    foreach (var item in items)
                    {
                        w.WriteStartObject();
                        w.WriteString("Name", item.Name);

                        if (string.IsNullOrWhiteSpace(item.Version) == false)
                            w.WriteString(nameof(item.Version), item.Version);
                        if (string.IsNullOrWhiteSpace(item.OS) == false)
                            w.WriteString(nameof(item.OS), item.OS);
                        if (string.IsNullOrWhiteSpace(item.Architecture) == false)
                            w.WriteString(nameof(item.Architecture), item.Architecture);

                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                }
                { // Write Repositories element
                    w.WriteStartArray("Repositories");
                    foreach (var repo in repositories)
                    {
                        w.WriteStringValue(repo);
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }

            // Utf8JsonWriter doesn't write to the memory stream before it has been disposed.
            // This means we cannot use the scoped using language feature for the writer, and we cannot return from inside
            // the Using scope.
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
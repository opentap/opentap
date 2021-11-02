using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Build.Framework;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal class OpenTapImageInstaller : IDisposable
    {
        private Assembly OpenTAP;
        private Assembly Package;
        private AssemblyLoadContext ctx;
        public string TapDir { get; set; }

        /// <summary>
        /// This object represents a trace listener of the type EventTraceListener from the <see cref="OpenTAP"/> assembly
        /// </summary>
        private object traceListener;
        void onEvent<T>(IEnumerable<T> events)
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
                var t = typeof(T);

                var source = t.GetField("Source").GetValue(evt).ToString();
                if (mutedSources.Contains(source)) continue;

                var Message = t.GetField("Message").GetValue(evt).ToString();

                var severity = (int)t.GetField("EventType").GetValue(evt);
                var logLevel = logLevelMap[severity];

                switch (logLevel)
                {
                    case "Error":
                        OnError?.Invoke($"{source} : {Message}");
                        break;
                    case "Warning":
                        OnWarning?.Invoke($"{source} : {Message}");
                        break;
                    case "Information":
                        OnInfo?.Invoke($"{source} : {Message}");
                        break;
                    case "Debug":
                        OnDebug?.Invoke($"{source} : {Message}");
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
            var traceListenerType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.Name == "EventTraceListener");
            if (traceListenerType == null) throw new Exception(reflectionException);

            traceListener = Activator.CreateInstance(traceListenerType, Array.Empty<object>());
            var evtType = traceListenerType.GetEvent("MessageLogged");
            var delegateType = evtType.EventHandlerType;

            var onEventMethodInfo =
                this.GetType().GetMethod(nameof(onEvent), BindingFlags.Instance | BindingFlags.NonPublic);
            var eventType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.FullName == "OpenTap.Diagnostic.Event");
            var genericInstance = onEventMethodInfo.MakeGenericMethod(eventType);

            var d = Delegate.CreateDelegate(delegateType, this, genericInstance);
            var addHandler = evtType.GetAddMethod();
            addHandler.Invoke(traceListener, new object[] { d });

            var iLogListenerType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.Name == "ILogListener");
            var logType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.Name == "Log");
            var addListener = logType.GetMethod("AddListener", BindingFlags.Public | BindingFlags.Static,
                new[] { iLogListenerType });
            addListener.Invoke(null, new[] { traceListener });
        }

        /// <summary>
        /// Detach the trace listener again when it's no longer needed. This is called during dispose.
        /// </summary>
        void detachTraceListener()
        {
            var iLogListenerType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.Name == "ILogListener");
            var logType = OpenTAP.ExportedTypes.FirstOrDefault(t => t.Name == "Log");
            var removeListener = logType.GetMethod("RemoveListener", BindingFlags.Public | BindingFlags.Static,
                new[] { iLogListenerType });
            removeListener.Invoke(null, new[] { traceListener });
        }

        private Assembly loadInMemory(string assemblyPath)
        {
            var ms = new MemoryStream();
            using (var fs = File.OpenRead(assemblyPath))
                fs.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ctx.LoadFromStream(ms);
        }

        public OpenTapImageInstaller(string tapDir)
        {
            TapDir = tapDir;
            ctx = new AssemblyLoadContext(nameof(OpenTapImageInstaller), true);
            // OpenTap.dll needs to be loaded for OpenTap.Package.dll to work since AssemblyLoadContext does not attempt to resolve this dependency
            var openTapDll = Path.Combine(tapDir, "OpenTap.dll");
            var openTapPackageDll = Path.Combine(tapDir, "OpenTap.Package.dll");
            // var openTapCliDll = Path.Combine(tapDir, "Packages/OpenTAP/OpenTap.Cli.dll");
            // var openTapBasicStepsDll = Path.Combine(tapDir, "Packages/OpenTAP/OpenTap.Plugins.BasicSteps.dll");

            if (File.Exists(openTapDll) == false || File.Exists(openTapPackageDll) == false)
                throw new Exception(
                    $"Could not find OpenTAP dlls in the output directory. You likely need to restore the solution..");
            OpenTAP = ctx.LoadFromAssemblyPath(openTapDll);
            Package = ctx.LoadFromAssemblyPath(openTapPackageDll);
            // ctx.LoadFromAssemblyPath(openTapCliDll);
            // ctx.LoadFromAssemblyPath(openTapBasicStepsDll);
            attachTraceListener();
        }

        private string reflectionException = $"Error during reflection. This is likely a bug.";

        /// <summary>
        /// Takes an image specifier and returns an image identifier
        /// </summary>
        /// <param name="image">Image specifier</param>
        /// <returns></returns>
        private object ResolveImage(string image)
        {
            try
            {
                var imageSpecifierType = Package.GetExportedTypes().FirstOrDefault(t => t.Name == "ImageSpecifier");
                if (imageSpecifierType == null)
                    throw new Exception(reflectionException);

                var parseMethod = imageSpecifierType.GetMethod("FromString", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
                if (parseMethod == null)
                    throw new Exception(reflectionException);

                var imageSpecifier = parseMethod.Invoke(null, new object[] { image });
                if (imageSpecifier == null)
                    throw new Exception(reflectionException);

                var imageIdentifierMethod = imageSpecifier.GetType().GetMethod("Resolve",
                        BindingFlags.Instance | BindingFlags.Public, new[] { typeof(CancellationToken) });
                if (imageIdentifierMethod == null)
                    throw new Exception(reflectionException);

                var imageIdentifier = imageIdentifierMethod.Invoke(imageSpecifier, new object[] { CancellationToken.None });
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
                    var deployMethod = imageIdentifier.GetType().GetMethod("Deploy",
                        BindingFlags.Public | BindingFlags.Instance,
                        new[] { typeof(string), typeof(CancellationToken) });
                    if (deployMethod == null)
                    {
                        OnError(reflectionException);
                        success = false;
                    }
                    else
                    {
                        deployMethod.Invoke(imageIdentifier, new object[] { TapDir, CancellationToken.None });
                    }
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
                    OnError?.Invoke($"Failed to resolve image:\n{imageString}");
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
            detachTraceListener();
            ctx.Unload();
        }

        private List<PackageObj> installedPackages = null;
        private List<PackageObj> getInstalledPackages()
        {
            if (installedPackages == null)
            {
                var installationType = Package.ExportedTypes.FirstOrDefault(t => t.Name == "Installation");
                if (installationType == null)
                    throw new Exception(reflectionException);

                var installation = Activator.CreateInstance(installationType, TapDir);

                var getPackagesMethod = installationType.GetMethod("GetPackages",
                    BindingFlags.Public | BindingFlags.Instance, Array.Empty<Type>());
                if (getPackagesMethod == null)
                    throw new Exception(reflectionException);

                var packages = getPackagesMethod.Invoke(installation, Array.Empty<object>());

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
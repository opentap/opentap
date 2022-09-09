using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Tap.Shared;

namespace OpenTap
{
    /// <summary>
    /// Representation of an assembly including its dependencies. Part of the object model used in the PluginManager
    /// </summary>
    [DebuggerDisplay("{Name} ({Location})")]
    public class AssemblyData : ITypeDataSource
    {
        private static readonly TraceSource log = Log.CreateSource("AssemblyData");
        /// <summary>
        /// The name of the assembly. This is the same as the filename without extension
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// The file from which this assembly can be loaded. The information contained in this AssemblyData object comes from this file.
        /// </summary>
        public string Location { get; }

        /// <summary> Gets the attributes of this .net assembly. </summary>
        public IEnumerable<object> Attributes => Load()?.GetCustomAttributes() ?? Enumerable.Empty<object>();

        IEnumerable<ITypeData> ITypeDataSource.Types => PluginTypes;

        /// <summary>
        /// <see cref="PluginAssemblyAttribute"/> decorating assembly, if included
        /// </summary>
        public PluginAssemblyAttribute PluginAssemblyAttribute { get; internal set; }

        /// <summary>
        /// A list of Assemblies that this Assembly references.
        /// </summary>
        public IEnumerable<AssemblyData> References { get; internal set; }

        IEnumerable<ITypeDataSource> ITypeDataSource.References => References;

        List<TypeData> pluginTypes;
        
        /// <summary>
        /// Gets a list of plugin types that this Assembly defines
        /// </summary>
        public IEnumerable<TypeData> PluginTypes => pluginTypes;

        internal void AddPluginType(TypeData typename)
        {
            if (typename == null)
                return;
            if (pluginTypes == null)
                pluginTypes = new List<TypeData>();
            pluginTypes.Add(typename);
        }

        /// <summary> The loaded state of the assembly. </summary>
        internal LoadStatus Status => assembly != null ? LoadStatus.Loaded : (failedLoad ? LoadStatus.FailedToLoad : LoadStatus.NotLoaded);
        
        /// <summary>
        /// Gets the version of this Assembly. This will return null if the version cannot be parsed.
        /// </summary>
        public Version Version { get; internal set; } = null;

        // NoSemanticVersion - marker version instead of null to show that no version has been parsed. Null is a valid value for version.
        static readonly SemanticVersion NoSemanticVersion = new SemanticVersion(-1, 0, 0, "", "invalidversion");
        
        SemanticVersion semanticVersion = NoSemanticVersion;
        
        /// <summary>
        /// Gets the version of this Assembly as a <see cref="SemanticVersion"/>. Will return null if the version is not well formatted.
        /// </summary>
        public SemanticVersion SemanticVersion
        {
            get
            {
                if (ReferenceEquals(semanticVersion, NoSemanticVersion))
                {
                    if (SemanticVersion.TryParse(RawVersion, out var version))
                        semanticVersion = version;
                    else if (Version != null)
                        semanticVersion = new SemanticVersion(Version.Major, Version.Minor, Version.Revision, null, null);
                    else
                        semanticVersion = null;
                }

                return semanticVersion;
            }
        }

        string ITypeDataSource.Version => RawVersion;
        
        /// <summary> Raw version as set by the assembly. </summary>
        internal string RawVersion { get; set; }

        internal AssemblyData(string location, Assembly preloadedAssembly = null)
        {
            Location = location;
            this.preloadedAssembly = preloadedAssembly;
        }

        /// <summary>  Optionally set for preloaded assemblies.  </summary>
        readonly Assembly preloadedAssembly;
        Assembly assembly;

        bool failedLoad;
        
        /// <summary>
        /// Returns the System.Reflection.Assembly corresponding to this. 
        /// If the assembly has not yet been loaded, this call will load it.
        /// </summary>
        public Assembly Load()
        {
            if (failedLoad)
                return null;
            if (assembly == null)
            {
                try
                {
                    var watch = Stopwatch.StartNew();
                    if (preloadedAssembly != null)
                        assembly = preloadedAssembly;
                    else
                    {
                        var _asm = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(asm => !asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location) && PathUtils.AreEqual(asm.Location, this.Location));
                        assembly = _asm;
                    }

                    if (assembly == null)
                    {
                        if (this.Name == "OpenTap")
                        {
                            assembly = typeof(PluginSearcher).Assembly;
                        }
                        else
                        {
                            assembly = Assembly.LoadFrom(Path.GetFullPath(this.Location));
                        }
                    }
                     
                    try
                    {
                        // Find attribute
                        if (PluginAssemblyAttribute != null && PluginAssemblyAttribute.PluginInitMethod != null)
                        {
                            string fullName = PluginAssemblyAttribute.PluginInitMethod;
                            // Break into namespace, class, and method name
                            string[] names = fullName.Split('.');
                            if (names.Count() < 3)
                                throw new Exception($"Could not find method {fullName} in assembly: {Location}");
                            string methodName = names.Last();
                            string className = names.ElementAt(names.Count() - 2);
                            string namespacePath = string.Join(".", names.Take(names.Count() - 2));
                            Type initClass = assembly.GetType($"{namespacePath}.{className}");
                            // Check if loaded class exists and is static (abstract and sealed) and is public
                            if (initClass == null || !initClass.IsClass || !initClass.IsAbstract || !initClass.IsSealed || !initClass.IsPublic)
                                throw new Exception($"Could not find method {fullName} in assembly: {Location}");
                            MethodInfo initMethod = initClass.GetMethod(methodName);
                            // Check if loaded method exists and is static and returns void and is public
                            if (initMethod == null || !initMethod.IsStatic || initMethod.ReturnType != typeof(void) || !initMethod.IsPublic)
                                throw new Exception($"Could not find method {fullName} in assembly: {Location}");
                            // Invoke the method and unwrap the InnerException to get meaningful error message
                            try
                            {
                                initMethod.Invoke(null, null);
                            }
                            catch (TargetInvocationException exc)
                            {
                                throw exc.InnerException;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedLoad = true;
                        assembly = null;
                        log.Error($"Failed to load plugins from {this.Location}");
                        log.Debug(ex);

                        return null;
                    }
                    log.Debug(watch, "Loaded {0}.", this.Name);
                }
                catch (SystemException ex)
                {
                    failedLoad = true;
                    StringBuilder sb = new StringBuilder(String.Format("Failed to load plugins from {0}", this.Location));
                    bool addedZoneInfo = false;
                    try
                    {
                        var zonetype = Type.GetType("System.Security.Policy.Zone");
                        if (zonetype != null)
                        {               
                            // Hack to support .net core without having to build separate assemblies.
                            dynamic zone = zonetype.GetMethod("CreateFromUrl").Invoke(null, new object[] { this.Location });
                            var sec = zone.SecurityZone.ToString();
                            if (sec.Contains("Internet") || sec.Contains("Untrusted"))

                            {
                                // The file is in an NTFS Windows operating system blocked state
                                sb.Append(" The file came from another computer and might be blocked to help protect this computer. Please unblock the file in Windows.");
                                addedZoneInfo = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Failed to check Security policy for file.");
                        log.Debug(e);
                        addedZoneInfo = true;
                    }

                    if (!addedZoneInfo)
                        sb.Append(" Error: "  + ex.Message);
                    log.Error(sb.ToString());
                    log.Debug(ex);
                }
            }
            return assembly;
        }

        /// <summary> Returns name and version as a string. </summary>
        public override string ToString() =>  $"{Name}, {RawVersion}";
    }
}

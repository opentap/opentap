//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Tap.Shared;

[assembly: OpenTap.PluginAssembly(true)]
namespace OpenTap
{
    /// <summary>
    /// Marks an assembly as one containing OpenTAP plugins.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PluginAssemblyAttribute : Attribute
    {
        /// <summary>
        /// Ask the <see cref="PluginSearcher"/> to also look for plugins among the internal types in this assembly (default is to only search in public types).
        /// </summary>
        public bool SearchInternalTypes { get; }
        /// <summary>
        /// (Optional) Full name of Plugin Init method that gets run before any other code in the plugin. Will only run once. 
        /// Requirement: Must be parameterless public static method returning void inside public static class
        /// Important note: If init method fails (throws an <see cref="Exception"/>), then NONE of the <see cref="ITapPlugin"/> types will load
        /// </summary>
        public string PluginInitMethod { get; }
        /// <summary>
        /// Marks an assembly as one containing OpenTAP plugins.
        /// </summary>
        /// <param name="SearchInternalTypes">True to ask the <see cref="PluginSearcher"/> to also look for plugins among the internal types in this assembly (default is to only search in public types).</param>
        public PluginAssemblyAttribute(bool SearchInternalTypes)
        {
            this.SearchInternalTypes = SearchInternalTypes;
        }
        /// <summary>
        /// Marks an assembly as one containing OpenTAP plugins.
        /// </summary>
        /// <param name="SearchInternalTypes">True to ask the <see cref="PluginSearcher"/> to also look for plugins among the internal types in this assembly (default is to only search in public types).</param>
        /// <param name="PluginInitMethod">Full name of Plugin Init method (<see cref="PluginInitMethod"/>)</param>
        public PluginAssemblyAttribute(bool SearchInternalTypes, string PluginInitMethod)
        {
            this.SearchInternalTypes = SearchInternalTypes;
            this.PluginInitMethod = PluginInitMethod;
        }
    }


    /// <summary>
    /// Searches assemblies for classes implementing ITapPlugin.
    /// </summary>
    public class PluginSearcher
    {
        private Options Option { get; set; }

        /// <summary>
        /// Options for Plugin Searcher.
        /// </summary>
        [Flags]
        public enum Options
        {
            /// <summary> No options </summary>
            None = 0,
            /// <summary> Allow multiple assemblies with the same name </summary>
            IncludeSameAssemblies = 1
        }

        /// <summary>
        /// Searches assemblies for classes implementing ITapPlugin.
        /// </summary>
        public PluginSearcher() { }

        /// <summary>
        /// Searches assemblies for classes implementing ITapPlugin.
        /// </summary>
        /// <param name="opts">Option setting for Plugin Searcher.</param>
        public PluginSearcher(Options opts = Options.None)
        {
            Option = opts;
        }

        private class AssemblyRef
        {
            public string Name;
            public Version Version;

            public AssemblyRef(string name, Version version)
            {
                Name = name;
                Version = version;
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode() * 17 + Version.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is AssemblyRef)
                {
                    var o = obj as AssemblyRef;
                    return (Name==o.Name) && (Version==o.Version);
                }
                return base.Equals(obj);
            }
        }

        private static readonly TraceSource log = Log.CreateSource("Searcher");
        class AssemblyDependencyGraph
        {
            private Options Option { get; set; }

            public AssemblyDependencyGraph(Options opt)
            {
                nameToAsmMap = new Dictionary<AssemblyRef, AssemblyData>();

                nameToAsmMap2 = new Dictionary<string, AssemblyRef>();
                asmNameToAsmData = new Dictionary<string, AssemblyData>();
                Assemblies = new List<AssemblyData>();
                UnfoundAssemblies = new HashSet<AssemblyRef>();

                Option = opt;
            }

            /// <summary>
            /// Returns a list of assemblies and their dependencies/references. 
            /// The list is sorted such that a dependency is before the assembly/assemblies that depend on it.
            /// </summary>
            public List<AssemblyData> Generate(IEnumerable<string> files)
            {
                if (nameToFileMap == null)
                    nameToFileMap = files.ToLookup(Path.GetFileNameWithoutExtension);
                else
                {
                    var existingFiles = nameToFileMap.SelectMany(g => g.Select(s => s));
                    nameToFileMap = existingFiles.Concat(files).Distinct().ToLookup(Path.GetFileNameWithoutExtension);
                }

                // print a warning if the same assembly is loaded more than once.
                foreach (var entry in nameToFileMap)
                {
                    var count = entry.Count();
                    if (count == 1) continue;
                    if (entry.Key.EndsWith(".resources") && entry.Key.StartsWith("Microsoft.CodeAnalysis"))
                        continue; // This improves the performance in debug builds, where lots of locale resource files are present.
                    
                    var versions = new HashSet<string>();
                    bool allInDependencies = true;
                    foreach (var file in entry)
                    {
                        try
                        {
                            if ((Path.GetDirectoryName(file)?.Contains("Dependencies") ?? false) == false)   
                                allInDependencies = false;
                            var fileVersion = FileVersionInfo.GetVersionInfo(file);
                            versions.Add(fileVersion?.FileVersion ?? "");
                        }
                        catch
                        {
                            // Accept errors here, this code is only used to print warnings.       
                        }
                    }

                    if (allInDependencies) continue; // these were only inside the dependencies folder.
                    if (versions.Count == 1) continue;

                    log.Warning("Multiple assemblies of different versions named {0} exists ", entry.Key);

                    int i = 0;
                    foreach (var file in entry)
                    {
                        string ver = "unknown";
                        try
                        {
                            ver = FileVersionInfo.GetVersionInfo(file)?.FileVersion ?? "0.0";
                        }
                        catch (Exception)
                        {
                            log.Debug("Unable to get version of {0}.", file);
                        }

                        log.Debug("Assembly {2}: {0} version: {1}", file, ver, 1 + i++);
                    }    
                }
                foreach (string file in files)
                    AddAssemblyInfo(file);

                return Assemblies;
            }
            
            private List<AssemblyData> Assemblies;
            private Dictionary<AssemblyRef, AssemblyData> nameToAsmMap;
            private Dictionary<string, AssemblyRef> nameToAsmMap2;
            private readonly Dictionary<string, AssemblyData> asmNameToAsmData;
            private ILookup<string, string> nameToFileMap;
            HashSet<AssemblyRef> UnfoundAssemblies; // for assemblies that are not in the files.

            /// <summary> Manually analyze and add an assembly file. </summary>
            internal AssemblyData AddAssemblyInfo(string file, Assembly loadedAssembly = null)
            {
                var normalizedFile = PathUtils.NormalizePath(file);
                if (nameToAsmMap2.TryGetValue(normalizedFile, out AssemblyRef asmRef2))
                {
                    return nameToAsmMap[asmRef2];
                }
                try
                {
                    var thisAssembly = new AssemblyData(file, loadedAssembly);
                    
                    List<AssemblyRef> refNames = new List<AssemblyRef>();
                    using (FileStream str = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        if(str.Length > int.MaxValue)
                            return null; // otherwise PEReader() will throw.
                        using (PEReader header = new PEReader(str, PEStreamOptions.LeaveOpen))
                        {
                            if (!header.HasMetadata)
                                return null;

                            MetadataReader metadata = header.GetMetadataReader();
                            AssemblyDefinition def = metadata.GetAssemblyDefinition();

                            // if we were asked to only prvide distinct assembly names and 
                            // this assembly name has already been encountered, just return that.
                            var fileIdentifer = Option.HasFlag(Options.IncludeSameAssemblies) ? file : def.GetAssemblyName().FullName;
                            if (asmNameToAsmData.TryGetValue(fileIdentifer, out AssemblyData data))
                                return data;

                            thisAssembly.Name = metadata.GetString(def.Name);

                            if (string.Compare(thisAssembly.Name, Path.GetFileNameWithoutExtension(file), true) != 0)
                                throw new Exception("Assembly name does not match the file name.");
                            var thisRef = new AssemblyRef(thisAssembly.Name, def.Version);

                            thisAssembly.Version = def.Version;

                            if (!nameToAsmMap.ContainsKey(thisRef))
                            {
                                nameToAsmMap.Add(thisRef, thisAssembly);
                                nameToAsmMap2[PathUtils.NormalizePath(thisAssembly.Location)] = thisRef;
                            }

                            asmNameToAsmData[fileIdentifer] = thisAssembly;

                            foreach (var asmRefHandle in metadata.AssemblyReferences)
                            {
                                var asmRef = metadata.GetAssemblyReference(asmRefHandle);
                                var name = metadata.GetString(asmRef.Name);
                                var newRef = new AssemblyRef(name, asmRef.Version);
                                if (UnfoundAssemblies.Contains(newRef))
                                {
                                    continue;
                                }
                                refNames.Add(new AssemblyRef(name, asmRef.Version));
                            }
                        }

                        List<AssemblyData> refList = null;
                        foreach (var refName in refNames)
                        {
                            if (nameToAsmMap.TryGetValue(refName, out AssemblyData asmData2))
                            {
                                if (refList == null) refList = new List<AssemblyData>();
                                refList.Add(asmData2);
                            }
                            else
                            {
                                if (nameToFileMap.Contains(refName.Name))
                                {
                                    AssemblyData asm = null;
                                    foreach (string file2 in nameToFileMap[refName.Name])
                                    {
                                        var data = AddAssemblyInfo(file2);
                                        if (data == null) continue;
                                        if (data.Version == refName.Version)
                                        {
                                            asm = data;
                                            break;
                                        }
                                        else if (Utils.Compatible(data.Version, refName.Version))
                                        {
                                            asm = data;
                                        }
                                    }
                                    if (asm != null)
                                    {
                                        if (refList == null) refList = new List<AssemblyData>();
                                        refList.Add(asm);
                                    }
                                    else
                                    {
                                        UnfoundAssemblies.Add(refName);
                                    }
                                }
                                else
                                {
                                    UnfoundAssemblies.Add(refName);
                                }
                            }
                        }
                        thisAssembly.References = (IEnumerable<AssemblyData>) refList ?? Array.Empty<AssemblyData>();
                        Assemblies.Add(thisAssembly);
                        return thisAssembly;
                    }
                }
                catch (Exception ex)
                {
                    // there was an error loading the file. Ignore that file.
                    log.Warning("Skipping assembly '{0}'. {1}", Path.GetFileName(file), ex.Message);
                    log.Debug(ex);
                    return null;
                }
            }
        }
        
        /// <summary>
        /// The assemblies found by Search. Ordered such that referenced assemblies come before assemblies that reference them.
        /// </summary>
        public IEnumerable<AssemblyData> Assemblies;

        AssemblyDependencyGraph graph = null;

        /// <summary>
        /// Searches assembly files and returns all the plugin types found in those.
        /// The search will also populate a complete list of types searched in the AllTypes property
        /// and all Assemblies found in the Assemblies property.
        /// Subsequent calls to this method will add to those properties.
        /// </summary>
        public IEnumerable<TypeData> Search(string dir)
        {
            var finder = new AssemblyFinder() { Quiet = true, IncludeDependencies = true, DirectoriesToSearch = new[] { dir } };
            IEnumerable<string> files = finder.AllAssemblies();

            return Search(files);
        }


        /// <summary> Adds an assembly outside the 'search' context. </summary>
        internal void AddAssembly(string path, Assembly loadedAssembly)
        {
            var asm = graph.AddAssemblyInfo(path, loadedAssembly);
            PluginsInAssemblyRecursive(asm);
        }

        /// <summary>
        /// Searches assembly files and returns all the plugin types found in those.
        /// The search will also populate a complete list of types searched in the AllTypes property
        /// and all Assemblies found in the Assemblies property.
        /// Subsequent calls to this method will add to those properties.
        /// </summary>
        public IEnumerable<TypeData> Search(IEnumerable<string> files)
        {
            Stopwatch timer = Stopwatch.StartNew();
            if (graph == null)
                graph = new AssemblyDependencyGraph(Option);
            Assemblies = graph.Generate(files);
            log.Debug(timer, "Ordered {0} assemblies according to references.", Assemblies.Count());

            AllTypes = new Dictionary<string, TypeData>();
            PluginTypes = new HashSet<TypeData>();
            foreach (AssemblyData asm in Assemblies)
            {
                PluginsInAssemblyRecursive(asm);
            }
            return PluginTypes;
        }

        internal readonly TypeData PluginMarkerType = new TypeData(typeof(ITapPlugin).FullName);

        private void PluginsInAssemblyRecursive(AssemblyData asm)
        {
            CurrentAsm = asm;
            ReadPrivateTypesInCurrentAsm = false;
            TypesInCurrentAsm = new Dictionary<TypeDefinitionHandle, TypeData>();
            using (FileStream file = new FileStream(asm.Location, FileMode.Open, FileAccess.Read))
            using (PEReader header = new PEReader(file, PEStreamOptions.LeaveOpen))
            {
                CurrentReader = header.GetMetadataReader();
                
                foreach (CustomAttributeHandle attrHandle in CurrentReader.CustomAttributes)
                {
                    CustomAttribute attr = CurrentReader.GetCustomAttribute(attrHandle);
                    
                    bool isPluginAssemblyAttribute = false;
                    if (attr.Constructor.Kind == HandleKind.MethodDefinition)
                    {
                        MethodDefinition ctor = CurrentReader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor);
                        isPluginAssemblyAttribute = MatchFullName(CurrentReader, ctor.GetDeclaringType(), "OpenTap", nameof(PluginAssemblyAttribute));
                    }
                    else if (attr.Constructor.Kind == HandleKind.MemberReference)
                    {
                        var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                        isPluginAssemblyAttribute = MatchFullName(CurrentReader, ctor.Parent, "OpenTap", nameof(PluginAssemblyAttribute));
                    }
                    if(isPluginAssemblyAttribute)
                    {
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider());
                        ReadPrivateTypesInCurrentAsm = bool.Parse(valueString.FixedArguments.First().Value.ToString());
                        if (valueString.FixedArguments.Count() > 1)
                        {
                            string initMethodName = valueString.FixedArguments.ElementAt(1).Value.ToString();
                            asm.PluginAssemblyAttribute = new PluginAssemblyAttribute(ReadPrivateTypesInCurrentAsm, initMethodName);
                        }
                        else
                            asm.PluginAssemblyAttribute = new PluginAssemblyAttribute(ReadPrivateTypesInCurrentAsm);
                        break;
                    }
                }
                
                foreach (var typeDefHandle in CurrentReader.TypeDefinitions)
                {
                    try
                    {
                        PluginFromTypeDefRecursive(typeDefHandle);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// All types found by the search indexed by their SearchAssembly.FullName.
        /// Null if PluginSearcher.Search has not been called.
        /// </summary>
        internal Dictionary<string, TypeData> AllTypes;

        /// <summary>
        /// Types found by the search that implement ITapPlugin.
        /// Null if PluginSearcher.Search has not been called.
        /// </summary>
        internal HashSet<TypeData> PluginTypes;

        private AssemblyData CurrentAsm;
        private bool ReadPrivateTypesInCurrentAsm = false;
        private Dictionary<TypeDefinitionHandle, TypeData> TypesInCurrentAsm;
        private MetadataReader CurrentReader;


        private TypeData PluginFromEntityRecursive(EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeReference:
                    return PluginFromTypeRef((TypeReferenceHandle)handle);
                case HandleKind.TypeDefinition:
                    return PluginFromTypeDefRecursive((TypeDefinitionHandle)handle);
                case HandleKind.TypeSpecification:
                    var baseSpec = CurrentReader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    try
                    {
                        return baseSpec.DecodeSignature(new SignatureTypeProvider(this), AllTypes);
                    }
                    catch
                    {
                        return null;
                    }
                default:
                    return null;
            }
        }

        private TypeData PluginFromTypeRef(TypeReferenceHandle handle)
        {
            string ifaceFullName = GetFullName(CurrentReader,handle);
            if (AllTypes.ContainsKey(ifaceFullName))
                return AllTypes[ifaceFullName];
            else
                return null; // This is not a type that we care about (not defined in any of the files the searcher is given)
        }

        private TypeData PluginFromTypeDefRecursive(TypeDefinitionHandle handle)
        {
            if (TypesInCurrentAsm.TryGetValue(handle, out var result))
                return result;
            TypeDefinition typeDef = CurrentReader.GetTypeDefinition(handle);
            var typeAttributes = typeDef.Attributes;
            if (ReadPrivateTypesInCurrentAsm)
            {
                if ((typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.NotPublic &&
                    (typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public &&
                    (typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPrivate &&
                    (typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic)
                    return null;
            }
            else
            {
                if ((typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public &&
                    (typeAttributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic)
                    return null;
            }

            string typeName;
            
            TypeDefinitionHandle declaringTypeHandle = typeDef.GetDeclaringType();
            if (declaringTypeHandle.IsNil)
            {
                typeName = string.Format("{0}.{1}", CurrentReader.GetString(typeDef.Namespace), CurrentReader.GetString(typeDef.Name));
            }
            else
            {
                // This is a nested type
                TypeData declaringType = PluginFromTypeDefRecursive(declaringTypeHandle);
                if (declaringType == null)
                    return null;
                typeName = string.Format("{0}+{1}", declaringType.Name, CurrentReader.GetString(typeDef.Name));
            }
            if (AllTypes.TryGetValue(typeName, out var existingPlugin))
            {
                if (existingPlugin.Assembly.Name == CurrentAsm.Name)
                {
                    // we assume this is the same plugin, just in another copy of the dll

                    // This can happen if you are creating a package with file in a subfoler. 
                    // That file will get copied, and we end up with it twice in the installation dir
                    // in that case it is important for the logic in EnumeratePlugins that this assembly also has the plugin types listed.
                    if (existingPlugin.PluginTypes != null &&
                        (CurrentAsm.PluginTypes == null || !CurrentAsm.PluginTypes.Any(t => t.Name == existingPlugin.Name)))
                    {
                        CurrentAsm.AddPluginType(existingPlugin);
                    }
                }
                return existingPlugin;
            }
            TypeData plugin = new TypeData(typeName);
            if (plugin.Name == PluginMarkerType.Name)
            {
                PluginMarkerType.Assembly = CurrentAsm;
                PluginMarkerType.TypeAttributes = typeDef.Attributes;
                AllTypes.Add(PluginMarkerType.Name, PluginMarkerType);
                TypesInCurrentAsm.Add(handle, PluginMarkerType);
                CurrentAsm.AddPluginType(PluginMarkerType);
                return PluginMarkerType;
            }
            plugin.TypeAttributes = typeDef.Attributes;
            plugin.Assembly = CurrentAsm;
            TypesInCurrentAsm.Add(handle, plugin);
            AllTypes.Add(plugin.Name, plugin);
            if (!typeDef.BaseType.IsNil)
            {
                TypeData baseType = PluginFromEntityRecursive(typeDef.BaseType);
                if (baseType != null)
                {
                    baseType.AddDerivedType(plugin);
                    plugin.AddBaseType(baseType);
                    plugin.AddPluginTypes(baseType.PluginTypes);
                }
            }

            foreach (InterfaceImplementationHandle ifaceHandle in typeDef.GetInterfaceImplementations())
            {
                EntityHandle ifaceEntity = CurrentReader.GetInterfaceImplementation(ifaceHandle).Interface;
                TypeData iface = PluginFromEntityRecursive(ifaceEntity);
                if (iface == null)
                    continue;
                iface.AddDerivedType(plugin);
                plugin.AddBaseType(iface);
                plugin.AddPluginTypes(iface.PluginTypes);
                if (iface.Name == PluginMarkerType.Name && plugin.PluginTypes == null)
                {
                    plugin.AddPluginType(plugin); // this inherits directly from ITapPlugin (otherwise it should have been picked up earlier)
                }
            }
            plugin.FinalizeCreation();
            
            foreach (CustomAttributeHandle attrHandle in typeDef.GetCustomAttributes())
            {
                CustomAttribute attr = CurrentReader.GetCustomAttribute(attrHandle);
                string attributeFullName = "";
                if (attr.Constructor.Kind == HandleKind.MethodDefinition)
                {
                    MethodDefinition ctor =
                        CurrentReader.GetMethodDefinition((MethodDefinitionHandle) attr.Constructor);
                    attributeFullName = GetFullName(CurrentReader, ctor.GetDeclaringType());

                }
                else if (attr.Constructor.Kind == HandleKind.MemberReference)
                {
                    var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle) attr.Constructor);
                    attributeFullName = GetFullName(CurrentReader, ctor.Parent);
                }

                switch (attributeFullName)
                {
                    case "OpenTap.DisplayAttribute":
                    {
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider(AllTypes));
                        string displayName =
                            GetStringIfNotNull(valueString.FixedArguments[0]
                                .Value); // the first argument to the DisplayAttribute constructor is the diaplay name
                        string displayDescription = GetStringIfNotNull(valueString.FixedArguments[1].Value);
                        string displayGroup = GetStringIfNotNull(valueString.FixedArguments[2].Value);
                        double displayOrder = (double)valueString.FixedArguments[3].Value;
                        bool displayCollapsed = bool.Parse(GetStringIfNotNull(valueString.FixedArguments[4].Value));
                        string[] displayGroups = GetStringArrayIfNotNull(valueString.FixedArguments[5].Value);
                        DisplayAttribute attrInstance = new DisplayAttribute(displayName, displayDescription,
                            displayGroup, displayOrder, displayCollapsed, displayGroups);
                        plugin.Display = attrInstance;
                    }
                        break;
                    case "System.ComponentModel.BrowsableAttribute":
                    {
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider());
                        plugin.IsBrowsable = bool.Parse(valueString.FixedArguments.First().Value.ToString());
                    }
                        break;
                    default:
                        break;
                }
            }

            // Check if the type is constructable by inspecting the available constructors
            if (plugin.createInstanceSet == false)
            {
                // Abstract types and interfaces cannot be instantiated
                if (typeAttributes.HasFlag(TypeAttributes.Interface) || typeAttributes.HasFlag(TypeAttributes.Abstract))
                {
                    plugin.CanCreateInstance = false;
                }
                else
                {
                    // The type can only be instantiated if it has a parameter-less constructor which does not require type arguments
                    bool hasGenericParameters(MethodDefinition m)
                    {
                        return m.GetGenericParameters().Count > 0;
                    }

                    bool hasParameters(MethodDefinition m)
                    {
                        return m.GetParameters().Count > 0;
                    }
                    
                    foreach (var methodHandle in typeDef.GetMethods())
                    {
                        var m = CurrentReader.GetMethodDefinition(methodHandle);

                        // This method is applicable if it is public, non-static, and has the RTSpecialName attribute
                        // The RTSpecialName attribute means that the method has a special significance explained by its name.
                        // All constructors will have this attribute, but most user-defined methods will not.
                        var attributes = m.Attributes;
                        var applicable = attributes.HasFlag(MethodAttributes.Public) &&
                                         attributes.HasFlag(MethodAttributes.Static) == false &&
                                         attributes.HasFlag(MethodAttributes.RTSpecialName);

                        if (!applicable)
                            continue;

                        if (CurrentReader.GetString(m.Name) != ".ctor")
                            continue;

                        if (hasGenericParameters(m) || hasParameters(m))
                        {
                            plugin.CanCreateInstance = false;
                            continue;
                        }
                        
                        // We know that the type is constructable, so we can stop searching.
                        plugin.CanCreateInstance = true;
                        break;
                    }
                }
            }

            if (plugin.PluginTypes != null)
            {
                PluginTypes.Add(plugin);
                CurrentAsm.AddPluginType(plugin);

                if(!plugin.Assembly.IsSemanticVersionSet)
                {
                    foreach (CustomAttributeHandle attrHandle in CurrentReader.GetAssemblyDefinition().GetCustomAttributes())
                    {
                        CustomAttribute attr = CurrentReader.GetCustomAttribute(attrHandle);
                        
                        if (attr.Constructor.Kind == HandleKind.MemberReference)
                        {
                            var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                            string attributeFullName = GetFullName(CurrentReader, ctor.Parent);
                            if(attributeFullName == typeof(System.Reflection.AssemblyInformationalVersionAttribute).FullName)
                            {
                                var valueString = attr.DecodeValue(new CustomAttributeTypeProvider(AllTypes));
                                if(SemanticVersion.TryParse(GetStringIfNotNull(valueString.FixedArguments[0].Value), out SemanticVersion infoVer)) // the first argument to the DisplayAttribute constructor is the InformationalVersion string
                                {
                                    plugin.Assembly.SemanticVersion = infoVer;
                                }
                            }
                        }
                    }
                    plugin.Assembly.IsSemanticVersionSet = true;
                }
            }
            return plugin;
        }

        private static string GetStringIfNotNull(object obj)
        {
            return obj?.ToString();
        }

        private static string[] GetStringArrayIfNotNull(object obj)
        {
            if (obj == null)
                return null;
            return (obj as IEnumerable<CustomAttributeTypedArgument<TypeData>>).Select(o => o.Value.ToString()).ToArray();
        }

        /// <summary>
        /// Helper to get the full name (namespace + name) of the type referenced by a TypeDefinitionHandle or TypeReferenceHandle
        /// </summary>
        static string GetFullName(MetadataReader metadata, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    var def = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return String.Format("{0}.{1}", metadata.GetString(def.Namespace), metadata.GetString(def.Name));
                case HandleKind.TypeReference:
                    var r = metadata.GetTypeReference((TypeReferenceHandle)handle);
                    return String.Format("{0}.{1}", metadata.GetString(r.Namespace), metadata.GetString(r.Name));
                //case HandleKind.TypeSpecification:
                //    var s = metadata.GetTypeSpecification((TypeSpecificationHandle)handle);
                //    s.DecodeSignature(new CustomAttributeTypeProvider(), null);
                //    return new PluginType ();
                default:
                    return null;
            }
        }

        bool MatchFullName(MetadataReader metadata, EntityHandle handle, string matchNamespace, string matchName)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                var def = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
                return metadata.GetString(def.Namespace) == matchNamespace && metadata.GetString(def.Name) == matchName;
                case HandleKind.TypeReference:
                var r = metadata.GetTypeReference((TypeReferenceHandle)handle);
                return metadata.GetString(r.Namespace) == matchNamespace && metadata.GetString(r.Name) == matchName;
                default:
                    return false;
            }
        }
        
        #region Providers needed by the Metadata API (not really important the way we use the API)
        struct CustomAttributeTypeProvider : ICustomAttributeTypeProvider<TypeData>
        {
            private Dictionary<string, TypeData> _types;

            public CustomAttributeTypeProvider(Dictionary<string, TypeData> types)
            {
                _types = types;
            }

            public TypeData GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return null;
            }

            public TypeData GetSystemType()
            {
                return _types["System.Type"];
            }

            public TypeData GetSZArrayType(TypeData elementType)
            {
                return null;
            }

            public TypeData GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                string fullName = GetFullName(reader,handle);
                return _types[fullName];
            }

            public TypeData GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                string fullName = GetFullName(reader, handle);
                return _types[fullName];
            }

            public TypeData GetTypeFromSerializedName(string name)
            {
                if (name == null)
                    return null;

                return _types[name];
            }

            public PrimitiveTypeCode GetUnderlyingEnumType(TypeData type)
            {
                throw new NotImplementedException();
            }

            public bool IsSystemType(TypeData type)
            {
                return type.Name == "System.Type";
            }
        }

        class SignatureTypeProvider : ISignatureTypeProvider<TypeData, Dictionary<string, TypeData>>
        {
            private readonly PluginSearcher _Searcher;
            public SignatureTypeProvider(PluginSearcher searcher)
            {
                _Searcher = searcher;
            }

            public TypeData GetArrayType(TypeData elementType, ArrayShape shape)
            {
                throw new NotImplementedException();
            }

            public TypeData GetByReferenceType(TypeData elementType)
            {
                throw new NotImplementedException();
            }

            public TypeData GetFunctionPointerType(MethodSignature<TypeData> signature)
            {
                throw new NotImplementedException();
            }

            public TypeData GetGenericInstantiation(TypeData genericType, ImmutableArray<TypeData> typeArguments)
            {
                return genericType;
            }

            public TypeData GetGenericMethodParameter(Dictionary<string, TypeData> genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public TypeData GetGenericTypeParameter(Dictionary<string, TypeData> genericContext, int index)
            {
                return null;
            }

            public TypeData GetModifiedType(TypeData modifier, TypeData unmodifiedType, bool isRequired)
            {
                throw new NotImplementedException();
            }

            public TypeData GetPinnedType(TypeData elementType)
            {
                throw new NotImplementedException();
            }

            public TypeData GetPointerType(TypeData elementType)
            {
                throw new NotImplementedException();
            }

            public TypeData GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return new TypeData("System." + typeCode);
            }

            public TypeData GetSZArrayType(TypeData elementType)
            {
                return elementType != null ? new TypeData(elementType.Name) : null;
            }

            public TypeData GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return _Searcher.PluginFromTypeDefRecursive(handle);
            }

            public TypeData GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return _Searcher.PluginFromTypeRef(handle);
            }

            public TypeData GetTypeFromSpecification(MetadataReader reader, Dictionary<string, TypeData> genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }

    /// <summary>
    /// The status of the loading operation for TypeData and AssemblyData.
    /// </summary>
    internal enum LoadStatus
    {
        /// <summary> Loading has not been done yet. </summary>
        NotLoaded = 1,
        /// <summary> This has been loaded. </summary>
        Loaded = 2,
        /// <summary> It failed to load. </summary>
        FailedToLoad = 3
    }
    

    /// <summary>
    /// Representation of an assembly including its dependencies. Part of the object model used in the PluginManager
    /// </summary>
    [DebuggerDisplay("{Name} ({Location})")]
    public class AssemblyData
    {
        private static readonly TraceSource log = Log.CreateSource("PluginManager");
        /// <summary>
        /// The name of the assembly. This is the same as the filename without extension
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// The file from which this assembly can be loaded. The information contained in this AssemblyData object comes from this file.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// <see cref="PluginAssemblyAttribute"/> decorating assembly, if included
        /// </summary>
        public PluginAssemblyAttribute PluginAssemblyAttribute { get; internal set; }

        /// <summary>
        /// A list of Assemblies that this Assembly references.
        /// </summary>
        public IEnumerable<AssemblyData> References { get; internal set; }

        List<TypeData> pluginTypes;
        
        /// <summary>
        /// Gets a list of plugin types that this Assembly defines
        /// </summary>
        public IEnumerable<TypeData> PluginTypes =>pluginTypes;

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
        /// Gets the version of this Assembly
        /// </summary>
        public Version Version { get; internal set; }
        
        /// <summary>
        /// Gets the version of this Assembly as a <see cref="SemanticVersion"/>
        /// </summary>
        public SemanticVersion SemanticVersion { get; internal set; }

        internal AssemblyData(string location, Assembly preloadedAssembly = null)
        {
            Location = location;
            this.preloadedAssembly = preloadedAssembly;
        }

        /// <summary>  Optionally set for preloaded assemblies.  </summary>
        readonly Assembly preloadedAssembly;
        Assembly assembly;

        bool failedLoad;
        internal bool IsSemanticVersionSet;

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
                    //TODO 
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
    }
}

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
        /// Marks an assembly as one containing OpenTAP plugins.
        /// </summary>
        /// <param name="SearchInternalTypes">True to ask the <see cref="PluginSearcher"/> to also look for plugins among the internal types in this assembly (default is to only search in public types).</param>
        public PluginAssemblyAttribute(bool SearchInternalTypes)
        {
            this.SearchInternalTypes = SearchInternalTypes;
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
            IEnumerable<string> files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
            files = files.Where(f => Path.GetExtension(f) == ".dll" || Path.GetExtension(f) == ".exe").ToList();

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

        internal TypeData PluginMarkerType = new TypeData
        {
            Name = typeof(ITapPlugin).FullName
        };

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
                    string attributeFullName = "";
                    if (attr.Constructor.Kind == HandleKind.MethodDefinition)
                    {
                        MethodDefinition ctor = CurrentReader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor);
                        attributeFullName = GetFullName(CurrentReader, ctor.GetDeclaringType());

                    }
                    else if (attr.Constructor.Kind == HandleKind.MemberReference)
                    {
                        var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                        attributeFullName = GetFullName(CurrentReader, ctor.Parent);
                    }
                    if(attributeFullName == typeof(PluginAssemblyAttribute).FullName)
                    {
                        var value = CurrentReader.GetBlobBytes(attr.Value);
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider());
                        ReadPrivateTypesInCurrentAsm = bool.Parse(valueString.FixedArguments.First().Value.ToString());
                        break;
                    }
                }

                foreach (var typeDefHandle in CurrentReader.TypeDefinitions)
                {
                    try
                    {
                        var st = PluginFromTypeDefRecursive(typeDefHandle);
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
            if (TypesInCurrentAsm.ContainsKey(handle))
            {
                return TypesInCurrentAsm[handle];
            }
            TypeDefinition typeDef = CurrentReader.GetTypeDefinition(handle);
            if (ReadPrivateTypesInCurrentAsm)
            {
                if ((typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NotPublic &&
                    (typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public &&
                    (typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPrivate &&
                    (typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic)
                    return null;
            }
            else
            {
                if ((typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public &&
                    (typeDef.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic)
                    return null;
            }

            TypeData plugin = new TypeData();
            TypeDefinitionHandle declaringTypeHandle = typeDef.GetDeclaringType();
            if (declaringTypeHandle.IsNil)
            {
                plugin.Name = string.Format("{0}.{1}", CurrentReader.GetString(typeDef.Namespace), CurrentReader.GetString(typeDef.Name));
            }
            else
            {
                // This is a nested type
                TypeData declaringType = PluginFromTypeDefRecursive(declaringTypeHandle);
                if (declaringType == null)
                    return null;
                plugin.Name = string.Format("{0}+{1}", declaringType.Name, CurrentReader.GetString(typeDef.Name));
            }
            if (AllTypes.ContainsKey(plugin.Name))
            {
                var existingPlugin = AllTypes[plugin.Name];
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
                        var value = CurrentReader.GetBlobBytes(attr.Value);
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider(AllTypes));
                        string displayName =
                            GetStringIfNotNull(valueString.FixedArguments[0]
                                .Value); // the first argument to the DisplayAttribute constructor is the diaplay name
                        string displayDescription = GetStringIfNotNull(valueString.FixedArguments[1].Value);
                        string displayGroup = GetStringIfNotNull(valueString.FixedArguments[2].Value);
                        double displayOrder = double.Parse(GetStringIfNotNull(valueString.FixedArguments[3].Value));
                        bool displayCollapsed = bool.Parse(GetStringIfNotNull(valueString.FixedArguments[4].Value));
                        string[] displayGroups = GetStringArrayIfNotNull(valueString.FixedArguments[5].Value);
                        DisplayAttribute attrInstance = new DisplayAttribute(displayName, displayDescription,
                            displayGroup, displayOrder, displayCollapsed, displayGroups);
                        plugin.Display = attrInstance;
                    }
                        break;
                    case "System.ComponentModel.BrowsableAttribute":
                    {
                        var value = CurrentReader.GetBlobBytes(attr.Value);
                        var valueString = attr.DecodeValue(new CustomAttributeTypeProvider());
                        plugin.IsBrowsable = bool.Parse(valueString.FixedArguments.First().Value.ToString());
                    }
                        break;
                    default:
                        break;
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
                        string attributeFullName = "";
                        if (attr.Constructor.Kind == HandleKind.MemberReference)
                        {
                            var ctor = CurrentReader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                            attributeFullName = GetFullName(CurrentReader, ctor.Parent);
                            if(attributeFullName == typeof(System.Reflection.AssemblyInformationalVersionAttribute).FullName)
                            {
                                var value = CurrentReader.GetBlobBytes(attr.Value);
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
            if (obj == null)
                return null;
            return obj.ToString();
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
                return new TypeData { Name = "System." + typeCode.ToString() };
            }

            public TypeData GetSZArrayType(TypeData elementType)
            {
                return elementType != null ? new TypeData { Name = elementType.Name } : null;
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
    /// Representation of a C#/dotnet type including its inheritance hierarchy. Part of the object model used in the PluginManager
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public partial class TypeData
    {
        /// <summary>
        /// Gets the fully qualified name of the type, including its namespace but not its assembly.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the TypeAttributes for this type. This can be used to check if the type is abstract, nested, an interface, etc.
        /// </summary>
        public TypeAttributes TypeAttributes { get; internal set; }
        
        /// <summary>
        /// Gets the Assembly that defines this type.
        /// </summary>
        public AssemblyData Assembly { get; internal set; }


        private DisplayAttribute display = null;
        /// <summary>
        /// Gets.the DisplayAttribute for this type. Null if the type does not have a DisplayAttribute
        /// </summary>
        public DisplayAttribute Display
        {
            get
            {
                if (display is null && attributes != null)
                {
                    foreach (var attr in attributes)
                    {
                        if (attr is DisplayAttribute displayAttr)
                        {
                            display = displayAttr;
                            break;
                        }
                    }
                }    
                return display;
            }
            internal set => display = value;
        }


        ICollection<TypeData> _BaseTypes;
        
        /// <summary> Gets a list of base types (including interfaces) </summary>
        internal ICollection<TypeData> BaseTypes => _BaseTypes;

        internal void FinalizeCreation()
        {
            _BaseTypes = _BaseTypes?.ToArray();
            _PluginTypes = _PluginTypes?.ToArray();
        }
        internal void AddBaseType(TypeData typename)
        {
            if (_BaseTypes == null)
                _BaseTypes = new HashSet<TypeData>();
            _BaseTypes.Add(typename);
        }

        private ICollection<TypeData> _PluginTypes;
        /// <summary>
        /// Gets a list of plugin types (i.e. types that directly implement ITapPlugin) that this type inherits from/implements
        /// </summary>
        public IEnumerable<TypeData> PluginTypes => _PluginTypes;

        internal void AddPluginType(TypeData typename)
        {
            if (typename == null)
                return;
            if (_PluginTypes == null)
                _PluginTypes = new HashSet<TypeData>();
            _PluginTypes.Add(typename);
        }
        internal void AddPluginTypes(IEnumerable<TypeData> types)
        {
            if (types == null)
                return;
            if (_PluginTypes == null)
                _PluginTypes = new HashSet<TypeData>();
            foreach (var t in types)
                _PluginTypes.Add(t);
        }

        private ICollection<TypeData> _DerivedTypes;
        /// <summary>
        /// Gets a list of types that has this type as a base type (including interfaces)
        /// </summary>
        public IEnumerable<TypeData> DerivedTypes => (IEnumerable<TypeData>) _DerivedTypes ?? Array.Empty<TypeData>();

        /// <summary>
        /// False if the type has a System.ComponentModel.BrowsableAttribute with Browsable = false.
        /// </summary>
        public bool IsBrowsable { get; internal set; }

        internal void AddDerivedType(TypeData typename)
        {
            if (_DerivedTypes == null)
                _DerivedTypes = new HashSet<TypeData>();
            else if (_DerivedTypes.Contains(typename))
                return;
            _DerivedTypes.Add(typename);
            if (BaseTypes != null)
            {
                foreach (TypeData b in BaseTypes)
                    b.AddDerivedType(typename);
            }
        }

        internal TypeData()
        {
            IsBrowsable = true;
        }
        
        private bool _FailedLoad;

        /// <summary>
        /// Returns the System.Type corresponding to this. 
        /// If the assembly in which this type is defined has not yet been loaded, this call will load it.
        /// </summary>
        public Type Load()
        {
            if (_FailedLoad) return null;
            if (type == null)
            {
                var asm = Assembly.Load();
                if(asm == null)
                {
                    _FailedLoad = true;
                    return null;
                }
                try
                {
                    type = asm.GetType(this.Name,true);
                    if (!dict.TryGetValue(type, out _))
                    {
                        dict.Add(type, this);
                    }
                }
                catch (Exception ex)
                {
                    _FailedLoad = true;
                    log.Error("Unable to load type '{0}' from '{1}'. Reason: '{2}'.", Name, Assembly.Location, ex.Message);
                    log.Debug(ex);
                }
            }
            return type;
        }

        /// <summary> The loaded state of the type. </summary>
        internal LoadStatus Status => type != null ? LoadStatus.Loaded : (_FailedLoad ? LoadStatus.FailedToLoad : LoadStatus.NotLoaded);

        static TraceSource log = Log.CreateSource("PluginManager");

        /// <summary>
        /// Returns the DisplayAttribute.Name if the type has a DisplayAttribute, otherwise the FullName without namespace
        /// </summary>
        /// <returns></returns>
        public string GetBestName()
        {
            return Display != null ? Display.Name : Name.Split('.', '+').Last();
        }
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
        /// A list of Assemblies that this Assembly references.
        /// </summary>
        public IEnumerable<AssemblyData> References { get; internal set; }

        private List<TypeData> _PluginTypes;
        /// <summary>
        /// Gets a list of plugin types that this Assembly defines
        /// </summary>
        public IEnumerable<TypeData> PluginTypes =>_PluginTypes;

        internal void AddPluginType(TypeData typename)
        {
            if (typename == null)
                return;
            if (_PluginTypes == null)
                _PluginTypes = new List<TypeData>();
            _PluginTypes.Add(typename);
        }

        /// <summary> The loaded state of the assembly. </summary>
        internal LoadStatus Status => _Assembly != null ? LoadStatus.Loaded : (_FailedLoad ? LoadStatus.FailedToLoad : LoadStatus.NotLoaded);

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
        private Assembly _Assembly;

        private bool _FailedLoad;
        internal bool IsSemanticVersionSet;

        /// <summary>
        /// Returns the System.Reflection.Assembly corresponding to this. 
        /// If the assembly has not yet been loaded, this call will load it.
        /// </summary>
        public Assembly Load()
        {
            if (_FailedLoad) return null;
            if (_Assembly == null)
            {
                try
                {
                    var watch = Stopwatch.StartNew();
                    if (preloadedAssembly != null)
                        _Assembly = preloadedAssembly;
                    else
                    {
                        var _asm = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(asm => !asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location) && PathUtils.AreEqual(asm.Location, this.Location));
                        _Assembly = _asm;
                    }

                    if (_Assembly == null)
                    {
                        if (this.Name == "OpenTap")
                        {
                            _Assembly = typeof(PluginSearcher).Assembly;
                        }
                        else
                        {
                            _Assembly = Assembly.LoadFrom(Path.GetFullPath(this.Location));
                        }
                        
                    }
                    
                        
                        

                    log.Debug(watch, "Loaded {0}.", this.Name);
                }
                catch (SystemException ex)
                {
                    _FailedLoad = true;
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
            if(_Assembly != null)
                AssemblyExtensions.lookup[_Assembly] = this.SemanticVersion;
            return _Assembly;
        }
    }

    internal static class AssemblyExtensions
    {
        // TODO: Change this to mapping from Assembly to AssemblyData instead.
        internal static ConcurrentDictionary<Assembly, SemanticVersion> lookup = new ConcurrentDictionary<Assembly, SemanticVersion>();
        internal static SemanticVersion GetSemanticVersion(this Assembly asm)
        {
            if (!lookup.ContainsKey(asm) || lookup[asm] == null)
            {
                string verString = asm.GetCustomAttributes<AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
                SemanticVersion ver = default(SemanticVersion);
                if (String.IsNullOrEmpty(verString) || !SemanticVersion.TryParse(verString, out ver))
                    verString = asm.GetCustomAttributes<AssemblyVersionAttribute>().FirstOrDefault()?.Version;
                if (String.IsNullOrEmpty(verString) || !SemanticVersion.TryParse(verString, out ver))
                    verString = asm.GetCustomAttributes<AssemblyFileVersionAttribute>().FirstOrDefault()?.Version;
                if (String.IsNullOrEmpty(verString) || !SemanticVersion.TryParse(verString, out ver))
                {
                    if(asm.IsDynamic == true || string.IsNullOrWhiteSpace(asm.Location))
                        verString = "0.0.0";
                    else if(Version.TryParse(FileVersionInfo.GetVersionInfo(asm.Location).ProductVersion, out Version pv))
                        verString = pv.ToString(3);
                    else if(Version.TryParse(FileVersionInfo.GetVersionInfo(asm.Location).FileVersion, out Version fv))
                        verString = fv.ToString(3);
                    else
                        verString = "0.0.0";

                }
                if (String.IsNullOrEmpty(verString) || !SemanticVersion.TryParse(verString, out ver))
                    Debug.Assert(false);
                lookup[asm] = ver;
            }
            return lookup[asm];
        }
    }

}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;

namespace OpenTap
{
    /// <summary>
    /// Abstract class forming the basis for all ResultListeners.
    /// </summary>
    public abstract class ResultListener : Resource, IResultListener, IEnabledResource
    {
        bool isEnabled = true;
        ///<summary> Gets or sets if this resource is enabled.</summary>
        [Browsable(false)]
        public bool IsEnabled {

            get => isEnabled;
            set
            {
                var oldValue = isEnabled;
                isEnabled = value;
                onEnabledChanged(oldValue, value);
            }
        }
        
        /// <summary> Called when IsEnabled is changed. </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected virtual void onEnabledChanged(bool oldValue, bool newValue)
        {

        }

        /// <summary>
        /// Called when a test plan starts.
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        public virtual void OnTestPlanRunStart(TestPlanRun planRun)
        {
        }

        /// <summary>
        /// Called when test plan finishes. At this point no more results will be sent to the result listener from the test plan run.  
        /// </summary>
        /// <param name="planRun">Test plan run parameters.</param>
        /// <param name="logStream">The log file from the test plan run as a stream.</param>
        public virtual void OnTestPlanRunCompleted(TestPlanRun planRun, System.IO.Stream logStream)
        {
        }

        /// <summary>
        /// Called just before a test step is started.
        /// </summary>
        /// <param name="stepRun"></param>
        public virtual void OnTestStepRunStart(TestStepRun stepRun)
        {
        }

        /// <summary>
        /// Called when a test step run is completed.
        /// Result might still be propagated to the result listener after this event.
        /// </summary>
        /// <param name="stepRun">Step run parameters.</param>
        public virtual void OnTestStepRunCompleted(TestStepRun stepRun)
        {
        }

        /// <summary>
        /// Called when a result is received.
        /// </summary>
        /// <param name="stepRunId"> Step run ID.</param>
        /// <param name="result">Result structure.</param>
        public virtual void OnResultPublished(Guid stepRunId, ResultTable result)
        {
        }
        
        // lookup keeping track of which result listeners implements OnResultPublished
        static ImmutableDictionary<Type, bool> implementsOnResultsPublished = ImmutableDictionary<Type, bool>.Empty;
        /// <summary> Returns true if the result listener has implemented OnResultPublished. This is used to optimize performance in situations where they don't. </summary>
        internal static bool ImplementsOnResultsPublished(ResultListener resultListener)
        {
            var resultListenerType = resultListener.GetType();
            if (!implementsOnResultsPublished.TryGetValue(resultListenerType, out bool doesImplement))
            {
                doesImplement = resultListenerType.MethodOverridden(typeof(ResultListener), nameof(OnResultPublished));
                implementsOnResultsPublished = implementsOnResultsPublished.Add(resultListenerType, doesImplement);
            }
            return doesImplement;
        }
    }

    /// <summary>
    /// Instructs the ResultListener not to save the 
    /// public property value as metadata for TestStep results.
    /// </summary>
    [Obsolete("This attribute is no longer in use and will be removed in a later version.")]
    [AttributeUsage(AttributeTargets.Property)]
    public class ResultListenerIgnoreAttribute : Attribute
    {

    }

    /// <summary>
    /// Represents a result parameter.
    /// </summary>
    [DebuggerDisplay("{Group} {Name} = {Value}")]
    [DataContract]
    public class ResultParameter : IParameter
    {
        /// <summary>
        /// Name of parameter.
        /// </summary>
        [DataMember]
        public readonly string Name;
        /// <summary>
        /// Group name of the parameter.
        /// </summary>
        [DataMember]
        public readonly string Group;
        /// <summary>
        /// Value of the parameter. If null, the value is the string "NULL".  
        /// </summary>
        [DataMember]
        public readonly IConvertible Value;
        /// <summary>
        /// Indicates the parameter came from a test step in a parent level above the initial object.  
        /// </summary>
        [DataMember]
        public readonly int ParentLevel;

        internal (string, string) Key => (Name, Group);
        IConvertible IParameter.Value => Value;

        string IAttributedObject.Name => Name;

        string IParameter.Group => Group;

        string IAttributedObject.ObjectType => "Parameter";

        /// <summary> Gets if this result is metadata. </summary>
        public bool IsMetaData { get; }

        /// <summary> null or the macro name representation of the ResultParameter. This will make it possible to insert the parameter value into a string. <see cref="MacroString"/></summary>
        public readonly string MacroName;

        /// <summary> Creates a result parameter with default group.</summary>
        public ResultParameter(string name, IConvertible value)
        {
            Name = name;
            Value = value ?? "NULL";
            Group = "";
            MacroName = null;
        }
        /// <summary>
        /// Initializes a new instance of ResultParameter.
        /// </summary>
        public ResultParameter(string group, string name, IConvertible value, MetaDataAttribute metadata = null, int parentLevel = 0)
        {
            Group = group ?? "";
            Name = name;
            Value = value ?? "NULL";
            ParentLevel = parentLevel;
            if (metadata != null)
            {
                MacroName = metadata.MacroName;
                IsMetaData = true;
            }
            else
            {
                MacroName = null;
            }
        }
        
        /// <summary>  Creates a new ResultParameter. </summary>
        /// <param name="group"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="macroName"></param>
        public ResultParameter(string group, string name, IConvertible value, string macroName)
        {
            Group = group;
            Name = name;
            Value = value ?? "NULL";
            
            if (macroName != null)
            {
                IsMetaData = true;
                if (macroName.Length > 0)
                    MacroName = macroName;
                else MacroName = name;
            }
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var parameter = obj as ResultParameter;
            return parameter != null &&
                   Name == parameter.Name &&
                   Group == parameter.Group &&
                   EqualityComparer<IConvertible>.Default.Equals(Value, parameter.Value) &&
                   ParentLevel == parameter.ParentLevel &&
                   MacroName == parameter.MacroName;
        }

        /// <summary>
        /// Calculates a hash code for the current object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hashCode = -1808396095;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Group);
            hashCode = hashCode * -1521134295 + EqualityComparer<IConvertible>.Default.GetHashCode(Value);
            hashCode = hashCode * -1521134295 + ParentLevel.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MacroName);
            return hashCode;
        }
    }

    // this array can be indexed and resized at the same time.
    class SafeArray<T> : IEnumerable<T>
    {
        T[][] arrays = Array.Empty<T[]>();

        int count;
        public int Count => count;

        public SafeArray(T[] init)
        {
            arrays = new[] {init};
            count = init.Length;
        }

        public SafeArray(SafeArray<T> original)
        {
            count = original.count;
            arrays = new []{new T[count]};
            int offset = 0;
            foreach (var elems in original.arrays)
            {
                elems.CopyTo(arrays[0], offset);
                offset += elems.Length;
            }
        }

        public SafeArray()
        {
            
        }

        readonly object resizeLock = new object(); 
            
        public void Resize(int newSize)
        {
            if(newSize < count)
                throw new InvalidOperationException();
            if (newSize == count) return;
            lock (resizeLock)
            {
                Array.Resize(ref arrays, arrays.Length + 1);
                arrays[arrays.Length - 1] = new T[newSize - count];
                count = newSize;
            }
        }
        
        public ref T this[int index]
        {
            get
            {
                for(int i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    if (index < array.Length)
                        return ref array[index];
                    index -= array.Length;
                }
                throw new IndexOutOfRangeException();
            }
        }

        public IEnumerator<T> GetEnumerator() =>  arrays.SelectMany(x => x).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>  GetEnumerator();
    }

    /// <summary>
    /// A collection of parameters related to the results.
    /// </summary>
    public class ResultParameters : IReadOnlyList<ResultParameter>
    {
        struct resultParameter
        {
            public string Name;
            public IConvertible Value;
            public string Group;
            public string MacroName;
            public ResultParameter ToResultParameter() => new ResultParameter(Group, Name, Value, MacroName);
            public static implicit operator resultParameter(ResultParameter v)
            {
                return new resultParameter
                {
                    Name = v.Name,
                    Value = v.Value,
                    Group = v.Group,
                    MacroName = v.MacroName ?? (v.IsMetaData ? "" : null) 
                };
            }
        }
        SafeArray<resultParameter> data = new SafeArray<resultParameter>();

        /// <summary>
        /// Gets the parameter with the given index.
        /// </summary>
        public ResultParameter this[int index] => data[index].ToResultParameter();

        /// <summary> Gets a ResultParameter by name. </summary>
        public ResultParameter Find(string name) => Find(name, "");
        
        /// <summary> Gets a ResultParameter by name. </summary>
        public ResultParameter Find(string name, string group)
        {
            if (indexByName.TryGetValue((name, group), out var idx) == false)
                return null;
            return this[idx];
        }

        /// <summary> Gets a ResultParameter by name. </summary>
        public ResultParameter Find((string name, string group) key)
        {
            if (indexByName.TryGetValue(key, out var idx) == false)
                return null;
            return this[idx];
        }
        
        int FindIndex((string name, string group) key)
        {
            if (indexByName.TryGetValue(key, out var idx))
                return idx;
            return -1;
        }

        /// <summary> Gets the parameter with the key name. </summary>
        public IConvertible this[string name, string group]
        {
            get => Find(name, group)?.Value;
            set
            {
                int index = -1;
                SetIndexed((name, group), ref index, value);
            }
        }

        /// <summary> Gets a named parameter specifying only name. This assumes that the empty group is being used. So it is the same as calling [name, ""].. </summary>
        public IConvertible this[string name]
        {
            get => this[name, ""];
            set => this[name, ""] = value;
        }

        static void getMetadataFromObject(object res, string nestedName, ICollection<ResultParameter> output)
        {
            GetPropertiesFromObject(res, output, nestedName, true);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every property on the inputted 
        /// object decorated with <see cref="MetaDataAttribute"/>.
        /// </summary>
        public static ResultParameters GetMetadataFromObject(object res)
        {
            if (res == null)
                throw new ArgumentNullException(nameof(res));
            var parameters = new List<ResultParameter>();
            getMetadataFromObject(res, "", parameters);
            return new ResultParameters(parameters);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every property on every 
        /// <see cref="ComponentSettings"/> implementation decorated with <see cref="MetaDataAttribute"/>. 
        /// </summary>
        public static ResultParameters GetComponentSettingsMetadata(bool expandComponentSettings = false)
        {
            var componentSettings = TypeData.FromType(typeof(ComponentSettings)) //get component settings instances (lazy)
                .DerivedTypes
                .Where(x => x.CanCreateInstance && ParameterCache.GetParametersMap(x, true).Any())
                .Select(td => ComponentSettings.GetCurrent(td.Type))
                .Where(o => o != null)
                .Cast<object>();

            if (expandComponentSettings)
            {
                var componentSettingsLists = TypeData
                    .FromType(typeof(ComponentSettings)) //get component settings instances (lazy)
                    .DerivedTypes
                    .Where(x => x.CanCreateInstance && x.DescendsTo(typeof(IEnumerable)))
                    .Select(td => ComponentSettings.GetCurrent(td.Type))
                    .Where(o => o != null)
                    .Cast<object>();
                
                if (expandComponentSettings)
                    componentSettings = componentSettings.Concat(componentSettingsLists.OfType<IEnumerable>().SelectMany(c => (IEnumerable<object>)c)).Distinct();
            }

            
            return new ResultParameters(componentSettings.ToArray().SelectMany(GetMetadataFromObject).ToArray());// get metadata from each.
        }

        
        /// <summary>
        /// Lazily pull result parameters from component settings. Reduces the number of component settings XML that needs to be deserialized.
        /// </summary>
        /// <param name="includeObjects">If objects in ComponentSettingsLists should be included.</param>
        /// <returns></returns>
        internal static IEnumerable<ResultParameters> GetComponentSettingsMetadataLazy(bool includeObjects)
        {
            TypeData engineSettingsType = TypeData.FromType(typeof(EngineSettings));
            AssemblyData engineAssembly = engineSettingsType.Assembly;

            int orderer(TypeData tp)
            {
                // always start with EngineSettings
                // prefer engine assemblies 
                // then loaded assemblies
                // then everything else.
                if (tp == engineSettingsType)
                    return 3;
                if (tp.Assembly == engineAssembly)
                    return 2;
                if (tp.Assembly.Status == LoadStatus.Loaded)
                    return 1;
                return 0;
            }

            var types = TypeData.FromType(typeof(ComponentSettings))
                .DerivedTypes.OrderByDescending(orderer).ToArray();

            foreach (var tp in types)
            {
                var t = tp.Load();
                if (tp.CanCreateInstance == false) continue;
                if (ParameterCache.GetParametersMap(tp, true).Any() == false)
                    continue;
                var componentSetting = ComponentSettings.GetCurrent(t);
                if (componentSetting != null)
                {
                    yield return GetMetadataFromObject(componentSetting);
                }
            }
            if (includeObjects == false) yield break;
            foreach (var tp in types)
            {
                var t = tp.Load();
                if (tp.CanCreateInstance == false) continue;
                if (tp.DescendsTo(typeof(IEnumerable)) == false) continue;
                var elements = ComponentSettings.GetCurrent(t) as IEnumerable ?? Array.Empty<object>();
                foreach (var elem in elements)
                    yield return GetMetadataFromObject(elem);
            }
        }


        /// <summary>
        /// Adds a new parameter to the resultParams list. if the parameter value is of the type Resource, every parameter from it is added, but not the origin object.
        /// </summary>
        static void GetParams(string group, string name, object value, MetaDataAttribute metadata, ICollection<ResultParameter> output)
        {
            if (value is IResource)
            {
                string resval = value.ToString();

                output.Add(new ResultParameter(group, name, resval, metadata));
                getMetadataFromObject(value, name + "/",output);
                return;
            }

            var parentName = name;

            IConvertible val;
            if (value == null)
                val = null;
            else if (value is IConvertible conv)
                val = conv;
            else if((val = StringConvertProvider.GetString(value)) == null)
                val = value.ToString();

            output.Add( new ResultParameter(group, parentName, val, metadata));
        }
        
        internal class ParameterCache
        {
            class Box
            {
                public ImmutableDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute metadata)[]> Data = ImmutableDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute metadata)[]>.Empty;
            }
            
            static readonly ThreadField<Box> cacheField = new ThreadField<Box>();
            public static void LoadCache() => cacheField.Value = new Box();
            public static IEnumerable<(IMemberData member, string group, string name, MetaDataAttribute metadata)> GetParametersMap(ITypeData type, bool metadataOnly)
            {
                if (cacheField.Value?.Data.TryGetValue(type, out var data) == true)
                {
                    if (metadataOnly)
                        return data.Where(x => x.metadata != null);
                    return data;
                }
                
                var value = getParametersMap(type, false);
                if (cacheField.Value is Box box)
                {
                    box.Data = box.Data.SetItem(type, value.ToArray());
                }
                if (metadataOnly)
                    return value.Where(x => x.metadata != null);
                return value;
            }
            static IEnumerable<(IMemberData member, string group, string name, MetaDataAttribute metadata)> getParametersMap(ITypeData type, bool metadataOnly)
            {
            
                foreach (var prop in type.GetMembers())
                {
                    if (!prop.Readable)
                        continue;
                
                    var metadataAttr = prop.GetAttribute<MetaDataAttribute>();
                    if (metadataAttr == null)
                    {
                        if (metadataOnly) continue;
                    
                        // if metadataAttr is specified, all we require is that we can read and write it. 
                        // Otherwise normal rules applies:

                        if (prop.Writable == false)
                            continue; // Don't add Properties with XmlIgnore attribute
                    
                        if (prop.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
                            continue;

                        if (!prop.IsBrowsable())
                            continue;
                    }
                
                    if (prop.HasAttribute<NonMetaDataAttribute>())
                        continue; // pretty rare case, best to check this after other things for performance.

                    var display = prop.GetDisplayAttribute();

                    if (metadataAttr != null && string.IsNullOrWhiteSpace(metadataAttr.MacroName))
                        metadataAttr = new MetaDataAttribute(metadataAttr.PromptUser, display.Name)
                        {
                            Group = metadataAttr.Group,
                            Name = metadataAttr.Name
                        };

                    var name = display.Name.Trim();
                    string group = "";

                    if (display.Group.Length == 1) group = display.Group[0].Trim();
                    group = metadataAttr?.Group ?? group;
                    name = metadataAttr?.Name ?? name;

                    yield return (prop, group, name, metadataAttr);
                }
            }
        }

        
        
        static void GetPropertiesFromObject(object obj, ICollection<ResultParameter> output, string namePrefix = "", bool metadataOnly = false)
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            foreach (var (prop, group, name, metadata) in ParameterCache.GetParametersMap(type, metadataOnly))
            {
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;
                GetParams(group, namePrefix + name, value, metadata, output);
            }
        }

        internal static void UpdateParams(ResultParameters parameters, object obj, string namePrefix = "")
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            var p = ParameterCache.GetParametersMap(type, false);
            foreach (var (prop, group, _name, metadata) in p)
            {
                if (prop.GetAttribute<MetaDataAttribute>()?.Frozen == true) continue; 
                var name = namePrefix + _name;
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;
                
                if (value is IResource)
                {
                    string resval = value.ToString();
                    parameters.Overwrite(name, resval, group, metadata);
                    UpdateParams(parameters, value, name);
                    continue;
                }
                
                IConvertible val = value as IConvertible ?? StringConvertProvider.GetString(value) ?? value.ToString();
                parameters.Overwrite(name, val, group, metadata);
            }
        }

        internal void Overwrite(string name, IConvertible value, string group, MetaDataAttribute metadata)
        {
           Add(group, name, value, metadata);
        }

        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every setting of the inputted 
        /// TestStep.
        /// </summary>
        internal static ResultParameters GetParams(ITestStep step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            var parameters = new List<ResultParameter>(5);
            GetPropertiesFromObject(step, parameters);
            
            if (parameters.Count == 0)
                return new ResultParameters();
            return new ResultParameters(parameters);
        }
        
        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters()
        {
            indexByName = new Dictionary<(string, string), int>();
        }

        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters(IEnumerable<ResultParameter> items)
        {
            addRangeUnsafe(items, true);
        }
        

        /// <summary>
        /// Returns a dictionary containing all the values in this list indexed by their <see cref="ResultParameter.Name"/>.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (ResultParameter val in this)
            {
                if(values.ContainsKey(val.Name) == false)
                    values.Add(val.Name, val.Value);
            }
            return values;
        }

        /// <summary>
        /// Returns the number of result parameters.
        /// </summary>
        public int Count => count;

        /// <summary> Adds a new element to the parameters. (synchronized). </summary>
        public void Add(ResultParameter parameter) => AddRange(new[] {parameter}); 

        int count = 0;
        
        void addRangeUnsafe(IEnumerable<ResultParameter> parameters, bool initCollection = false)
        {
            if (initCollection)
            {
                var capacity = parameters.Count();
                var data2 = new resultParameter[capacity];
                var indexByName2 = new Dictionary<(string, string), int>(capacity);
                var count2 = 0;
                foreach (var par in parameters)
                {
                    if (par?.Name == null) continue; 
                    if (indexByName2.TryGetValue(par.Key, out var idx))
                    {
                        data2[idx] = par;
                    }
                    else
                    {
                        indexByName2[par.Key] = count2;
                        data2[count2] = par;
                        count2 += 1;
                    }
                }
                // resize data to the actual count. This is a no-op if no change.
                Array.Resize(ref data2, count2);
                data = new SafeArray<resultParameter>(data2);
                indexByName = indexByName2;
                count = count2;
                return;
            }
            
            Dictionary<(string, string), int> newIndexes = null;
            List<ResultParameter> newParameters = null;
            
            foreach (var par in parameters)
            {
                if (!indexByName.TryGetValue(par.Key, out var idx))
                    idx = -1;

                if (idx >= 0)
                {
                    data[idx] = par;
                    continue;
                }

                if (newIndexes == null)
                {
                    newIndexes = new Dictionary<(string, string), int>();
                    newParameters = new List<ResultParameter>();
                }

                if (newIndexes.TryGetValue(par.Key, out idx))
                {
                    newParameters[idx - count] = par;    
                }
                else
                {
                    int nidx = count + newParameters.Count;
                    newParameters.Add(par);
                    newIndexes[par.Key] = nidx;
                }   
            }

            if (newIndexes != null)
            {
                var newSize = count + newParameters.Count;
                if(data.Count < newSize)
                    data.Resize(newSize);
                foreach (var elem in newParameters)
                {
                    data[count] = elem;
                    count += 1;
                }

                foreach (var kv in newIndexes)
                    indexByName.Add(kv.Key, kv.Value);
            }
        }

        Dictionary<(string, string), int> indexByName;

        readonly object addLock = new object();
        
        /// <summary>
        /// Adds a range of result parameters (synchronized).
        /// </summary>
        /// <param name="parameters"></param>
        public void AddRange(IEnumerable<ResultParameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            lock(addLock)
                addRangeUnsafe(parameters);
        }

        IEnumerator<ResultParameter> IEnumerable<ResultParameter>.GetEnumerator() => data.Select(x => x.ToResultParameter()).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => data.Select(x => x.ToResultParameter()).GetEnumerator();

        /// <summary> Copies all the data inside a ResultParameters instance. </summary>
        /// <returns></returns>
        internal ResultParameters Clone()
        {
            var r = new ResultParameters
            {
                data = new SafeArray<resultParameter>(data),
                indexByName = new Dictionary<(string, string), int>(indexByName)
            };
            
            return r;
        }
        

        internal IConvertible GetIndexed((string, string) index, ref int keyIndex)
        {
            if(keyIndex == -1)
                keyIndex = FindIndex(index);
            if (keyIndex == -1)
                return null;
            return data[keyIndex].Value;
        }

        internal void SetIndexed((string name, string group) key, ref int index, IConvertible value)
        {
            if(index == -1)
                index = FindIndex(key);
            if (index == -1)
            {
                Add(new ResultParameter(key.group, key.name, value));
            }
            else
            {
                var prev = data[index];
                prev.Value = value;
                prev.Name = key.name;
                prev.Group = key.group;
                data[index] = prev;
            }
        }

        
        internal void IncludeMetadataFromObject(TestPlan obj)
        {
            var metadata = GetMetadataFromObject(obj);
            AddRange(metadata);
        }

        /// <summary> Adds a new result parameter. </summary>
        public void Add(string group, string name, IConvertible value, MetaDataAttribute metaDataAttribute)
        {
            Add(new ResultParameter(group, name, value, metaDataAttribute));
        }
    }
}

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
using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.CodeAnalysis;

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
    [DebuggerDisplay("{Name} = {Value}")]
    [DataContract]
    public class ResultParameter : IParameter
    {
        /// <summary>
        /// Name of parameter.
        /// </summary>
        [DataMember]
        public readonly string Name;
        /// <summary>
        /// Pretty name of the parameter.  
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
            Group = group;
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
                IsMetaData = false;
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
            IsMetaData = macroName != null;
            MacroName = macroName;
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

        object resizeLock = new object(); 
            
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
        
        public T this[int index]
        {
            get
            {
                for(int i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    if (index < array.Length)
                        return array[index];
                    index -= array.Length;
                }
                throw new IndexOutOfRangeException();
            }
            set
            {
                for(int i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    if (index < array.Length)
                    {
                        array[index] = value;
                        return;
                    }
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
                    MacroName = v.MacroName
                };
            }
        }
        SafeArray<resultParameter> data = new SafeArray<resultParameter>();

        /// <summary>
        /// Gets the parameter with the given index.
        /// </summary>
        /// <param name="index"></param>
        public ResultParameter this[int index] => data[index].ToResultParameter();

        /// <summary> Gets a ResultParameter by name. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ResultParameter Find(string name)
        {
            int idx = 0;
            if (indexByName.TryGetValue(name, out idx) == false)
                return null;
            return this[idx];
        }
        
        int FindIndex(string name)
        {
            if (indexByName.TryGetValue(name, out var idx))
                return idx;
            return -1;
        }

        /// <summary>
        /// Gets the parameter with the key name.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IConvertible this[string key]
        {
            get => Find(key)?.Value;
            set
            {
                int index = -1;
                SetIndexed(key, ref index, value);
            }
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
                throw new ArgumentNullException("res");
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
            var componentSettings = PluginManager //get component settings instances (lazy)
                .GetPlugins<ComponentSettings>()
                .Select(ComponentSettings.GetCurrent)
                .Where(o => o != null)
                .Cast<object>();

            if (expandComponentSettings)
                componentSettings = componentSettings.Concat(componentSettings.OfType<IEnumerable>().SelectMany(c => (IEnumerable<object>)c));
            return new ResultParameters(componentSettings.SelectMany(GetMetadataFromObject).ToArray());// get metadata from each.
        }

        
        /// <summary>
        /// Lazily pull result parameters from component settings. Reduces the number of component settings XML that needs to be deserialized.
        /// </summary>
        /// <param name="includeObjects">If objects in componentsettingslists should be included.</param>
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
                var componentSetting = ComponentSettings.GetCurrent(t);
                if(componentSetting is IEnumerable elements)
                {
                    foreach(var elem in elements)
                    {
                        yield return GetMetadataFromObject(elem);
                    }
                }
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
            if (value == null)
            {
                value = "NULL";
            }

            var parentName = name;

            IConvertible val;
            if (value is IConvertible)
                val = value as IConvertible;
            else if((val = StringConvertProvider.GetString(value)) == null)
                val = value.ToString();

            output.Add( new ResultParameter(group, parentName, val, metadata));
        }

        static ConcurrentDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute
            metadata)[]> propertiesLookup =
            new ConcurrentDictionary<ITypeData, (IMemberData member, string group, string name, MetaDataAttribute
                metadata)[]>();

        static (IMemberData member, string group, string name, MetaDataAttribute metadata)[] GetParametersMap(
            ITypeData type)
        {
            if (propertiesLookup.TryGetValue(type, out var result))
                return result;
            var lst = new List<(IMemberData member, string group, string name, MetaDataAttribute metadata)>();
            foreach (var prop in type.GetMembers())
            {
                if (!prop.Readable)
                    continue;
                if (prop.HasAttribute<NonMetaDataAttribute>())
                    continue;

                var metadataAttr = prop.GetAttribute<MetaDataAttribute>();
                if (metadataAttr == null)
                {
                    // if metadataAttr is specified, all we require is that we can read and write it. 
                    // Otherwise normal rules applies:

                    if (prop.Writable == false)
                        continue; // Don't add Properties with XmlIgnore attribute
                    
                    if (prop.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
                        continue;

                    if (!prop.IsBrowsable())
                        continue;
                }

                var display = prop.GetDisplayAttribute();
                var metadata = prop.GetAttribute<MetaDataAttribute>();
                if (metadata != null && string.IsNullOrWhiteSpace(metadata.MacroName))
                    metadata = new MetaDataAttribute(metadata.PromptUser, display.Name);

                var name = display.Name.Trim();
                string group = "";

                if (display.Group.Length == 1) group = display.Group[0].Trim();

                lst.Add((prop, group, name, metadata));
            }

            result = lst.ToArray();
            propertiesLookup[type] = result;
            return result;
        }
        
        private static void GetPropertiesFromObject(object obj, ICollection<ResultParameter> output, string namePrefix = "", bool metadataOnly = false)
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            foreach (var (prop, group, name, metadata) in GetParametersMap(type))
            {
                if (metadataOnly && metadata == null) continue;
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
            var p = GetParametersMap(type);
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
        internal static ResultParameters GetParams(ITestStep step, params string[] extra)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            var parameters = new List<ResultParameter>(5);
            parameters.AddRange(extra.Select(x => new ResultParameter(x, null)));
            GetPropertiesFromObject(step, parameters);
            
            if (parameters.Count == 0)
                return new ResultParameters();
            return new ResultParameters(parameters);
        }
        
        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every setting of the inputted 
        /// TestStep.
        /// </summary>
        public static ResultParameters GetParams(ITestStep step) => GetParams(step, Array.Empty<string>());

        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters()
        {
            indexByName = new Dictionary<string, int>();
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

        /// <summary>
        /// Adds a new element to the parameters. (synchronized).
        /// </summary>
        /// <param name="parameter"></param>
        public void Add(ResultParameter parameter)
        {
            AddRange(new[] {parameter});
        }

        int count = 0;
        
        void addRangeUnsafe(IEnumerable<ResultParameter> parameters, bool initCollection = false)
        {
            
            if (initCollection)
            {
                var capacity = parameters.Count();
                    
                data = new SafeArray<resultParameter>(new resultParameter[capacity]);
                indexByName = new Dictionary<string, int>(capacity);
                foreach (var par in parameters)
                {
                    if (par?.Name == null) continue; 
                    if (indexByName.TryGetValue(par.Name, out var idx))
                    {
                        data[idx] = par;
                    }
                    else
                    {
                        indexByName[par.Name] = count;
                        data[count] = par;
                        count += 1;
                    }
                }
                return;
            }
            
            Dictionary<string, int> newIndexes = null;
            List<ResultParameter> newParameters = null;
            
            foreach (var par in parameters)
            {
                if (!indexByName.TryGetValue(par.Name, out var idx))
                    idx = -1;

                if (idx >= 0)
                {
                    data[idx] = par;
                    continue;
                }

                if (newIndexes == null)
                {
                    newIndexes = new Dictionary<string, int>();
                    newParameters = new List<ResultParameter>();
                }

                if (newIndexes.TryGetValue(par.Name, out idx))
                {
                    newParameters[idx - count] = par;    
                }
                else
                {
                    int nidx = count + newParameters.Count;
                    newParameters.Add(par);
                    newIndexes[par.Name] = nidx;
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

        Dictionary<string, int> indexByName;

        object addLock = new object();
        
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
                indexByName = new Dictionary<string, int>(indexByName)
            };
            
            return r;
        }
        

        internal IConvertible GetIndexed(string verdictName, ref int verdictIndex)
        {
            if(verdictIndex == -1)
                verdictIndex = FindIndex(verdictName);
            return data[verdictIndex].Value;
        }

        internal void SetIndexed(string name, ref int index, IConvertible value)
        {
            if(index == -1)
                index = FindIndex(name);
            if (index == -1)
            {
                Add(new ResultParameter(name, value));
            }
            else
            {
                data[index] = new resultParameter{Name = name, Value = value};
            }
        }

        
        internal void IncludeMetadataFromObject(TestPlan obj)
        {
            var metadata = GetMetadataFromObject(obj);
            AddRange(metadata);
        }

        /// <summary>
        /// Adds a new result parameter.
        /// </summary>
        /// <param name="group"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="metaDataAttribute"></param>
        public void Add(string group, string name, IConvertible value, MetaDataAttribute metaDataAttribute)
        {
            Add(new ResultParameter(group, name, value, metaDataAttribute));
        }
    }
    class NonMetaDataAttribute : Attribute
    {
        
    }
}

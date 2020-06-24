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
using System.Collections.Immutable;

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
        public bool IsMetaData { get; internal set; }

        /// <summary> null or the macro name representation of the ResultParameter. This will make it possible to insert the parameter value into a string. <see cref="MacroString"/></summary>
        public readonly string MacroName;

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

    /// <summary>
    /// A collection of parameters related to the results.
    /// </summary>
    public class ResultParameters : IReadOnlyList<ResultParameter>
    {
        object addlock = new object();
        IReadOnlyList<ResultParameter> data;

        /// <summary>
        /// Gets the parameter with the given index.
        /// </summary>
        /// <param name="index"></param>
        public ResultParameter this[int index] => data[index];

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
                var rp = Find(key);
                if (rp != null)
                    Add(new ResultParameter(rp.Group, key, value, null, rp.ParentLevel));
                else
                    Add(new ResultParameter("", key, value, null)); //assume parent level 0.
            }
        }

        static void getMetadataFromObject(object res, string nestedName, ICollection<ResultParameter> output)
        {
            GetPropertiesFromObject(res, output, nestedName, typeof(MetaDataAttribute));
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
            if (value is IConvertible && !(value is Enum))
                val = value as IConvertible;
            else if((val = StringConvertProvider.GetString(value)) == null)
                val = value.ToString();

            output.Add( new ResultParameter(group, parentName, val, metadata));
        }

        static ConcurrentDictionary<ITypeData, List<IMemberData>> propertiesLookup = new ConcurrentDictionary<ITypeData, List<IMemberData>>();

        private static void GetPropertiesFromObject(object obj, ICollection<ResultParameter> output, string namePrefix = "", params Type[] attributeFilter)
        {
            if (obj == null)
                return;
            var type = TypeData.GetTypeData(obj);
            if (!propertiesLookup.ContainsKey(type))
            {
                List<IMemberData> lst = new List<IMemberData>();
                foreach (var prop in type.GetMembers())
                {
                    if (!prop.Readable)
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

                    if (attributeFilter.Length > 0)
                    {
                        // Skip properties _prop does not have any of the attributes specified in "attributeFilter"
                        var attributes = prop.Attributes;
                        if (attributes.Count(att => attributeFilter.Contains(att.GetType())) == 0)
                            continue;
                    }
                    
                    lst.Add(prop);
                }
                propertiesLookup[type] = lst;
            }
            foreach (var prop in propertiesLookup[type])
            {
                var display = prop.GetDisplayAttribute();
                var metadata = prop.GetAttribute<MetaDataAttribute>();
                if (metadata != null && string.IsNullOrWhiteSpace(metadata.MacroName))
                    metadata = new MetaDataAttribute(metadata.PromptUser, display.Name);
                object value = prop.GetValue(obj);
                if (value == null)
                    continue;

                var name = display.Name.Trim();
                string group = "";

                if (display.Group.Length == 1) group = display.Group[0].Trim();

                GetParams(group, namePrefix + name, value, metadata, output);
            }
        }
        
        /// <summary>
        /// Returns a <see cref="ResultParameters"/> list with one entry for every setting of the inputted 
        /// TestStep.
        /// </summary>
        public static ResultParameters GetParams(ITestStep step)
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
            data = Array.Empty<ResultParameter>();
        }

        /// <summary>
        /// Initializes a new instance of the ResultParameters class.
        /// </summary>
        public ResultParameters(IEnumerable<ResultParameter> items) : this()
        {
            addRangeUnsafe(items);
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
        public int Count => data.Count;

        /// <summary>
        /// Adds a new element to the parameters. (synchronized).
        /// </summary>
        /// <param name="parameter"></param>
        public void Add(ResultParameter parameter)
        {
            AddRange(new[] { parameter });
        }

        void addRangeUnsafe(IEnumerable<ResultParameter> parameters)
        {
            var tmp = data.ToList();
            Dictionary<string, int> newIndexes = null;

            foreach (var par in parameters)
            {
                var idx = tmp.FindIndex(rp =>
                    (rp.Name == par.Name) &&
                    (rp.Group == par.Group) &&
                    (rp.ParentLevel == par.ParentLevel));


                if (idx >= 0)
                    tmp[idx] = par;
                else
                {
                    int nidx = tmp.Count;
                    tmp.Add(par);
                    if (newIndexes == null)
                        newIndexes = new Dictionary<string, int>(indexByName);
                    newIndexes[par.Name] = nidx;
                }
            }

            data = tmp;
            if(newIndexes != null)
                indexByName = newIndexes;
        }

        Dictionary<string, int> indexByName = new Dictionary<string, int>();

        /// <summary>
        /// Adds a range of result parameters (synchronized).
        /// </summary>
        /// <param name="parameters"></param>
        public void AddRange(IEnumerable<ResultParameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            lock (addlock)
            {
               addRangeUnsafe(parameters);
            }
        }

        IEnumerator<ResultParameter> IEnumerable<ResultParameter>.GetEnumerator()
        {
            return data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }

        /// <summary> Copies all the data inside a ResultParameters instance. </summary>
        /// <returns></returns>
        internal ResultParameters Clone()
        {
            return new ResultParameters(this);
        }
    }
}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Reflection;
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
        public bool IsEnabled { get { return isEnabled; } set { isEnabled = value; } }
        
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

        IConvertible IParameter.Value
        {
            get
            {
                return Value;
            }
        }

        string IAttributedObject.Name
        {
            get
            {
                return Name;
            }
        }

        string IParameter.Group
        {
            get
            {
                return Group;
            }
        }

        string IAttributedObject.ObjectType
        {
            get
            {
                return "Parameter";
            }
        }

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

        static ConcurrentDictionary<Type, List<MemberData>> propertieslookup = new ConcurrentDictionary<Type, List<MemberData>>();

        private static void GetPropertiesFromObject(object obj, ICollection<ResultParameter> output, string namePrefix = "", params Type[] attributeFilter)
        {
            if (obj == null)
                return;
            if (!propertieslookup.ContainsKey(obj.GetType()))
            {
                List<MemberData> lst = new List<MemberData>();
                foreach (var _prop in obj.GetType().GetMemberData())
                {
                    var prop = _prop.Property;
                    if (!prop.CanRead)
                        continue;

                    var metadataAttr = _prop.GetAttribute<MetaDataAttribute>();
                    if (metadataAttr == null)
                    {
                        // if metadataAttr is specified, all we require is that we can read and write it. 
                        // Otherwise normal rules applies:
                        
                        if ((prop.CanWrite == false || prop.GetSetMethod() == null))
                            continue; // 
                                      // Don't add Properties with XmlIgnore attribute
                        if (_prop.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
                            continue;

                        if (!prop.IsBrowsable())
                            continue;
                    }

                    if (attributeFilter.Length > 0)
                    {
                        // Skip properties that does not have any of the attributes specified in "attributeFilter"
                        object[] attributes = prop.GetCustomAttributes(false);
                        if (attributes.Count(att => attributeFilter.Contains(att.GetType())) == 0)
                            continue;
                    }
                    
                    // Don't add Lists. They do not become useful strings.
                    if (prop.PropertyType.DescendsTo(typeof(IEnumerable)) && prop.PropertyType != typeof(string))
                        continue;
                    
                    lst.Add(_prop);
                }
                propertieslookup[obj.GetType()] = lst;
            }
            foreach (var prop in propertieslookup[obj.GetType()])
            {
                var metadata = prop.GetAttribute<MetaDataAttribute>();
                if (metadata != null && string.IsNullOrWhiteSpace(metadata.MacroName))
                    metadata = new MetaDataAttribute(metadata.PromptUser, prop.Display.Name);
                object value = prop.Property.GetValue(obj, null);
                if (value == null)
                    continue;

                var name = prop.Display.Name.Trim();
                string group = "";

                if (prop.Display.Group.Length == 1) group = prop.Display.Group[0].Trim();

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
                throw new ArgumentNullException("step");
            var parameters = new List<ResultParameter>();
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
            AddRange(items);
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

        ImmutableDictionary<string, int> indexByName = ImmutableDictionary<string, int>.Empty;

        /// <summary>
        /// Adds a range of result parameters (synchronized).
        /// </summary>
        /// <param name="parameters"></param>
        public void AddRange(IEnumerable<ResultParameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            lock (addlock)
            {
                var tmp = data.ToList();
                var newIndexes = indexByName.ToBuilder();

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
                        if (newIndexes.ContainsKey(par.Name) == false)
                        {
                            newIndexes.Add(par.Name, nidx);
                        }
                    }
                }

                data = tmp;
                indexByName = newIndexes.ToImmutable();
            }
        }

        IEnumerator<ResultParameter> IEnumerable<ResultParameter>.GetEnumerator()
        {
            return ((IEnumerable<ResultParameter>)data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<ResultParameter>)data).GetEnumerator();
        }

        /// <summary> Copies all the data inside a ResultParameters instance. </summary>
        /// <returns></returns>
        internal ResultParameters Clone()
        {
            return new ResultParameters(this);
        }
    }
}

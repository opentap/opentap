//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
namespace OpenTap
{

    /// <summary>
    /// This class represents a set of external test plan parameters that can be defined when a test plan is loaded.
    /// </summary>
    public class ExternalParameter
    {
        /// <summary>
        /// The name of this entry.
        /// </summary>
        public string Name { get; private set; }

        Dictionary<ITestStep, List<IMemberData>> properties;
        TestPlan plan;
        /// <summary> Maps test step to member infos. </summary>
        public IEnumerable<KeyValuePair<ITestStep, IEnumerable<IMemberData>>> Properties
            => properties.Select(x => new KeyValuePair<ITestStep, IEnumerable<IMemberData>>(x.Key, x.Value));


        /// <summary>
        /// Gets the list of PropertyInfos associated with this mask entry.
        /// </summary>
        public IEnumerable<IMemberData> PropertyInfos
        {
            get { return properties.SelectMany(x => x.Value).Distinct(); }
        }

        /// <summary>
        /// Gets or sets the value of the combined properties. The setter requires the types to be the same or IConvertibles.
        /// </summary>
        public object Value
        {
            get
            {
                if (properties.Count == 0) return 0;
                var item = properties.First();
                return item.Value.First().GetValue(item.Key);
            }
            set
            {
                bool strConvertSuccess = false;
                string str = null;
                strConvertSuccess = StringConvertProvider.TryGetString(value, out str);

                TapSerializer serializer = null;
                string serialized = null;
                if (!strConvertSuccess && value != null)
                {
                    serializer = new TapSerializer();
                    try
                    {
                        
                        serialized = serializer.SerializeToString(value);
                    }
                    catch
                    {

                    }
                }

                foreach (ITestStep step in properties.Keys)
                {

                    foreach (IMemberData prop in properties[step])
                    {
                        try
                        {
                            object setVal = value;
                            if (strConvertSuccess)
                            {
                                if (StringConvertProvider.TryFromString(str, prop.TypeDescriptor, step, out setVal) == false)
                                    setVal = value;
                            }
                            else if(serialized != null)
                            {
                                try
                                {
                                    setVal = serializer.DeserializeFromString(serialized);
                                }
                                catch
                                {

                                }
                            }
                            prop.SetValue(step, setVal); // This will throw an exception if it is not assignable.

                        }
                        catch
                        {
                            object _value = value;
                            if (_value != null)
                                prop.SetValue(step, _value); // This will throw an exception if it is not assignable.
                        }
                    }
                }
            }
        }

        internal void Clean()
        {
            var steps = Utils.FlattenHeirarchy(plan.ChildTestSteps, step => step.ChildTestSteps).ToHashSet();

            properties = properties.Where(props => steps.Contains(props.Key)).ToDictionary(k => k.Key, v => v.Value);
        }

        /// <summary>
        /// Gets the property that is bound by the step with ID stepGuid.
        /// </summary>
        /// <param name="stepGuid"></param>
        /// <returns></returns>
        public List<IMemberData> GetProperties(ITestStep stepGuid)
        {
            if (stepGuid == null)
                throw new ArgumentNullException("stepGuid");
            if (!properties.ContainsKey(stepGuid))
                return null;
            return properties[stepGuid];
        }

        /// <summary>Constructor for the ExternalParameter.</summary>
        /// <param name="Plan"></param>
        /// <param name="Name"></param>
        public ExternalParameter(TestPlan Plan, string Name)
        {
            this.plan = Plan;
            this.Name = Name;
            properties = new Dictionary<ITestStep, List<IMemberData>>();
        }

        /// <summary>
        /// Adds a property to the external parameters.
        /// </summary>
        /// <param name="stepId"></param>
        /// <param name="property"></param>
        public void Add(ITestStep stepId, IMemberData property)
        {
            if (stepId == null)
                throw new ArgumentNullException("stepId");
            if (property == null)
                throw new ArgumentNullException("property");
            foreach (var prop in properties.SelectMany(p => p.Value))
            {
                // enum, numbers, others
                ITypeData t1 = prop.TypeDescriptor;
                ITypeData t2 = property.TypeDescriptor;
                if (object.Equals(t1, t2) == false)
                    throw new Exception("External properties with same name has to be of same type.");
            }
            if (properties.ContainsKey(stepId))
                properties[stepId].Add(property);
            else
                properties[stepId] = new List<IMemberData> { property };
        }

        /// <summary>
        /// Removes a step from the external parameters.
        /// </summary>
        /// <param name="stepId"></param>
        public void Remove(ITestStep stepId)
        {
            if (stepId == null)
                throw new ArgumentNullException("stepId");
            properties.Remove(stepId);
        }
    }

    /// <summary> External test plan parameters. </summary>
    public class ExternalParameters
    {
        readonly List<ExternalParameter> entries = new List<ExternalParameter>();

        /// <summary>
        /// Gets the list of external test plan parameters.
        /// </summary>
        public IReadOnlyList<ExternalParameter> Entries { get { return entries; } }

        TestPlan plan;

        /// <summary>Constructor for the ExternalParameters.</summary>
        /// <param name="plan"></param>
        public ExternalParameters(TestPlan plan)
        {
            this.plan = plan;
        }
        

        /// <summary> Adds a step property to the external test plan parameters.</summary>
        /// <param name="step"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="Name"></param>
        public ExternalParameter Add(ITestStep step, IMemberData propertyInfo, string Name = null)
        {
            if (step == null)
                throw new ArgumentNullException("step");
            if (propertyInfo == null) // As it otherwise won't raise exception right away.
                throw new ArgumentNullException("propertyInfo");
            var et = Find(step, propertyInfo);
            //if (et != null)
            //{
            //    throw new Exception(string.Format("Property {0} could not be found on step {1}", propertyInfo, step));
            //}
            if (Name == null)
            {
                Name = propertyInfo.GetDisplayAttribute().Name.Trim();
                int idx = 0;
                var origName = Name;
                while(this.Get(Name) != null)
                   Name = string.Format("{0} {1}", origName , ++idx);
            }

            var entry = entries.FirstOrDefault(e => e.Name == Name);
            if (entry == null)
            {
                entry = new ExternalParameter(plan, Name);
                entries.Add(entry);
            }

            var props = entry.GetProperties(step);
            if ((props == null) || !props.Contains(propertyInfo))
            {
                entry.Add(step, propertyInfo);
            }
            return entry;
        }

        /// <summary> Removes a step property from the external parameters. </summary>
        /// <param name="step"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="Name"></param>
        public void Remove(ITestStep step, IMemberData propertyInfo, string Name = null)
        {
            if (step == null)
                throw new ArgumentNullException("step");
            ExternalParameter entry = null;
            if (Name == null)
                entry = entries.Where(e => e.GetProperties(step) != null)
                    .FirstOrDefault(e => e.GetProperties(step).Contains(propertyInfo));
            else
                entry = entries.FirstOrDefault(e => e.Name == Name);
            if (entry == null)
                return;
            var props = entry.GetProperties(step);
            if (props == null)
                return;
            props.Remove(propertyInfo);
            if (props.Count == 0)
                entry.Remove(step);
            if (entry.PropertyInfos.Count() == 0)
                entries.Remove(entry);
        }


        /// <summary>
        /// Ensures that each entry test step is also present the test plan.
        /// </summary>
        public void Clean()
        {
            foreach (var entry in Entries.ToList())
            {
                entry.Clean();
                if (entry.PropertyInfos.Any() == false)
                    entries.Remove(entry);
            }
        }

        /// <summary> Gets an entry by name. </summary>
        /// <param name="ExternalParameterName"></param>
        /// <returns></returns>
        public ExternalParameter Get(string ExternalParameterName)
        {
            return entries.FirstOrDefault(e => e.Name == ExternalParameterName);
        }

        /// <summary>
        /// Finds the external parameter that is defined by 'step' and 'property'.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public ExternalParameter Find(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException("step");
            foreach (var entry in entries)
            {
                var props = entry.GetProperties(step);
                if ((props != null) && props.Contains(property))
                    return entry;
            }
            return null;
        }

    }
}

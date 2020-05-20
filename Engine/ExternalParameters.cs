//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
namespace OpenTap
{

    /// <summary> Represent an external test plan parameters that can be defined when a test plan is loaded. </summary>
    public class ExternalParameter
    {
        /// <summary> The name of this parameter. </summary>
        public string Name { get; }

        readonly TestPlan plan;
        /// <summary> Maps test step to member data. </summary>
        public IEnumerable<KeyValuePair<ITestStep, IEnumerable<IMemberData>>> Properties
            => member.ParameterizedMembers
                .Select(x => new KeyValuePair<ITestStep, IEnumerable<IMemberData>>((ITestStep)x.Source, new []{x.Member}));


        /// <summary> Gets the list of PropertyInfos associated with this mask entry. </summary>
        public IEnumerable<IMemberData> PropertyInfos => Properties
            .SelectMany(x => x.Value)
            .Distinct();

        /// <summary>
        /// Gets or sets the value of the combined properties. This requires the types to be the same or IConvertibles.
        /// </summary>
        public object Value
        {
            get => member.GetValue(plan);
            set => member.SetValue(plan, value);
        }

        internal void Clean(HashSet<ITestStep> steps)
        {
            var members = member.ParameterizedMembers
                .Where(x => steps.Contains(x.Source) == false)
                .ToArray();
            foreach (var item in members)
                item.Member.Unparameterize(member, item.Source);
        }

        /// <summary> Gets the property that is bound by the step with ID step. </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public List<IMemberData> GetProperties(ITestStep step)
        {
            return Properties.Where(x => x.Key == step).SelectMany(x => x.Value).ToList();
        }

        ParameterMemberData member;

        /// <summary> Constructor for the ExternalParameter. </summary>
        /// <param name="Plan"></param>
        /// <param name="Name"></param>
        public ExternalParameter(TestPlan Plan, string Name)
        {
            this.plan = Plan;
            this.Name = Name;
            member = TypeData.GetTypeData(plan).GetMember(Name) as ParameterMemberData;
        }

        internal ExternalParameter(TestPlan plan, ParameterMemberData parameter)
        {
            this.plan = plan;
            this.Name = parameter.Name;
            member = parameter;
        }

        /// <summary> Adds a property to the external parameters. </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        public void Add(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            plan.ExternalParameters.Add(step, property, Name);
        }

        /// <summary> Removes a step from the external parameters. </summary>
        /// <param name="step"></param>
        public void Remove(ITestStep step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            var members = member.ParameterizedMembers;
            foreach (var item in members.Where(x => step == x.Source))
                item.Member.Unparameterize(member, item.Source);
        }
    }

    /// <summary> External test plan parameters. </summary>
    public class ExternalParameters
    {

        /// <summary> Gets the list of external test plan parameters. </summary>
        public IReadOnlyList<ExternalParameter> Entries
        {
            get
            {
                var fwd = TypeData.GetTypeData(plan).GetMembers().OfType<ParameterMemberData>();
                return fwd.Select(x => new ExternalParameter(plan, x)).ToList();
            }
        }

        readonly TestPlan plan;

        /// <summary> Constructor for the ExternalParameters. </summary>
        /// <param name="plan"></param>
        public ExternalParameters(TestPlan plan)
        {
            this.plan = plan;
        }
        
        /// <summary> Adds a step property to the external test plan parameters. </summary>
        /// <param name="step"></param>
        /// <param name="setting"></param>
        /// <param name="Name"></param>
        public ExternalParameter Add(ITestStep step, IMemberData setting, string Name = null)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (setting == null) // As it otherwise won't raise exception right away.
                throw new ArgumentNullException(nameof(setting));
            var existing = Find(step, setting);
            if (existing != null)
                return existing;
            if (Name == null)
                Name = setting.GetDisplayAttribute().Name;
            
            setting.Parameterize(plan, step, Name);
            return Get(Name);
        }

        /// <summary> Removes a step property from the external parameters. </summary>
        /// <param name="step">The step owning the property. </param>
        /// <param name="propertyInfo"> The property to remove. </param>
        /// <param name="name">Un-used parameter. </param>
        public void Remove(ITestStep step, IMemberData propertyInfo, string name = null)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if(propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
            ParameterMemberData fwd = propertyInfo.GetParameter(plan, step);
            if (fwd == null) return;
            if(name != null && fwd.Name != name)
                throw new InvalidOperationException("Name does not match external parameter name.");
            propertyInfo.Unparameterize(fwd, step);
        }

        /// <summary> Ensures that each entry test step is also present the test plan. </summary>
        public void Clean()
        {
            var steps = Utils.FlattenHeirarchy(plan.ChildTestSteps, step => step.ChildTestSteps).ToHashSet();
            foreach (var entry in Entries.ToList())
                entry.Clean(steps);
        }

        /// <summary> Gets an entry by name. </summary>
        /// <param name="externalParameterName"></param>
        /// <returns></returns>
        public ExternalParameter Get(string externalParameterName)
        {
            if(TypeData.GetTypeData(plan).GetMember(externalParameterName) is ParameterMemberData member)
                return new ExternalParameter(plan, member);
            return null;
        }

        /// <summary>
        /// Finds the external parameter that is defined by 'step' and 'property'. If no external parameter is found null is returned.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public ExternalParameter Find(ITestStep step, IMemberData property)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if(property == null)
                throw new ArgumentNullException(nameof(property));
            var parameter = property.GetParameter(plan, step);
            if(parameter != null)
                return new ExternalParameter(plan, parameter);
            return null;
        }
    }
}

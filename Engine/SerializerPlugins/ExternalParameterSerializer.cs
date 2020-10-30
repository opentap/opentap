//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for parameters. </summary>
    [Display("Parameter Serializer", "Works with parameters in the test plan.")]
    public class ExternalParameterSerializer : TapSerializerPlugin
    {
        /// <summary>
        /// Structure for holding data about <see cref="TestPlan.ExternalParameters"/>
        /// </summary>
        public struct ExternalParamData
        {
            /// <summary>
            /// The object
            /// </summary>
            public ITestStep Object;
            /// <summary>
            /// The external param property.
            /// </summary>
            public IMemberData Property;
            /// <summary>
            ///  The name of the external test plan parameter.
            /// </summary>
            public string Name;
        }
        /// <summary> The order of this serializer. </summary>
        public override double Order => 50;
        
        List<XElement> currentNode = new List<XElement>();

        /// <summary>
        /// Stores the data if a test plan was not serialized but the external keyword was used. 
        /// </summary>
        public readonly List<ExternalParamData> UnusedExternalParamData = new List<ExternalParamData>();

        /// <summary>
        /// Pre-Loaded external parameter Name/Value sets.
        /// </summary>
        public readonly Dictionary<string, string> PreloadedValues = new Dictionary<string, string>();

        static readonly XName External = "external";
        static readonly XName Scope = "Scope";
        static readonly XName Parameter = "Parameter";

        bool loadScopeParameter(Guid scope, ITestStep step, IMemberData member, string parameter)
        {
            ITestStepParent parent;
            if (scope == Guid.Empty)
            {
                parent = step.GetParent<TestPlan>();
            }
            else
            {
                ITestStep subparent = step.Parent as ITestStep;
                while (subparent != null)
                {
                    if (subparent.Id == scope)
                        break;
                    subparent = subparent.Parent as ITestStep;
                }
                parent = subparent;
            }

            if (parent == null) return false;
            member.Parameterize(parent, step, parameter);
            return true;
        }

        void loadPreloadedvalues(TestPlan plan)
        {
            foreach (var value in PreloadedValues)
            {
                var ext = plan.ExternalParameters.Get(value.Key);
                if (ext == null) continue;
                try
                {
                    ext.Value = value.Value;
                }
                catch
                {

                }
            }

            // Update all external parameter values.
            // This is generally redundant, since they were all serialized with the same values.
            // but just to be sure.
            foreach (var extParam in plan.ExternalParameters.Entries)
                extParam.Value = extParam.Value;
        }
        
        XElement rootNode;
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize(XElement elem, ITypeData t, Action<object> setter)
        {
            if (rootNode == null && t.DescendsTo(typeof(TestPlan)))
            {
                rootNode = elem;
                TestPlan _plan = null;
               
                bool ok = Serializer.Deserialize(elem,  x=>
                {
                    _plan = (TestPlan)x;
                    setter(_plan); 
                }, t);
                
                // preloaded values should be handled as the absolute last thing
                // 2x Defer is used to ensure that there's a very little chance that it will be overwritten
                // but idealy there should be a way for ordering defers.
                Serializer.DeferLoad(() =>
                {
                    loadPreloadedvalues(_plan);
                    Serializer.DeferLoad(() => loadPreloadedvalues(_plan));
                });

                return ok;
            }
            if (elem.HasAttributes == false || currentNode.Contains(elem)) return false;

            var parameter = elem.Attribute(External)?.Value ?? elem.Attribute(Parameter)?.Value;
            if (string.IsNullOrWhiteSpace(parameter)) return false;
            var stepSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            var step = stepSerializer?.Object as ITestStep;
            if (step == null) return false;
            var member = stepSerializer.CurrentMember;

            Guid.TryParse(elem.Attribute(Scope)?.Value, out Guid scope);
            if (!loadScopeParameter(scope, step, member, parameter))
                Serializer.DeferLoad(() => loadScopeParameter(scope, step, member, parameter));
            if (scope != Guid.Empty) return false;
            var plan = Serializer.SerializerStack.OfType<TestPlanSerializer>().FirstOrDefault()?.Plan;
            if (plan == null)
            {
                currentNode.Add(elem);
                try
                {
                    UnusedExternalParamData.Add(new ExternalParamData
                    {
                        Object = (ITestStep) stepSerializer.Object, Property = stepSerializer.CurrentMember,
                        Name = parameter
                    });
                    return Serializer.Deserialize(elem, setter, t);
                }
                finally
                {
                    currentNode.Remove(elem);
                }
            }

            currentNode.Add(elem);
            try
            {
                
                bool ok = Serializer.Deserialize(elem, setter, t);
                var ext = plan.ExternalParameters.Get(parameter);

                Serializer.DeferLoad(() =>
                {
                    var ext2 = plan.ExternalParameters.Get(parameter);
                    if (ext2 == null) return;

                    if (PreloadedValues.ContainsKey(ext2.Name)) 
                        // If there is a  preloaded value, use that.
                        ext2.Value = PreloadedValues[ext2.Name];
                });
                if (ext != null && PreloadedValues.ContainsKey(ext.Name)) 
                    // If there is a  preloaded value, use that.
                    ext.Value = PreloadedValues[ext.Name];
                return ok;
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }
        
        /// <summary>
        /// This dictionary is used to cache which parent steps point to which child test steps via a parameter.
        /// Hence it maps (step,member) to (parent, member).
        /// </summary>
        Dictionary<(object step, IMemberData member), (object parent, ParameterMemberData parentMember)> parameterReverseLookup = 
        new Dictionary<(object step, IMemberData member), (object parent, ParameterMemberData parentMember)>();
        HashSet<ITestStepParent> reversedParents = new HashSet<ITestStepParent>();

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeData expectedType)
        {
            if (currentNode.Contains(elem)) return false;
            

            var objSerializer = Serializer.SerializerStack.OfType<IConstructingSerializer>().FirstOrDefault();
            if (objSerializer?.CurrentMember == null || false == objSerializer.Object is ITestStep)
                return false;

            
            ITestStep step = (ITestStep)objSerializer.Object;
            
            var member = objSerializer.CurrentMember;
            // here I need to check if any of its parent steps are forwarding 
            // its member data.

            {
                // update reverse lookup.
                ITestStepParent parameterParent = step.Parent;
                while (parameterParent != null)
                {
                    if (reversedParents.Add(parameterParent))
                    {
                        var parameters = TypeData.GetTypeData(parameterParent)
                            .GetMembers()
                            .OfType<ParameterMemberData>();

                        foreach (var parameter in parameters)
                        {
                            foreach (var set in parameter.ParameterizedMembers)
                                parameterReverseLookup.Add(set, (parameterParent, parameter));
                        }

                        parameterParent = parameterParent.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (parameterReverseLookup.TryGetValue(((object)step, member), out (object parameterParent, ParameterMemberData parameterMember) parentMember) == false) return false;

            elem.SetAttributeValue(Parameter, parentMember.parameterMember.Name);
            if (parentMember.parameterParent is ITestStep parentStep)
                elem.SetAttributeValue(Scope, parentStep.Id.ToString());
            // skip
            try
            {
                currentNode.Add(elem);
                return Serializer.Serialize(elem, obj, expectedType);
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }
    }
}

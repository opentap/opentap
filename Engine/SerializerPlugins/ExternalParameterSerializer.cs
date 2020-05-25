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
    /// <summary> Serializer implementation for External parameters. </summary>
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
        public override double Order { get { return 2; } }
        
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
                var extParam = plan.ExternalParameters.Get(parameter);

                if (ok)
                {
                    Serializer.DeferLoad(() =>
                    {
                        extParam = plan.ExternalParameters.Get(parameter);

                        if (PreloadedValues.ContainsKey(extParam.Name)) // If there is a  preloaded value, use that.
                            extParam.Value = PreloadedValues[extParam.Name];
                        else
                            extParam.Value = extParam.Value;
                    });
                    if (extParam != null && PreloadedValues.ContainsKey(extParam.Name)) // If there is a  preloaded value, use that.
                        extParam.Value = PreloadedValues[extParam.Name];
                }
                

                return ok;
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeData expectedType)
        {
            if (currentNode.Contains(elem)) return false;
            

            ObjectSerializer objSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            if (objSerializer == null || objSerializer.CurrentMember == null || false == objSerializer.Object is ITestStep)
                return false;

            
            ITestStep step = (ITestStep)objSerializer.Object;
            
            var member = objSerializer.CurrentMember;
            // here I need to check if any of its parent steps are forwarding 
            // its member data.

            ITestStepParent parameterParemt = step.Parent;
            IMemberData parameterMember = null;
            while (parameterParemt != null && parameterMember == null)
            {
                var members = TypeData.GetTypeData(parameterParemt).GetMembers().OfType<IParameterMemberData>();
                parameterMember = members.FirstOrDefault(x => x.ParameterizedMembers.Any(y => y.Source == step && y.Member == member));
                if (parameterMember == null)
                    parameterParemt = parameterParemt.Parent;
            }

            if (parameterMember == null) return false;

            elem.SetAttributeValue(Parameter, parameterMember.Name);
            if (parameterParemt is ITestStep parentStep)
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

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;

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
            public IMemberInfo Property;
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

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement elem, ITypeInfo t, Action<object> setter)
        {
            if (currentNode.Contains(elem)) return false;
            
            var attr = elem.Attribute("external");
            if (attr == null) return false;
            var stepSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            if (stepSerializer == null || false == stepSerializer.Object is ITestStep) return false;
            var planSerializer = Serializer.SerializerStack.OfType<TestPlanSerializer>().FirstOrDefault();
            if (planSerializer == null || planSerializer.Plan == null)
            {
                currentNode.Add(elem);
                try
                {
                    UnusedExternalParamData.Add(new ExternalParamData { Object = (ITestStep)stepSerializer.Object, Property = stepSerializer.CurrentMember, Name = attr.Value });
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
                var prop = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault().CurrentMember;
                var extParam = planSerializer.Plan.ExternalParameters.Add((ITestStep)stepSerializer.Object, prop, attr.Value);
                
                bool ok = Serializer.Deserialize(elem, setter, t);
                if (ok)
                {
                    Serializer.DeferLoad(() =>
                    {
                        extParam.Value = extParam.Value;
                    });
                }
                if (PreloadedValues.ContainsKey(extParam.Name)) // If there is a  preloaded value, use that.
                    extParam.Value = PreloadedValues[extParam.Name];
                return ok;
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeInfo expectedType)
        {
            if (currentNode.Contains(elem)) return false;
            

            ObjectSerializer objSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            if (objSerializer == null || objSerializer.CurrentMember == null || false == objSerializer.Object is ITestStep)
                return false;
            ITestStep step = (ITestStep)objSerializer.Object;
            TestPlan plan = null;
            {
                TestPlanSerializer planSerializer = Serializer.SerializerStack.OfType<TestPlanSerializer>().FirstOrDefault();
                if (planSerializer != null && planSerializer.Plan != null)
                    plan = planSerializer.Plan;
            }
            plan = plan ?? step.GetParent<TestPlan>();
            if (plan == null)
                return false;
            
            var external = plan.ExternalParameters.Find(objSerializer.Object as ITestStep, objSerializer.CurrentMember);
            if(external != null)
            {
                elem.SetAttributeValue("external", external.Name);
            }else
            {
                return false;
            }
            try
            {
                currentNode.Add(elem);
                return Serializer.Serialize(elem as XElement, obj, expectedType);
            }
            finally
            {
                currentNode.Remove(elem);
            }
        }
    }

}

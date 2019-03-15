//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for TestPlans. </summary>
    internal class TestPlanSerializer : ObjectSerializer
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 1; } }

        HashSet<XElement> currentNode = new HashSet<XElement>();
        internal TestPlan Plan { get; private set; }

        /// <summary>
        /// Deserializes a test plan from XML.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="_t"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        public override bool Deserialize(XElement element, ITypeData _t, Action<object> setter)
        {
            if (_t.IsA(typeof(TestPlan)) == false)
                return false;
            var prevPlan = Plan;
            Plan = new TestPlan { Path = Serializer.ReadPath };
            try
            {
                return TryDeserializeObject(element, _t, setter, Plan);
            }
            finally
            {
                Plan = prevPlan;
            }

        }

        /// <summary>
        /// Serializes an object to XML.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="obj"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public override bool Serialize(XElement element, object obj, ITypeData expectedType)
        {
            if (obj is TestPlan == false || currentNode.Contains(element))
                return false;
            var prevPlan = Plan;
            Plan = (TestPlan)obj;
            currentNode.Add(element);
            try
            {
                return Serializer.Serialize(element, obj);
            }
            finally
            {
                Plan = prevPlan;
                currentNode.Remove(element);
            }
        }
    }

}

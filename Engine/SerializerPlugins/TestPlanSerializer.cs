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
    public class TestPlanSerializer : ObjectSerializer
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 1; } }

        HashSet<XElement> currentNode = new HashSet<XElement>();
        internal TestPlan Plan { get; private set; }
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement node, Type t, Action<object> setter)
        {
            if (t != typeof(TestPlan))
                return false;
            var prevPlan = Plan;
            Plan = new TestPlan { Path = Serializer.ReadPath };
            try
            {
                return TryDeserializeObject(node, t, setter, Plan);
            }
            finally
            {
                Plan = prevPlan;
            }
        }

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement node, object obj, Type expectedType)
        {
            if (obj is TestPlan == false || currentNode.Contains(node))
                return false;
            var prevPlan = Plan;
            Plan = (TestPlan)obj;
            currentNode.Add(node);
            try
            {
                return Serializer.Serialize(node, obj);
            }
            finally
            {
                Plan = prevPlan;
                currentNode.Remove(node);
            }
        }
    }

}

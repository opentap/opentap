//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for TestStepList. </summary>
    public class TestStepListSerializer : TapSerializerPlugin
    {
        

        /// <summary> The order of this serializer. </summary>
        public override double Order
        {
            get { return 2; }
        }
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize(XElement elem, Type t, Action<object> setResult)
        {
            if (t != typeof(TestStepList)) return false;
            var steps = new TestStepList();
            foreach (var subnode in elem.Elements())
            {
                ITestStep result = null;
                try
                {
                    if (!Serializer.Deserialize(subnode, x => result = (ITestStep)x))
                    {
                        Log.Warning(subnode, "Unable to deserialize step.");
                        continue; // skip to next step.
                    }
                }
                catch(Exception ex)
                {
                    Log.Error(ex);
                    continue;
                }

                steps.CheckInserts = false;
                try
                {
                    if(result != null)
                        steps.Add(result);
                }
                finally
                {
                    steps.CheckInserts = true;
                }
            }
            setResult(steps);
            return true;
        }

        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object target, Type expectedType)
        {
            if(target is TestStepList)
            {
                TestStepList list = (TestStepList)target;
                for(int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    XElement newelem = new XElement("TestStep");
                    Serializer.Serialize(newelem, item);
                    elem.Add(newelem);
                }
                return true;
            }
            return false;
        }
    }

}

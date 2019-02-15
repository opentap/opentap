//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for IDynamicStep. </summary>
    /// It needs to act like an object serializer, which is why it inherits from it.
    /// It implementes TapSerializerPlugin, so it can explicitly override serializer behavior.
    public class DynamicStepSerializer : ObjectSerializer
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return new TestStepSerializer().Order + 1; } }

        HashSet<XElement> currentNode = new HashSet<XElement>();
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize(XElement elem, Type t, Action<object> setter)
        {
            if (t.HasInterface<IDynamicStep>() == false)
                return false;
            
            try
            {
                IDynamicStep step = (IDynamicStep)Activator.CreateInstance(t);
                Serializer.Register(step);
                var toIgnore = new HashSet<string>();

                TryDeserializeObject(elem, step.GetType(), setter, step, logWarnings: false);
                

                ITestStep genStep = step.GetStep();
                if(genStep != step)
                    Serializer.Register(genStep);
                bool res = true;
                if (elem.IsEmpty == false)
                    res = TryDeserializeObject(elem, genStep.GetType(), setter, genStep, logWarnings: false);
                else
                    setter(genStep);
                Serializer.GetSerializer<TestStepSerializer>().FixupStep(genStep, true);
                return res;
            }
            catch(Exception e)
            {
                Log.Error("Unable to deserialize step of type {0}. Error was: {1}", t, e.Message);
                
                Log.Debug(e);
                return true;
            }
        }
        
        const int testStepSizeThreshCompress = 4096;
        HashSet<XElement> serializing = new HashSet<XElement>();
        /// <summary> Serialization implementation. </summary>
        public override bool Serialize(XElement elem, object obj, Type expectedType)
        {
            if (obj is IDynamicStep == false) return false;
            if (serializing.Contains(elem)) return false;
            var step = (IDynamicStep)obj;
            
            try
            {
                serializing.Add(elem);
                return Serializer.Serialize(elem, obj, expectedType);
            }
            finally
            {
                serializing.Remove(elem);
                elem.SetAttributeValue("type", step.GetStepFactoryType().FullName);
            }
        }
    }

}

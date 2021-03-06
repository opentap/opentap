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
    internal class DynamicStepSerializer : ObjectSerializer
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return new TestStepSerializer().Order + 1; } }
        

        /// <summary>
        /// Serializes a dynamic step.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="obj"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public override bool Serialize(XElement elem, object obj, ITypeData expectedType)
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

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize(XElement elem, ITypeData t, Action<object> setter)
        {
            if (t.DescendsTo(typeof(IDynamicStep)) == false)
                return false;
            
            try
            {
                IDynamicStep step = (IDynamicStep)t.CreateInstance(Array.Empty<object>());
                Serializer.Register(step);
                
                TryDeserializeObject(elem, TypeData.GetTypeData(step), setter, step, logWarnings: false);
                
                ITestStep genStep = step.GetStep();
                bool res = true;
                if (elem.IsEmpty == false)
                    res = TryDeserializeObject(elem, TypeData.GetTypeData(genStep), setter, genStep, logWarnings: false);
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
        
        HashSet<XElement> serializing = new HashSet<XElement>();
    }

}

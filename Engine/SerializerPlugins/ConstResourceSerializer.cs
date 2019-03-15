//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Collections;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for IConstResourceProperty items. </summary>
    internal class ConstResourceSerializer : TapSerializerPlugin
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 3; } }

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement element, ITypeData t, Action<object> setter)
        {
            if (t.DescendsTo(TypeData.FromType(typeof(IConstResourceProperty))))
            {
                if (element.IsEmpty) return true; // Dont do anything with a <port/>
                var name = element.Attribute("Name");
                if (name == null)
                    return true;
                Serializer.Deserialize(element.Element("Device"), obj =>
                 {
                     var resource = obj as IResource;

                     if (obj is ComponentSettings)
                     { // for legacy support. type argument was a component settings, not a device. In this case used index to get the resource.
                         obj = ComponentSettings.GetCurrent(obj.GetType());
                         var lst = (IList)obj;
                         var dev = element.Element("Device");
                         int index = -1;
                         if(dev != null && int.TryParse(dev.Value, out index) && index >= 0 && lst.Count > index)
                         {
                             
                             resource = ((IList)obj)[index] as IResource;
                         }
                     }
                     if(resource == null)
                        return;
                     
                     
                     foreach(var resProp in resource.GetConstProperties())
                     {
                         if(TypeData.GetTypeData(resProp).DescendsTo(t) && resProp.Name == name.Value)
                         {
                             setter(resProp);
                             return;
                         }
                     }
                 }, typeof(IResource));
                return true;
            }
            return false;
        }

        /// <summary>
        /// For avoiding recursive Serialize calls.
        /// </summary>
        HashSet<object> checkRentry = new HashSet<object>();
        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeData expectedType)
        {
            if (obj is IConstResourceProperty == false) return false;
            if (checkRentry.Contains(obj)) return false;
            checkRentry.Add(obj);
            try
            {
                IConstResourceProperty port = (IConstResourceProperty)obj;

                elem.SetAttributeValue("Name", port.Name);
                IList settings = ComponentSettingsList.GetContainer(port.Device.GetType());
                if (port.Device != null && settings != null)
                {

                    XElement device = new XElement("Device");
                    device.SetAttributeValue("type", settings.GetType().Name);
                    if (Serializer.Serialize(device, port.Device, TypeData.FromType(port.GetType())))
                    {
                        elem.Add(device);
                    }
                }
                else
                    elem.Add(new XElement("Device"));
                return true;
            }
            finally
            {
                checkRentry.Remove(obj);
            }
        }
    }

}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Collections;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for Resources. </summary>
    public class ResourceSerializer : TapSerializerPlugin
    {
        /// <summary>
        /// True if there was an change caused by a mismatch of resource names in the tesplan and names in the bench settings
        /// </summary>
        internal bool TestPlanChanged { get; set; }

        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 1; } }

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement elem, ITypeInfo _t, Action<object> setter)
        {
            var t = (_t as CSharpTypeInfo)?.Type;
            if (t != null && t.DescendsTo(typeof(IResource)) && ComponentSettingsList.HasContainer(t))
            {
                var srcattr = elem.Attribute("Source");
                string src = null;
                if(srcattr != null)
                    src = srcattr.Value;

                if (elem.HasElements || src == "")
                {
                    return false;
                }
                else
                {
                    var content = elem.Value.Trim();

                    Serializer.DeferLoad(() =>
                    {
                        // we need to load this asynchronously to avoid recursively 
                        // serializing another ComponentSettings.
                        {
                            var obj = fetchObject(t, (o, i) => (o as IResource).Name.Trim() == content, src);
                            if (obj != null)
                            {
                                if (obj is IResource resource && resource.Name != content && !string.IsNullOrWhiteSpace(content))
                                {
                                    TestPlanChanged = true;
                                    var msg = $"Missing '{content}'. Using '{resource.Name}' instead.";
                                    if (elem.Parent.Element("Name") != null)
                                        msg = $"Missing '{content}' used by '{elem.Parent.Element("Name").Value}.{elem.Name.ToString()}. Using '{resource.Name}' instead.'";
                                    Log.Info(msg);
                                    Serializer.PushError(elem, msg);
                                }
                                setter(obj);
                                return;
                            }
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                TestPlanChanged = true;
                                var msg = $"Missing '{content}'.";
                                if (elem.Parent.Element("Name") != null)
                                    msg = $"Missing '{content}' used by '{elem.Parent.Element("Name").Value}.{elem.Name.ToString()}'";
                                Log.Info(msg);
                                Serializer.PushError(elem, msg);
                            }
                        }
                    });
                    return true;
                }
            }
            return false;
        }

        object fetchObject(Type t, Func<object, int, bool> test, string src)
        {
            IList objects = null;
            if(src != null)
            {
                var containerType = PluginManager.LocateType(src);
                if(containerType != null)
                {
                    objects = (IList)ComponentSettings.GetCurrent(containerType);
                }
            }

            if(objects == null)
                objects = ComponentSettingsList.GetContainer(t);

            var exact = objects.Cast<object>().Where(test).Where(o => o.GetType().DescendsTo(t)).FirstOrDefault();
            if (exact != null)
                return exact;

            return objects.Cast<object>().FirstOrDefault(x => x.GetType().DescendsTo(t));
        }

        HashSet<object> checkRentry = new HashSet<object>();
        
        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, ITypeInfo expectedType)
        {            
            if (obj == null) return false;

            Type type = obj.GetType();
            if(obj is IResource && ComponentSettingsList.HasContainer(type))
            {
                // If the next thing on the stack is a CollectionSerializer, it means that we are deserializing a ComponentSettingsList.
                // but if it is a ObjectSerializer it is probably a nested(stacked) resource property.
                var prevSerializer = Serializer.SerializerStack.FirstOrDefault(x => x is CollectionSerializer || x is ObjectSerializer);
                var collectionSerializer = prevSerializer as CollectionSerializer;
                if (collectionSerializer != null && collectionSerializer.ComponentSettingsSerializing.Contains(obj))
                {
                    if (checkRentry.Contains(obj)) return false;
                    checkRentry.Add(obj);
                    try
                    {
                        var result = Serializer.Serialize(elem, obj, expectedType);
                        if (result)
                            elem.SetAttributeValue("Source", ""); // set src to "" to show that it should not be deserialized by reference.
                        return result;
                    }
                    finally
                    {
                        checkRentry.Remove(obj);
                    }
                }
                
                var container = ComponentSettingsList.GetContainer(type);
                var index = container.IndexOf(obj);
                if (index != -1)
                {
                    elem.Value = (obj as IResource).Name ?? index.ToString();
                    elem.SetAttributeValue("Source", container.GetType().FullName);
                }
                // important to return true, otherwise it will serialize as a new value.
                return true; 
            }
            return false;
        }
    }

}

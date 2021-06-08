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
    internal class ResourceSerializer : TapSerializerPlugin
    {
        /// <summary>
        /// True if there was an change caused by a mismatch of resource names in the tesplan and names in the bench settings
        /// </summary>
        internal bool TestPlanChanged { get; set; }

        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 1; } }

        static XName sourceName = "Source"; 
        
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement elem, ITypeData _t, Action<object> setter)
        {
            // fastest and most obvious checks first.
            if (elem.HasElements) return false;
            var t = _t.AsTypeData()?.Type;
            if (t == null) return false;
            
            string src = null;
            {
                var srcAttribute = elem.Attribute(sourceName); 
                if (srcAttribute != null)
                    src = srcAttribute.Value;
            }

            // null and white-space has different meaning here.
            // "" means explicitly no source collection, while null means no known source.
            if (src == "")  
                return false;

            if (t.DescendsTo(typeof(IResource)) ||
                t.DescendsTo(typeof(Connection)) && ComponentSettingsList.HasContainer(t))
            {
                var content = elem.Value.Trim();

                Serializer.DeferLoad(() =>
                {
                    // we need to load this asynchronously to avoid recursively 
                    // serializing another ComponentSettings.
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        setter(null);
                        return;
                    }

                    string getName(object o)
                    {
                        if (o is Connection con)
                            return con.Name;
                        if (o is IResource res)
                            return res.Name;
                        return "";
                    }

                    var obj = fetchObject(t, (o, i) => getName(o).Trim() == content, src);
                    if (obj != null)
                    {
                        var name = getName(obj);
                        if (name != content && !string.IsNullOrWhiteSpace(content))
                        {
                            TestPlanChanged = true;
                            var msg = $"Missing '{content}'. Using '{name}' instead.";
                            if (elem.Parent.Element("Name") != null)
                                msg =
                                    $"Missing '{content}' used by '{elem.Parent.Element("Name").Value}.{elem.Name}. Using '{name}' instead.'";
                            Serializer.PushError(elem, msg);
                        }

                        setter(obj);
                    }
                    else
                    {
                        TestPlanChanged = true;
                        var msg = $"Missing '{content}'.";
                        if (elem.Parent.Element("Name") != null)
                            msg =
                                $"Missing '{content}' used by '{elem.Parent.Element("Name").Value}.{elem.Name}'";
                        Serializer.PushError(elem, msg);
                    }
                });
                return true;
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
        public override bool Serialize( XElement elem, object obj, ITypeData expectedType)
        {            
            if (obj == null) return false;
            
            Type type = obj.GetType();
            if((obj is IResource && ComponentSettingsList.HasContainer(type)) || obj is Connection)
            {
                // source was set by something else. Assume it can deserialize as well.
                if (false == string.IsNullOrEmpty(elem.Attribute("Source")?.Value as string)) return false;

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
                            elem.SetAttributeValue(sourceName, ""); // set src to "" to show that it should not be deserialized by reference.
                        return result;
                    }
                    finally
                    {
                        checkRentry.Remove(obj);
                    }
                }
                
                var container = ComponentSettingsList.GetContainer(type);
                var index = container.IndexOf(obj);
                if (index == -1)
                    return false;
                if (index != -1)
                {
                    if (obj is Connection con)
                        elem.Value = con.Name ?? index.ToString();
                    else if (obj is IResource res)
                        elem.Value = res.Name ?? index.ToString();
                    
                    elem.SetAttributeValue(sourceName, container.GetType().FullName);
                }
                // important to return true, otherwise it will serialize as a new value.
                return true; 
            }
            return false;
        }
    }

}

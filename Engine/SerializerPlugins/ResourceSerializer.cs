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
                
                // If there is no type matching the exact requested type.
                // try looking one up that matches the property type.
                // propertyType is an attempt to calculate the current target type.
                // it might be null if it cannot be determined.
                Type propertyType = null;
                foreach (var parentSerializer in Serializer.SerializerStack.Skip(1)) 
                {
                    if (parentSerializer is ObjectSerializer obj)
                    {
                        propertyType = obj.CurrentMember?.TypeDescriptor?.AsTypeData()?.Type;
                    }else if (parentSerializer is CollectionSerializer col)
                    {
                        propertyType = col.CurrentElementType;
                    }

                    if (propertyType != null)
                    {
                        break;
                    }
                }
                
                // if it is not possible to detect a base type, or if t is incompatible with it
                // assign 't' to the propertyType.
                if (propertyType == null && !t.DescendsTo(propertyType))
                {
                    propertyType = t;
                }
                
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
                    
                    // The following are the priorities for resource resolution.
                    // 1. matching name and exact type.
                    // 2. matching name and compatible type. (info message emitted)
                    // 3. matching type. (errors emitted)
                    // 4. defaulting to (errors emitted) compatible type.
                    
                    var obj = fetchObject(t, propertyType, o => getName(o).Trim() == content, src);
                    if (obj != null)
                    {
                        var name = getName(obj).Trim();
                        if (name != content && !string.IsNullOrWhiteSpace(content))
                        {
                            TestPlanChanged = true;
                            var msg = $"Missing '{content}'. Using '{name}' instead.";
                            if (elem.Parent.Element("Name") != null)
                                msg =
                                    $"Missing '{content}' used by '{elem.Parent.Element("Name").Value}.{elem.Name}. Using '{name}' instead.'";
                            Serializer.PushError(elem, msg);
                        }else if (obj.GetType().DescendsTo(t) == false)
                        {
                            Serializer.PushMessage(elem,
                                $"Selected resource '{getName(obj)}' of type {obj.GetType().GetDisplayAttribute().GetFullName()} instead of declared type {t.GetDisplayAttribute().GetFullName()}.");
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

        object fetchObject(Type exactType, Type compatibleType, Func<object, bool> test, string src)
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
                objects = ComponentSettingsList.GetContainer(exactType);

            object exactMatch = null;
            object compatibleMatch = null;
            object exactTypeMatch = null;
            
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                bool matchName = test(obj);
                bool matchType = obj.GetType().DescendsTo(exactType); 
                if (matchName)
                {
                    if (matchType)
                    {
                        exactMatch = obj;
                        return exactMatch;
                    }
                    if (exactType != compatibleType && obj.GetType().DescendsTo(compatibleType))
                    {
                        compatibleMatch = obj;
                    }
                }
                else
                {
                    if (matchType)
                    {
                        exactTypeMatch = obj;
                    }
                }
            }

            return exactMatch ?? compatibleMatch ?? exactTypeMatch;

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
                {
                    if (container is IComponentSettingsList lst)
                    {
                        if (lst.GetRemovedAliveResources().Contains(obj))
                        {  // skip serializing if the referenced instrument has been deleted.
                            elem.Remove();
                            return true;
                        }
                    }
                    // serialize it normally / in-place.
                    return false;
                }

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

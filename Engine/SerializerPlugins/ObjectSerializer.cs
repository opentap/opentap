//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Globalization;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Collections;

namespace OpenTap.Plugins
{
    /// <summary>
    /// Default object serializer.
    /// </summary>
    public class ObjectSerializer : TapSerializerPlugin
    {
        /// <summary>
        /// Gets the property currently being serialized.
        /// </summary>
        public PropertyInfo Property { get; private set; }

        /// <summary>
        /// Specifies order. Minimum order should  be -1 as this is the most basic serializer.  
        /// </summary>
        public override double Order
        {
            get { return -1; }
        }
        
        /// <summary> The currently serializing or deserializing object. </summary>
        public object Object { get; private set; }

        /// <summary>
        /// Tries to deserialize an object from an XElement.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <param name="newobj"></param>
        /// <param name="logWarnings">Whether warning messages should be emitted in case of missing properties.</param>
        /// <returns>True on success.</returns>
        public virtual bool TryDeserializeObject(XElement element, Type t, Action<object> setter, object newobj = null, bool logWarnings = true)
        {
            if (t.IsClass == false)
                return false;
            if (element.IsEmpty && !element.HasAttributes)
            {
                setter(null);
                return true;
            }
            if (newobj == null)
                try
                {
                    newobj = Activator.CreateInstance(t);
                }
                catch (TargetInvocationException ex)
                {
                    
                    if (ex.InnerException is System.ComponentModel.LicenseException)
                        throw new Exception(string.Format("Could not create an instance of '{0}': {1}", t.GetDisplayAttribute().Name, ex.InnerException.Message));
                    else
                    {
                        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    }
                }
            
            var prevobj = Object;
            Object = newobj;

            var properties = t.GetMemberData().Where(x => x.HasAttribute<XmlIgnoreAttribute>() == false).ToArray();
            try
            {
                
                foreach (var prop in properties)
                {
                    var p = prop.Property;
                    if (p == null) continue;
                    var attr = prop.GetAttribute<XmlAttributeAttribute>();
                    if (attr == null) continue;
                    var attr_value = element.Attribute(string.IsNullOrWhiteSpace(attr.AttributeName) ? prop.Info.Name : attr.AttributeName);
                    if (attr_value != null)
                    {
                        try
                        {
                            var value = readContentInternal(p.PropertyType, false, () => attr_value.Value, element);
                            p.SetValue(newobj, value, null);
                            
                        }
                        catch (Exception e)
                        {
                            if (logWarnings)
                            {
                                Log.Warning(element, "Attribute value '{0}' was not read correctly as a {1}", attr_value.Value, p);
                                Log.Debug(e);
                            }
                        }
                    }
                }
                var props = properties.ToLookup(x => x.GetCustomAttributes<XmlElementAttribute>().FirstOrDefault()?.ElementName ?? x.Info.Name);
                foreach (var element2 in element.Elements().ToArray())
                {
                    MemberData property = null;
                    var propertyMatches = props[element2.Name.LocalName];
                    
                    int hits = 0;
                    foreach(var p in propertyMatches)
                    {
                        var prop = p.Property;
                        if (prop.CanWrite && prop.GetSetMethod() != null || p.HasAttribute<XmlIgnoreAttribute>())
                        {
                            property = p;
                            hits++;
                        }
                    }
                    
                    if (0 == hits)
                    {
                        try
                        {
                            property = t.GetMemberData(element2.Name.LocalName);
                        }
                        catch { }
                        if (property == null || property.Property.CanWrite == false || property.Property.GetSetMethod() == null)
                        {
                            if (logWarnings)
                                Log.Warning(element2, "Unable to set property '{0}'. The property does not exist.", element2.Name.LocalName);
                            continue;
                        }
                        hits = 1;
                    }
                    if (hits > 1)
                        Log.Warning(element2, "Multiple properties named '{0}' are available to the serializer in '{1}' this might give issues in serialization.", element2.Name.LocalName, t.GetDisplayAttribute().Name);

                    var prev = Property;
                    Property = property.Property;
                    try
                    {
                        if (Property.HasAttribute<XmlIgnoreAttribute>()) // This property shouldn't have been in the file in the first place, but in case it is (because of a change or a bug), we shouldn't try to set it. (E.g. SweepLoopRange.SweepStep)
                            if (!Property.HasAttribute<BrowsableAttribute>()) // In the special case we assume that this is some compatibility property that still needs to be set if present in the XML. (E.g. TestPlanReference.DynamicDataContents)
                                continue;
                        
                        if (Property.PropertyType.HasInterface<IList>() && Property.PropertyType.IsGenericType && Property.HasAttribute<XmlElementAttribute>()) 
                        {
                            // Special case to mimic old .NET XmlSerializer behavior
                            var list = (IList)Property.GetValue(newobj);
                            Action<object> setValue = x => list.Add(x);
                            Serializer.Deserialize(element2, setValue, Property.PropertyType.GetGenericArguments().First());
                        }
                        else
                        {
                            Action<object> setValue = x => property.Property.SetValue(newobj, x);
                            Serializer.Deserialize(element2, setValue, Property.PropertyType);
                        }
                    }
                    catch (Exception e)
                    {
                        if (logWarnings)
                        {
                            Log.Warning(element2, "Unable to set property '{0}'.", Property.Name);
                            Log.Warning("Error was: \"{0}\".", e.Message);
                            Log.Debug(e);
                        }
                    }
                    finally
                    {
                        Property = prev;
                    }

                }
                setter(newobj);
            }
            finally
            {
                Object = prevobj;
                
                if (newobj is IDeserializedCallback)
                {
                    // Callback should be added in the end because an inner part of the object might also have defered it.
                    Serializer.DeferLoad(() =>
                    {
                        try
                        {
                            ((IDeserializedCallback)newobj).OnDeserialized();
                        }
                        catch (Exception e)
                        {
                            Log.Warning("Exception caught while handling OnSerialized");
                            Log.Debug(e);
                        }
                    });
                }
            }
            return true;
        }

        object readContentInternal(Type propType, bool ignoreComponentSettings, Func<string> getvalueString, XElement elem)
        {
            
            object value = null;

            if (propType.IsEnum || propType.IsPrimitive || propType == typeof(string) || propType.IsValueType || propType == typeof(Type))
            {
                string valueString = getvalueString();
                if (valueString != null)
                {
                    if (propType.IsEnum)
                    {
                        if (!string.IsNullOrEmpty(valueString))
                        {
                            // legacy support: A flagged enum did not have ','s, but just spaces between.
                            if (!valueString.Contains(','))
                            {
                                var splitted = valueString.Split(' ');
                                value = Enum.Parse(propType, string.Join(",", splitted));
                            }
                            else
                                value = Enum.Parse(propType, valueString);
                        }
                    }

                    else if (propType == typeof(String) || propType == typeof(char))
                    {
                        if (elem.HasElements)
                        {
                            // string contains Base64 if it has invalid XML chars.
                            var encode = elem.Element("Base64");
                            if (encode != null && encode.Value != null)
                            {
                                try
                                {
                                    value = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encode.Value));
                                }
                                catch
                                {
                                    value = encode.Value;
                                }
                            }
                        }
                        if (value == null)
                            value = valueString;
                        if(propType == typeof(char))
                            value = ((string)value)[0];
                    }
                    else if (propType == typeof(TimeSpan) || propType == typeof(TimeSpan?))
                    {
                        value = TimeSpan.Parse(valueString, CultureInfo.InvariantCulture);
                    }
                    else if (propType == typeof(Guid) || propType == typeof(Guid?))
                    {
                        value = Guid.Parse(valueString);
                    }
                    else if (propType == typeof(Type))
                    {
                        value = PluginManager.LocateType(valueString.Split(',').First());
                    }else if (tryConvertNumber(propType, valueString, out value))
                    {
                        // Conversions done in tryConvertNumber
                    }
                    else if (propType.HasInterface<IConvertible>())
                    {
                        value = Convert.ChangeType(valueString, propType, CultureInfo.InvariantCulture);
                    }
                }
            }
            return value;
        }
        
        static bool tryConvertNumber(Type type, string valueString, out object value)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                {
                    byte oval;
                    bool ok = byte.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.SByte:
                {
                    SByte oval;
                    bool ok = SByte.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.UInt16:
                {
                    UInt16 oval;
                    bool ok = UInt16.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.UInt32:
                {
                    UInt32 oval;
                    bool ok = UInt32.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.UInt64:
                {
                    UInt64 oval;
                    bool ok = UInt64.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                    
                case TypeCode.Int16:
                {
                    Int16 oval;
                    bool ok = Int16.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.Int32:
                {
                    Int32 oval;
                    bool ok = Int32.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.Int64:
                {
                    Int64 oval;
                    bool ok = Int64.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.Decimal:
                {
                    value = BigFloat.Convert(valueString, CultureInfo.InvariantCulture).ConvertTo(typeof(decimal));
                    return true;
                }
                case TypeCode.Double:
                {
                    Double oval;
                    bool ok = Double.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                case TypeCode.Single:
                {
                    Single oval;
                    bool ok = Single.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out oval);
                    value = oval;
                    return ok;
                }
                default:
                    value = null;
                    return false;
            }
        }

        /// <summary>
        /// Implementation on deserializer for generic objects.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        public override bool Deserialize(XElement element, Type t, Action<object> setter)
        {
            object result = null;
            try
            {
                object obj = readContentInternal(t, false, () => element.Value, element);
                if (obj != null)
                {
                    setter(obj);
                    return true;
                }
            }
            catch
            {
                Log.Warning(element, "Object value was not read correctly.");
                return false;
            }
            try
            {
                if (TryDeserializeObject(element, t, x => result = x))
                {
                    setter(result);
                    return true;
                }
            }
            catch (Exception)
            {
                Log.Error("Unable to create instance of {0}.", t);
                throw;
            }
            return false;
        }
        
        // for detecting cycles in object serialization.
        HashSet<object> cycleDetetionSet = new HashSet<object>();

        bool AlwaysG17DoubleFormat = false;

        /// <summary>
        /// Implementation on serializer for generic objects.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public override bool Serialize(XElement elem, object obj, Type type)
        {
            if (obj == null)
                return true;
            object prevObj = Object;
            // If cycleDetectorLut already contains an element, we've been here before.
            // Note the cycleDetectorLut adds and removes the obj later if it is not already in it.
            if (cycleDetetionSet.Contains(obj))
                throw new Exception("Cycle detected");
            object theobj = obj;
            try
            {
                cycleDetetionSet.Add(obj);
                Object = obj;
                switch (obj)
                {
                    case double d:
                        if (AlwaysG17DoubleFormat)
                        {
                            // the fastest and most accurate string representation of a double.
                            elem.Value = ((double)obj).ToString("G17", CultureInfo.InvariantCulture);
                            return true;
                        }
                        else
                        {
                            // It was decided to use R instead of G17 for readability, although G17 is slightly faster.
                            // however, there is a bug in "R" formatting on some .NET versions, that means that 
                            // roundtrip actually does not work.
                            // See section "Note to Callers:" at https://msdn.microsoft.com/en-us/library/kfsatb94(v=vs.110).aspx
                            // so here we format and then parse back to see if it can actually roundtrip.
                            // if not, we format with G17.
                            var d_str = ((double)obj).ToString("R", CultureInfo.InvariantCulture);
                            var d_re = double.Parse(d_str, CultureInfo.InvariantCulture);
                            if (d_re != d)
                                d_str = ((double)obj).ToString("G17", CultureInfo.InvariantCulture);
                            elem.Value = d_str;

                            return true;
                        }
                    case float f:
                        elem.Value = ((float)obj).ToString("R", CultureInfo.InvariantCulture);
                        return true;
                    case decimal d:
                        elem.Value = BigFloat.Convert(d).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case char c:
                        // Convert to a string and then handle it later.
                        obj = Convert.ToString(c, CultureInfo.InvariantCulture);
                        break;
                    default:
                        break;
                }

                if (obj is string str) // handled separately due to "case char c" from above.
                {
                    try
                    {
                        // This will throw an XmlException if the string contains invalid XML chars.
                        // If thats the case we base64 encode it.
                        System.Xml.XmlConvert.VerifyXmlChars(str);
                        elem.Value = str;
                    }
                    catch (System.Xml.XmlException)
                    {
                        var subelem = new XElement("Base64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(str)));
                        elem.Add(subelem);
                    }
                    return true;
                }
                if(type == typeof(bool))
                {
                    elem.Value = (bool)obj ? "true": "false"; // must be lower case for old XmlSerializer to work
                    return true;
                }

                if (type.IsEnum || type.IsPrimitive || type.IsValueType)
                {   
                    elem.Value = Convert.ToString(obj, CultureInfo.InvariantCulture);
                    return true;
                }
                
                var properties = type.GetMemberData().Where(x => x.HasAttribute<XmlIgnoreAttribute>() == false).ToArray();
                foreach (MemberData prop in properties)
                {
                    if (prop.Property is PropertyInfo info)
                    {
                        var attr = prop.GetAttribute<XmlAttributeAttribute>();
                        if (attr != null)
                        {
                            var defaultAttr = prop.GetAttribute<DefaultValueAttribute>();
                            var val = info.GetValue(obj);
                            if (defaultAttr != null &&  object.Equals(defaultAttr.Value,val))
                                continue;
                            string valStr = Convert.ToString(val, CultureInfo.InvariantCulture);
                            if(info.PropertyType == typeof(bool))
                                valStr = (bool)val ? "true" : "false"; // must be lower case for old XmlSerializer to work
                            elem.SetAttributeValue(string.IsNullOrWhiteSpace(attr.AttributeName) ? info.Name : attr.AttributeName, valStr);
                        }
                    }
                }
                
                var xmlTextProp = properties.FirstOrDefault(p => p.HasAttribute<XmlTextAttribute>());
                if (xmlTextProp != null)
                { // XmlTextAttribute support
                    var textvalue = xmlTextProp.Property.GetValue(obj, null);
                    if (textvalue != null)
                    {
                        var oldProp = Property;
                        Property = xmlTextProp.Property;
                        Serializer.Serialize(elem, textvalue);
                        Property = oldProp;
                    }
                }
                else
                {
                    foreach (MemberData subProp in properties)
                    {
                        if (subProp.Property is PropertyInfo p)
                        {
                            if (p.CanRead && p.CanWrite && null != p.GetSetMethod() && null == subProp.GetAttribute<XmlAttributeAttribute>())
                            {
                                var oldProp = Property;
                                Property = p;
                                try
                                {
                                    object val = p.GetValue(obj, null);
                                    if (val != null)
                                    {
                                        var enu = val as IEnumerable;
                                        if (enu != null && enu.GetEnumerator().MoveNext() == false) // the value is an empty IEnumerable
                                        {
                                            var defaultAttr = p.GetAttribute<DefaultValueAttribute>();
                                            if (defaultAttr != null && defaultAttr.Value == null)
                                                continue;
                                        }
                                        var attr = p.GetAttribute<XmlElementAttribute>();
                                        if (p.PropertyType.HasInterface<IList>() && p.PropertyType.IsGenericType && attr != null)
                                        {
                                            // Special case to mimic old .NET XmlSerializer behavior
                                            foreach (var item in enu)
                                            {
                                                XElement elem2 = new XElement(attr.ElementName ?? p.Name);
                                                Serializer.Serialize(elem2, item, p.PropertyType.GetGenericArguments().First());
                                                elem.Add(elem2);
                                            }
                                        }
                                        else
                                        {
                                            XElement elem2 = new XElement(p.Name);
                                            Serializer.Serialize(elem2, val, p.PropertyType);
                                            elem.Add(elem2);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Warning("Unable to serialize property '{0}'.", p.Name);
                                    Log.Debug(e);
                                }
                                finally
                                {
                                    Property = oldProp;
                                }
                            }
                        }
                    }
                }

                if (elem.IsEmpty && obj != null)
                    elem.Value = "";
                return true;
            }
            finally
            {
                Object = prevObj;
                if (!cycleDetetionSet.Remove(theobj))
                    throw new InvalidOperationException("obj was modified.");
            }
        }
    }

}

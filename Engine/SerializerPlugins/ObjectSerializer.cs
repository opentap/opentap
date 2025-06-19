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
using System.Collections;
using System.Xml;

namespace OpenTap.Plugins
{

    /// <summary>
    /// Implemented by serializer plugins that creates and populates members of an object.
    /// </summary>
    public interface IConstructingSerializer
    {
        /// <summary> The object currently being serialized/deserialized. </summary>
        object Object { get; }
        /// <summary> Optionally set to indicate which member of Object is being serialized/deserialized. </summary>
        IMemberData CurrentMember { get; }
    }
    /// <summary>
    /// Default object serializer.
    /// </summary>
    internal class ObjectSerializer : TapSerializerPlugin, ITapSerializerPlugin, IConstructingSerializer
    {
        /// <summary>
        /// Gets the member currently being serialized.
        /// </summary>
        public IMemberData CurrentMember { get; private set; }
        public static XName IgnoreMemberXName = "ignore-member";

        /// <summary>
        /// Specifies order. Minimum order should  be -1 as this is the most basic serializer.  
        /// </summary>
        public override double Order
        {
            get { return -1; }
        }
        
        /// <summary> The currently serializing or deserializing object. </summary>
        public object Object { get; private set; }

        Dictionary<ITypeData, IMemberData[]> serializableMembers = new Dictionary<ITypeData, IMemberData[]>();
        
        /// <summary>
        /// Tries to deserialize an object from an XElement.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <param name="newobj"></param>
        /// <param name="logWarnings">Whether warning messages should be emitted in case of missing properties.</param>
        /// <returns>True on success.</returns>
        public virtual bool TryDeserializeObject(XElement element, ITypeData t, Action<object> setter, object newobj = null, bool logWarnings = true)
        {
            
            if (element.IsEmpty && !element.HasAttributes)
            {
                setter(null);
                return true;
            }
            if (newobj == null)
            {
                if (t.CanCreateInstance == false)
                {
                    // if the instance type cannot be constructed,
                    // use the instance already on the object.
                    var objectSerializer = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
                    var ownerMember = objectSerializer?.CurrentMember;
                    var ownerObj = objectSerializer?.Object;
                    if (ownerMember == null || ownerObj == null)
                    {
                        throw new Exception($"Cannot create instance of {t} and no default value exists.");
                    }
                    if (ownerMember.HasAttribute<IFactoryAttribute>())
                    {
                        // Use element factory if the member type is not the target type, but rather 'has a' target type.
                        // we dont need to detect if it 'has a' target type, lets just assume it.
                        if (ownerMember.TypeDescriptor.DescendsTo(t) && ownerMember.GetAttribute<FactoryAttribute>() is FactoryAttribute f)
                        {
                            newobj = FactoryAttribute.Create(ownerObj, f);
                        }
                        else if (ownerMember.GetAttribute<ElementFactoryAttribute>() is ElementFactoryAttribute f2)
                        {
                            newobj = FactoryAttribute.Create(ownerObj, f2);
                        }
                    }

                    if (newobj == null)
                    {
                        newobj = ownerMember.GetValue(ownerObj);
                        if (newobj == null)
                            throw new Exception($"Unable to get default value of {ownerMember}");
                    }
                }
                else
                {
                    newobj = t.CreateInstance(Array.Empty<object>());
                }
            }
            
            var prevobj = Object;
            Object = newobj;
            
            if (newobj == null)
                throw new ArgumentNullException(nameof(newobj));
            t = TypeData.GetTypeData(newobj);
            var t2 = t;
            var properties = serializableMembers.GetOrCreateValue(t2, t3 => t3.GetMembers()
                .Where(x => x.HasAttribute<XmlIgnoreAttribute>() == false)
                .ToArray());

            try
            {
                
                foreach (var prop in properties)
                {
                    var attr = prop.GetAttribute<XmlAttributeAttribute>();
                    if (attr == null) continue;
                    var name = string.IsNullOrWhiteSpace(attr.AttributeName) ? prop.Name : attr.AttributeName;
                    var attr_value = element.Attribute(Serializer.PropertyXmlName(name));
                    

                    if (attr_value != null)
                    {
                        try
                        {
                            var typeData = prop.TypeDescriptor.AsTypeData();
                            readContentInternal(typeData.Type, false, () => attr_value.Value, element, out object value);
                            
                            prop.SetValue(newobj, value);
                            
                        }
                        catch (Exception e)
                        {
                            if (logWarnings)
                            {
                                Log.Warning(element, "Attribute value '{0}' was not read correctly as a {1}", attr_value.Value, prop);
                                Log.Debug(e);
                            }
                        }
                    }
                }

                if (properties.FirstOrDefault(x => x.HasAttribute<XmlTextAttribute>()) is IMemberData mem2)
                {
                    object value;
                    if (mem2.TypeDescriptor is TypeData td &&
                        readContentInternal(td.Load(), false, () => element.Value, element, out object _value))
                    { value = _value; } 
                    else
                        value = StringConvertProvider.FromString(element.Value, mem2.TypeDescriptor, null);
                    mem2.SetValue(newobj, value);
                }
                else
                {
                    var props = properties.ToLookup(x => x.GetAttributes<XmlElementAttribute>().FirstOrDefault()?.ElementName ?? x.Name);
                    var elements = element.Elements().ToArray();
                    bool[] visited = new bool[elements.Length];
                    
                    double order = 0;
                    int foundWithCurrentType = 0;
                    while (true)
                    {
                        double nextOrder = 1000;
                        // since the object might be dynamically adding properties as other props are added.
                        // we need to iterate a bit. Example: Test Plan Reference.

                        int found = visited.Count(x => x);
                        for (int i = 0; i < elements.Length; i++)
                        {
                            var element2 = elements[i];
                            if (visited[i]) continue;
                            if (element2.Attribute(IgnoreMemberXName) is XAttribute attr && attr.Value == "true")
                            {
                                visited[i] = true;
                                continue;
                            }
                            IMemberData property = null;
                            var name = XmlConvert.DecodeName(element2.Name.LocalName);
                            var propertyMatches = props[name];

                            int hits = 0;
                            foreach (var p in propertyMatches)
                            {
                                if (p.Writable || p.HasAttribute<XmlIgnoreAttribute>())
                                {
                                    property = p;
                                    hits++;
                                }
                            }

                            if (property == null)
                            {
                                continue; // later we might try this property again.
                            }
                            if (hits > 1)
                                Log.Warning(element2, "Multiple properties named '{0}' are available to the serializer in '{1}' this might give issues in serialization.", element2.Name.LocalName, t.GetAttribute<DisplayAttribute>().Name);

                            if (property.GetAttribute<DeserializeOrderAttribute>() is DeserializeOrderAttribute orderAttr)
                            {
                                if (order < orderAttr.Order)
                                {
                                    if (orderAttr.Order < nextOrder)
                                    {
                                        nextOrder = orderAttr.Order;
                                    }
                                    continue;
                                }
                            }
                            else
                            {
                                nextOrder = order;
                            }

                            visited[i] = true;
                            var prev = CurrentMember;
                            CurrentMember = property;
                            try
                            {
                                if (CurrentMember.HasAttribute<XmlIgnoreAttribute>()) // This property shouldn't have been in the file in the first place, but in case it is (because of a change or a bug), we shouldn't try to set it. (E.g. SweepLoopRange.SweepStep)
                                    if (!CurrentMember.HasAttribute<BrowsableAttribute>()) // In the special case we assume that this is some compatibility property that still needs to be set if present in the XML. (E.g. TestPlanReference.DynamicDataContents)
                                        continue;

                                if (property is MemberData mem && mem.Member is PropertyInfo Property && Property.PropertyType.HasInterface<IList>() && Property.PropertyType.IsGenericType && Property.HasAttribute<XmlElementAttribute>())
                                {
                                    // Special case to mimic old .NET XmlSerializer behavior
                                    var list = (IList)Property.GetValue(newobj);
                                    Action<object> setValue = x => list.Add(x);
                                    Serializer.Deserialize(element2, setValue, Property.PropertyType.GetGenericArguments().First());
                                }
                                else
                                {
                                    void setValue(object x)
                                    {
                                        property.SetValue(newobj, x);
                                    }
                                    
                                    if (property.HasAttribute<DeserializeInPlaceAttribute>())
                                    {
                                        var current = property.GetValue(newobj);
                                        if (current == null)
                                            throw new Exception($"Unable to deserialize {property} in-place.");
                                        this.TryDeserializeObject(element2, TypeData.GetTypeData(current), (x) => { },
                                            current, true);
                                    }
                                    else
                                    {
                                        Serializer.Deserialize(element2, setValue, property.TypeDescriptor);    
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (logWarnings)
                                {
                                    Log.Warning(element2, "Unable to set property '{0}'.", CurrentMember.Name);
                                    Log.Warning("Error was: \"{0}\".", e.Message);
                                    Log.Debug(e);
                                }
                            }
                            finally
                            {
                                CurrentMember = prev;
                            }
                        }

                        int nowFound = visited.Count(x => x);
                        if (found == nowFound && order == nextOrder)
                        {
                            // The might have changed by loading properties.
                            var t3 = TypeData.GetTypeData(newobj);
                            if (Equals(t3, t2) == false)
                            {
                                if (nowFound != foundWithCurrentType) 
                                {  //  check avoids infinite loop if ITypeData did not overload Equals.
                                    
                                    foundWithCurrentType = nowFound;
                                    t2 = t3;
                                    continue;
                                }
                            }
                            if (nowFound < elements.Length)
                            { // still elements left to check. Last resort to try loading at defer.

                                // Some of the items might not be deserializable before defer load.
                                // if this is the case do this as a defer action.
                                void postDeserialize()
                                {
                                    t2 = TypeData.GetTypeData(newobj);
                                    for(int j = 0; j < elements.Length; j++)
                                    {
                                        if (visited[j]) continue;
                                        var propertyElement = elements[j];
                                        IMemberData property = null;
                                        var elementName = XmlConvert.DecodeName(propertyElement.Name.LocalName);
                                        try
                                        {   
                                            property = t2.GetMember(elementName);
                                            if (property == null)
                                                property = t2.GetMembers().FirstOrDefault(x => x.Name == elementName);
                                        }
                                        catch { }
                                        if (property == null || property.Writable == false)
                                        {
                                            continue;
                                        }
                                        
                                        Action<object> setValue = x => property.SetValue(newobj, x);
                                        var prevobj2 = Object;
                                        var prevmember = this.CurrentMember;
                                        Object = newobj;
                                        CurrentMember = property;
                                        // at this point the serializer stack is technically empty,
                                        // so add the current on top and deserialize.
                                        Serializer.PushActiveSerializer(this);
                                        try
                                        {
                                            Serializer.Deserialize(propertyElement, setValue, property.TypeDescriptor);
                                        }
                                        finally
                                        {
                                            // clean up.
                                            Serializer.PopActiveSerializer();
                                            Object = prevobj2;
                                            CurrentMember = prevmember;
                                        }

                                        visited[j] = true;
                                    }
                                    // print a warning message if the element could not be deserialized.
                                    for (int j = 0; j < elements.Length; j++)
                                    {
                                        if (visited[j]) continue;
                                        var elem = elements[j];
                                        var elementName = elem.Name.LocalName;
                                        if (elementName.Contains('.') == false)
                                        {
                                            // if the element name contains '.' it is usually a special name and hence
                                            // an error message is not needed. e.g:
                                            //     Package.Dependencies
                                            //     TestStep.Inputs
                                            var message =
                                                $"Unable to read element '{elem.Name.LocalName}'. The property does not exist.";
                                            Serializer.PushError(elem, message);
                                        }
                                    }
                                }
                                Serializer.DeferLoad(postDeserialize);
                            }
                            break;
                        }
                        order = nextOrder;
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

        bool readContentInternal(Type propType, bool ignoreComponentSettings, Func<string> getvalueString, XElement elem, out object outvalue)
        {
            
            object value = null;
            bool ok;

            if (propType.IsEnum || propType.IsPrimitive || propType == typeof(string) || propType.IsValueType || propType == typeof(Type))
            {  
                ok = true;
                string valueString = getvalueString()?.Trim();

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
                        // elem.Element("Base64") fails if the attribute "xmlns" is set on the document.
                        // In this case, "Base64" must be prepended with the value of xmlns in brackets.
                        // If the namespace is not set, ns.GetName("Base64") will just evaluate to "Base64"
                        var ns = elem.GetDefaultNamespace();
                        var encode = elem.Element(ns.GetName("Base64"));
                        if (encode != null)
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
                    if (propType == typeof(char))
                        value = ((string) value)[0];
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
                }
                else if (tryConvertNumber(propType, valueString, out value))
                {
                    // Conversions done in tryConvertNumber
                }
                else if (propType.HasInterface<IConvertible>())
                {
                    value = Convert.ChangeType(valueString, propType, CultureInfo.InvariantCulture);
                }else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    // For nullable<T> we recurse focusing on the underlying non-nullable type.
                    if (valueString == null)
                    {
                        outvalue = null;
                        return true; // null is a valid value for nullable types.
                    }

                    return readContentInternal(Nullable.GetUnderlyingType(propType), ignoreComponentSettings, getvalueString, elem, out outvalue);
                }
                else
                {
                    ok = false;
                }
            }
            else
            {
                ok = false;
            }

            outvalue = value;
            return ok;
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

        // for detecting cycles in object serialization.
        HashSet<object> cycleDetetionSet = new HashSet<object>();

        bool AlwaysG17DoubleFormat = false;

        internal static readonly XName DefaultValue = "DefaultValue";

        /// <summary>
        /// Deserializes an object from XML.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        public override bool Deserialize(XElement element, ITypeData t, Action<object> setter)
        {
            try
            {
                if (t is TypeData ctd)
                {
                    if (readContentInternal(ctd.Type, false, () => element.IsEmpty ? null : element.Value, element, out object obj))
                    {
                        setter(obj);
                        return true;
                    }
                }
                if (TryDeserializeObject(element, t, setter))
                    return true;
            }
            catch (Exception ex) 
            {
                Serializer.HandleError(element, $"Unable to read {t.GetDisplayAttribute().GetFullName()}.", ex);
            }
            return false;
        }
        /// <summary>
        /// Checks if the string can be turned into an XML string. 
        /// This should return true only if the transformation to/from xml is reversible.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static bool containsOnlyReversibleTapXmlChars(string str)
        {
            int len = str.Length;
            for(int i = 0; i < len; i++)
            {
                char c = str[i];
                if (c == '\r') // special case. Somehow deserialization turns \n into \r.
                    return false;
                if (char.IsLetter(c))
                    continue;
                if (XmlConvert.IsXmlChar(c))
                    continue;
                if(i < len - 1)
                {
                    char c2 = str[i + 1];
                    if(XmlConvert.IsXmlSurrogatePair(c2, c))
                        continue;
                }
                return false;
                
            }
            return true;
        }

        private List<string> GetUsedFiles(object obj)
        {
            var result = new List<string>();

            string GetStringOrMacroString(IMemberData prop, object o)
            {
                if (prop.HasAttribute<XmlIgnoreAttribute>())
                    return null;
                object val = prop.GetValue(o);

                if (val is string s)
                    return s;
                if (val is MacroString m)
                    return m.ToString();

                return null;
            }

            var td = TypeData.GetTypeData(obj);

            var props = td.GetMembers().Where(m => m.HasAttribute<FileDependencyAttribute>());
            foreach (var prop in props)
            {
                var path = GetStringOrMacroString(prop, obj);

                if (string.IsNullOrWhiteSpace(path) == false)
                    result.Add(path);
            }

            return result;
        }

        // this 'cache' only caches serialized values during this instance of the serialize
        // this is to avoid a memory leak for e.g GUIDs, which falls into the category of primitives.
        readonly Dictionary<object, string> enumTable = new Dictionary<object, string>();

        /// <summary>
        /// Serializes an object to XML.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="obj"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public override bool Serialize(XElement elem, object obj, ITypeData expectedType)
        {
            if (obj == null)
                return true;

            object prevObj = Object;
            // If cycleDetectorLut already contains an element, we've been here before.
            // Note the cycleDetectorLut adds and removes the obj later if it is not already in it.
            if (!cycleDetetionSet.Add(obj))
                throw new Exception("Cycle detected");
            
            foreach (var fileResource in GetUsedFiles(obj))
                Serializer.NotifyFileUsed(fileResource);
            
            object obj2 = obj;
            try
            {
                Object = obj;
                switch (obj)
                {
                    case double d:
                        if (AlwaysG17DoubleFormat)
                        {
                            // the fastest and most accurate string representation of a double.
                            elem.Value = d.ToString("G17", CultureInfo.InvariantCulture);
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
                            var d_str = d.ToString("R", CultureInfo.InvariantCulture);
                            var d_re = double.Parse(d_str, CultureInfo.InvariantCulture);
                            if (d_re != d) 
                                // round trip not possible with R, use G17 instead.
                                d_str = d.ToString("G17", CultureInfo.InvariantCulture);
                            elem.Value = d_str;

                            return true;
                        }
                    case float f:
                        elem.Value = f.ToString("R", CultureInfo.InvariantCulture);
                        return true;
                    case decimal d:
                        elem.Value = BigFloat.Convert(d).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case char c:
                        // Convert to a string and then handle it later.
                        obj = Convert.ToString(c, CultureInfo.InvariantCulture);
                        break;
                }

                if (obj is string str) // handled separately due to "case char c" from above.
                {
                    // check if the str->xml is reversible otherwise base64 encode it.
                    if (containsOnlyReversibleTapXmlChars(str) && str.Trim() == str)
                    {
                        elem.Value = str;   
                    }
                    else
                    {
                        var subelem = new XElement("Base64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(str)));
                        elem.Add(subelem);
                    }
                    return true;
                }

                if (expectedType is TypeData type)
                {
                    if(type.Type.IsEnum || type.Type.IsPrimitive || type.Type.IsValueType)
                    {
                        if (type.Type == typeof(bool))
                        {
                            elem.Value =
                                (bool)obj ? "true" : "false"; // must be lower case for old XmlSerializer to work
                            return true;
                        }

                        if (enumTable.TryGetValue(obj, out var v))
                        {
                            elem.Value = v;
                            return true;
                        }
                        v = Convert.ToString(obj, CultureInfo.InvariantCulture);
                        enumTable[obj] = v;
                        elem.Value = v;
                        return true;
                    }
                }

                IMemberData xmlTextProp = null;
                var _type = TypeData.GetTypeData(obj);
                var properties = _type.GetMembers();
                foreach (IMemberData prop in properties)
                {
                    if (prop.HasAttribute<XmlIgnoreAttribute>()) continue;
                    if (prop.HasAttribute<XmlTextAttribute>())
                        xmlTextProp = prop;
                    var attr = prop.GetAttribute<XmlAttributeAttribute>();
                    if (attr != null)
                    {
                        var val = prop.GetValue(obj);
                        var defaultAttr = prop.GetAttribute<DefaultValueAttribute>();
                        var name = string.IsNullOrWhiteSpace(attr.AttributeName) ? prop.Name : attr.AttributeName;
                        if (defaultAttr != null && object.Equals(defaultAttr.Value, val))
                            continue;
                        string valStr = Convert.ToString(val, CultureInfo.InvariantCulture);
                        if (val is bool b)
                            valStr = b ? "true" : "false"; // must be lower case for old XmlSerializer to work
                        elem.SetAttributeValue(Serializer.PropertyXmlName(name), valStr);
                    }
                }

                if (xmlTextProp != null)
                { // XmlTextAttribute support
                    var text = xmlTextProp.GetValue(obj);
                    if (text != null)
                        Serializer.Serialize(elem, text, xmlTextProp.TypeDescriptor);
                }
                else
                {
                    foreach (IMemberData subProp in properties)
                    {
                        if (subProp.HasAttribute<XmlIgnoreAttribute>()) continue;
                        if (subProp.Readable && subProp.Writable && null == subProp.GetAttribute<XmlAttributeAttribute>())
                        {
                            var oldProp = CurrentMember;
                            CurrentMember = subProp;
                            try
                            {
                                object val = subProp.GetValue(obj);
                                
                                var enu = val as IEnumerable;
                                if (enu != null && enu.GetEnumerator().MoveNext() == false) // the value is an empty IEnumerable
                                {
                                    var defaultAttr = subProp.GetAttribute<DefaultValueAttribute>();
                                    if (defaultAttr != null && defaultAttr.Value == null)
                                        continue;
                                }

                                var attr = subProp.GetAttribute<XmlElementAttribute>();
                                if (subProp.TypeDescriptor is TypeData cst && cst.Type.HasInterface<IList>() &&
                                    cst.Type.IsGenericType && attr != null)
                                {
                                    // Special case to mimic old .NET XmlSerializer behavior
                                    foreach (var item in enu)
                                    {
                                        string name = attr.ElementName ?? subProp.Name;
                                        XElement elem2 = new XElement(Serializer.PropertyXmlName(name));
                                        SetHasDefaultValueAttribute(subProp, item, elem2);
                                        elem.Add(elem2);
                                        Serializer.Serialize(elem2, item, TypeData.FromType(cst.Type.GetGenericArguments().First()));
                                    }
                                }
                                else
                                {
                                    XElement elem2 = new XElement(Serializer.PropertyXmlName(subProp.Name));
                                    
                                    { // MetaDataAttribute -> save the metadata name in the test plan xml.
                                        if (subProp.GetAttribute<MetaDataAttribute>() is MetaDataAttribute metaDataAttr)
                                        {
                                            string name = metaDataAttr.Name ??
                                                          subProp.GetDisplayAttribute()?.Name ?? subProp.Name;
                                            elem2.SetAttributeValue("Metadata", name);
                                        }
                                    }
                                    
                                    // if the setting has the default value, then don't notify that the type has been used, 
                                    // because it will probably not have been used in the end.
                                    bool hasDefaultValue =  SetHasDefaultValueAttribute(subProp, val, elem2);
                                    
                                    elem.Add(elem2);
                                    
                                    Serializer.Serialize(elem2, val, subProp.TypeDescriptor, notifyTypeUsed: !hasDefaultValue);
                                }
                            }
                            catch (Exception e)
                            {
                                Serializer.PushError(null, $"Unable to serialize property '{subProp.Name}'.", e);
                            }
                            finally
                            {
                                CurrentMember = oldProp;
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
                if (!cycleDetetionSet.Remove(obj2))
                    throw new InvalidOperationException("obj was modified.");
            }
        }

        bool SetHasDefaultValueAttribute(IMemberData subProp, object val, XElement elem2)
        {
            
            var attr = subProp.GetAttribute<DefaultValueAttribute>();
            if (attr != null && !(subProp is IParameterMemberData))
            {
                Serializer.GetSerializer<DefaultValueSerializer>().RegisterDefaultValue(elem2, attr.Value);
                return val == attr.Value;
            }
            return false;
        }
    }
}

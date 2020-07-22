//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using System.Collections;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for Collections. </summary>
    internal class CollectionSerializer : TapSerializerPlugin, IConstructingSerializer
    {
        /// <summary> Order of this serializer.   </summary>
        public override double Order { get { return 1; } }

        internal IList CurrentSettingsList;

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement element, ITypeData _t, Action<object> setResult)
        {
            object prevobj = this.Object;
            try
            {

                var t = (_t as TypeData)?.Type;
                if (t == null || !t.DescendsTo(typeof(IEnumerable)) || t == typeof(string)) return false;

                IEnumerable finalValues = null;
                Type genericType = t.GetEnumerableElementType() ?? typeof(object);
                if (t.IsArray == false && t.HasInterface<IList>() && t.GetConstructor(Type.EmptyTypes) != null &&
                    genericType.IsValueType == false)
                {
                    finalValues = (IList) Activator.CreateInstance(t);
                    if (finalValues is ComponentSettings)
                    {
                        CurrentSettingsList = (IList) finalValues;
                    }
                }

                if (finalValues == null)
                    finalValues = new List<object>();

                var prevSetResult = setResult;
                setResult = x =>
                {
                    // ensure that IDeserializedCallback is going to get used.
                    prevSetResult(x);
                    var deserializedHandler = x as IDeserializedCallback;
                    if (deserializedHandler != null)
                    {
                        Serializer.DeferLoad(() =>
                        {
                            try
                            {
                                deserializedHandler.OnDeserialized();
                            }
                            catch (Exception e)
                            {
                                Log.Warning("Exception caught while handling OnDeserialized");
                                Log.Debug(e);
                            }
                        });
                    }
                };

                if (element.IsEmpty)
                {

                    try
                    {
                        if (t.IsArray)
                            setResult(Array.CreateInstance(t.GetElementType(), 0));
                        else if (t.IsInterface)
                            setResult(null);
                        else
                            setResult(Activator.CreateInstance(t));
                        return true;
                    }
                    catch (MemberAccessException)
                    {
                        // No or private zero-arg constructor.
                        // Cannot create instance - let it be null.
                    }

                    return false;
                }

                if (genericType.IsNumeric() && element.HasElements == false)
                {
                    var str = element.Value;
                    var parser = new NumberFormatter(CultureInfo.InvariantCulture);
                    var values2 = parser.Parse(str);
                    finalValues = values2.CastTo(genericType);
                    if (t == typeof(IEnumerable<>).MakeGenericType(genericType)
                        || finalValues.GetType() == t)
                    {
                        setResult(finalValues);
                        return true;
                    }
                }
                else
                {

                    this.Object = finalValues;
                    var vals = (IList) finalValues;
                    foreach (var node2 in element.Elements())
                    {
                        try
                        {
                            int index = vals.Count;
                            vals.Add(null);
                            if (node2.IsEmpty == false || node2.HasAttributes) // Assuming that null is allowed.
                            {
                                if (!Serializer.Deserialize(node2, x => vals[index] = x, t: genericType))
                                    // Serialization of an element failed, so remove the placeholder.
                                    vals.RemoveAt(vals.Count - 1);
                            }
                        }
                        catch (Exception e)
                        {
                            vals.RemoveAt(vals.Count - 1);
                            Log.Error(e.Message);
                            Log.Debug(e);
                            continue;
                        }
                    }
                }

                IEnumerable values;
                if (t.IsArray)
                {
                    int elementCount = finalValues.Cast<object>().Count();
                    values = (IList) Activator.CreateInstance(t, elementCount);
                    Serializer.DeferLoad(() =>
                    {
                        // Previous deserialization might have been deferred, so we have to defer again.
                        var lst = (IList) values;
                        int i = 0;
                        foreach (var item in finalValues)
                            lst[i++] = item;
                        setResult(values);
                    });
                }
                else if (t.DescendsTo(typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)))
                {
                    var lst = Activator.CreateInstance(typeof(List<>).MakeGenericType(genericType), finalValues);
                    values = (IList) Activator.CreateInstance(t, lst);
                }
                else if (t.HasInterface<IList>())
                {
                    if (finalValues.GetType() == t)
                    {
                        values = (IList) finalValues;
                    }
                    else
                    {
                        values = (IList) Activator.CreateInstance(t);
                        if (values is ComponentSettings)
                        {
                            // ComponentSettings must be loaded now.
                            foreach (var item in finalValues)
                                ((IList) values).Add(item);
                            CurrentSettingsList = (IList) values;
                        }
                        else
                        {
                            foreach (var val in finalValues)
                                ((IList) values).Add(val);
                        }
                    }
                }
                else if (finalValues is IList lst)
                {
                    values = lst;
                }
                else if (finalValues is IEnumerable en)
                {
                    var lst2 = new List<object>();
                    foreach (var value in en.Cast<object>())
                        lst2.Add(value);
                    values = lst2;
                }
                else
                {
                    throw new Exception("Unable to deserialize list");
                }

                if (values.GetType().DescendsTo(t) == false)
                {
                    try
                    {
                        dynamic newvalues = Activator.CreateInstance(t);
                        if (newvalues is IDictionary)
                        {
                            foreach (dynamic x in values)
                                newvalues.Add(x.Key, x.Value); // x is KeyValuePair.
                        }
                        else
                        {
                            foreach (dynamic x in values)
                                newvalues.Add(x);
                        }

                        values = newvalues;
                    }
                    catch (Exception)
                    {
                        Log.Warning(element, "Unable to deserialize enumerable type '{0}'", t.Name);
                    }
                }

                setResult(values);
                return true;
            }
            finally
            {
                this.Object = prevobj;
            }
        }

        static readonly XName Element = "Element"; 
        
        internal HashSet<object> ComponentSettingsSerializing = new HashSet<object>();
        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object sourceObj, ITypeData expectedType)
        {
            if (sourceObj is IEnumerable == false || sourceObj is string) return false;
            IEnumerable sourceEnumerable = (IEnumerable)sourceObj;
            Type type = sourceObj.GetType();
            Type genericTypeArg = type.GetEnumerableElementType();
            if (genericTypeArg.IsNumeric())
            {
                var parser = new NumberFormatter(CultureInfo.InvariantCulture) {UseRanges = false};
                elem.Value = parser.FormatRange(sourceEnumerable);
                return true;
            }

            object prevObj = this.Object;
            try
            {
                this.Object = sourceObj;
                bool isComponentSettings = sourceObj is ComponentSettings;
                foreach (object obj in sourceEnumerable)
                {
                    var step = new XElement(Element);
                    if (obj != null)
                    {
                        type = obj.GetType();
                        step.Name = TapSerializer.TypeToXmlString(type);
                        if (isComponentSettings)
                            ComponentSettingsSerializing.Add(obj);
                        try
                        {
                            Serializer.Serialize(step, obj, expectedType: TypeData.FromType(genericTypeArg));
                        }
                        finally
                        {
                            if (isComponentSettings)
                                ComponentSettingsSerializing.Remove(obj);
                        }
                    }

                    elem.Add(step);
                }
            }
            finally
            {
                this.Object = prevObj;
            }

            return true;
        }

        public object Object { get; private set; }

        public IMemberData CurrentMember => null;
    }

}

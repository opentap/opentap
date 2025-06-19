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
        public override double Order { get; } = 1;
        
        /// <summary> Current serializing element type. </summary>
        public Type CurrentElementType { get; private set; }

        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement element, ITypeData _t, Action<object> setResult)
        {
            object prevobj = this.Object;
            var prevElementType = CurrentElementType;
            try
            {
                var t = (_t as TypeData)?.Type;
                if (t == null || !t.DescendsTo(typeof(IEnumerable)) || t == typeof(string)) return false;

                IEnumerable finalValues = null;
                Type genericType = t.GetEnumerableElementType() ?? typeof(object);
                CurrentElementType = genericType;
                if (t.IsArray == false && t.HasInterface<IList>() && t.GetConstructor(Type.EmptyTypes) != null &&
                    genericType.IsValueType == false)
                {
                    finalValues = (IList) Activator.CreateInstance(t);
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

                bool tryGetFactory(out Func<IList> ctor)
                {
                    var os = Serializer.SerializerStack
                        .OfType<ObjectSerializer>()
                        .FirstOrDefault();
                    if (os?.CurrentMember is IMemberData member
                        && member.GetAttribute<FactoryAttribute>() is {} fac
                        && member.TypeDescriptor.DescendsTo(t))
                    {
                        if (member is MemberData && os.Object != null)
                        {
                            ctor = () => (IList)FactoryAttribute.Create(os.Object, fac);
                            return true;
                        }

                        if (member is IParameterMemberData pmem)
                        {
                            ctor = () =>
                            {
                                if (!FactoryAttribute.TryCreateFromMember(pmem, fac, out var o))
                                    throw new Exception(
                                        $"Cannot create object of type '{pmem.TypeDescriptor.Name}' using factory '{fac.FactoryMethodName}'.");
                                if (o is not IList lst)
                                    throw new Exception(
                                        $"Object constructed from factory '{fac.FactoryMethodName}' is not a list.");
                                return lst;
                            };
                            return true;
                        }
                    }

                    ctor = null;
                    return false;
                }


                if (element.IsEmpty)
                {
                    try
                    {
                        var os = Serializer.SerializerStack
                            .OfType<ObjectSerializer>()
                            .FirstOrDefault();
                        if (!_t.CanCreateInstance && !t.IsArray && tryGetFactory(out var f))
                        {
                            setResult(f());
                        }
                        else if (t.IsArray)
                        {
                            setResult(Array.CreateInstance(t.GetElementType(), 0));
                        }
                        else if (t.IsInterface)
                        {
                            setResult(null);
                        }
                        else
                        {
                            setResult(Activator.CreateInstance(t));
                        }

                        return true;
                    }
                    catch (MemberAccessException)
                    {
                        // No or private zero-arg constructor.
                        // Cannot create instance - let it be null.
                    }

                    return false;
                }
                
                IEnumerable values = null;
                
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
                
                {
                    if (!_t.CanCreateInstance && !t.IsArray)
                    {
                        var os = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
                        var mem = os?.CurrentMember;
                        if (mem == null) throw new Exception("Unable to get member list");
                        /* First check if there is a factory we can use. */
                        if (tryGetFactory(out var f))
                        {
                            values = f();
                            this.Object = values;
                        } 
                        else /* otherwise try to update in place */
                        {
                            // the data has to be updated in-place.
                            var val = ((IEnumerable)mem.GetValue(os.Object)).Cast<object>();
                            var elems = element.Elements();
                            if (elems.Count() != val.Count())
                            {
                                Log.Warning("Deserialized unbalanced list.");
                            }

                            foreach (var (elem, obj) in elems.Pairwise(val))
                            {
                                if (!os.TryDeserializeObject(elem, TypeData.GetTypeData(obj), o => { }, obj, true))
                                    return false;
                            }

                            return true;
                        }
                    }

                    if(this.Object != values)
                        this.Object = finalValues;
                    if (finalValues is ICombinedNumberSequence seq)
                        finalValues = seq.Cast<object>().ToArray();
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
                else if (!_t.CanCreateInstance && !t.IsArray && tryGetFactory(out _))
                {
                    var lst = (IList)values;
                    foreach (var item in finalValues)
                        lst.Add(item);
                    values = lst;
                }
                else if (t.DescendsTo(typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)))
                {
                    var lst = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(genericType));
                    foreach (var item in finalValues)
                        lst.Add(item);
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
                CurrentElementType = prevElementType;
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
                    // We need to add the step to the document immediately so each serializer call 
                    // has access to the element's parents.
                    elem.Add(step);

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
                        catch
                        {
                            // Remove the element from the document in case serialization fails
                            step.Remove();
                            throw;
                        }
                        finally
                        {
                            if (isComponentSettings)
                                ComponentSettingsSerializing.Remove(obj);
                        }
                    }

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

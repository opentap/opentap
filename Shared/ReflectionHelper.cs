//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using OpenTap.Translation;

//**** WARNING ****//
// This file is used in many projects(link existing), but only with internal protection.
// NEVER insert a public class here or things will break due to multiple definitions of the same class.
// Bugs introduced here will cause bugs in other projects too, so be careful.
// **

namespace OpenTap
{
    /// <summary>
    /// Class to ease the use of reflection.
    /// </summary>
    internal static class ReflectionHelper
    {
        /// <summary>Returns true if 'type' and 'basetype' are equal. </summary> 
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool IsA(this ITypeData type, Type basetype)
        {
            if (type is TypeData cst)
                return cst.Type == basetype;
            return false;
        }
        
        public static T GetBaseType<T>(this ITypeData type) where T: ITypeData
        {
            var typeIterator = type;
            while (typeIterator != null)
            {
                if (typeIterator is TypeData)
                    break;
                if (typeIterator is T t2)
                    return t2;
                typeIterator = typeIterator.BaseType;
            }
            return default;
        }
        
        /// <summary> Really fast direct descendant test. This checks for reference equality of the type or a base type, and 'baseType'.
        /// Given these constraints are met, this can be 6x faster than DescendsTo, but should only be used in special cases. </summary>
        public static bool DirectInheritsFrom(this ITypeData type, ITypeData baseType)
        {
            do
            {
                if(ReferenceEquals(type, baseType)) return true;
                type = type.BaseType;
            } while (type != null);
            return false;
        }

        public static TypeData AsTypeData(this ITypeData type)
        {
            do
            {
                if (type is TypeData td)
                    return td;
                type = type?.BaseType;
            } while (type != null);
            return null;
        } 

        static Dictionary<MemberInfo, DisplayAttribute> displayLookup = new Dictionary<MemberInfo, DisplayAttribute>(1024);
        static object displayLookupLock = new object();

        public static DisplayAttribute GetDisplayAttribute(this MemberInfo type)
        {
            lock (displayLookupLock)
            {
                if (!displayLookup.ContainsKey(type))
                {
                    DisplayAttribute attr;
                    try
                    {
                        attr = type.GetAttribute<DisplayAttribute>();
                    }
                    catch
                    {   // This might happen for outdated plugins where an Attribute type ceased to exist.
                        attr = null;
                    }

                    if (attr == null)
                    {
                        attr = new DisplayAttribute(type.Name, null, Order: -10000, Collapsed: false);
                    }

                    displayLookup[type] = attr;
                }

                return displayLookup[type];
            }
        }

        /// <summary>
        /// Parses a DisplayName into a group:name pair.
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        internal static string ParseDisplayname(string displayName, out string group)
        {
            group = "";
            var parts = displayName.Trim().TrimStart('-').Split('\\');

            if (parts.Length == 1) return displayName;
            else if (parts.Length >= 2)
            {
                group = parts[0].Trim();
                return parts.Last().Trim();
            }
            else
                return displayName.Trim();
        }
       
        static object[] getAttributes(MemberInfo mem)
        {
            try
            {
                return mem.GetCustomAttributes(true);
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        static readonly ConditionalWeakTable<MemberInfo, object[]> attrslookup = new ConditionalWeakTable<MemberInfo, object[]>();
        public static object[] GetAllCustomAttributes(this MemberInfo prop)
        {
            return attrslookup.GetValue(prop, getAttributes);
        }
        
        static object[] getAttributesNoInherit(MemberInfo mem)
        {
            try
            {
                return mem.GetCustomAttributes(false);
            }
            catch
            {
                return Array.Empty<object>();
            }
        }
        static readonly ConditionalWeakTable<MemberInfo, object[]> attrslookupNoInherit = new ConditionalWeakTable<MemberInfo, object[]>();
        public static object[] GetAllCustomAttributes(this MemberInfo prop, bool inherit)
        {
            if(!inherit)
                return attrslookupNoInherit.GetValue(prop, getAttributesNoInherit);
            return GetAllCustomAttributes(prop);
        }

        /// <summary>
        /// Gets the custom attributes. Both type and property attributes. Also inherited attributes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T[] GetCustomAttributes<T>(this MemberInfo prop) where T : Attribute
        {
            // This method impacts GUI, serialization and even test plan execution times.
            // it needs to be as fast as possible.

            // Avoid allocation when there is nothing of type T in the attributes
            var array = GetAllCustomAttributes(prop);
            int cnt = 0;
            foreach (var attr in array)
            {
                if (attr is T)
                    cnt++;
            }

            if (cnt == 0)
                return Array.Empty<T>(); // This avoids allocation of empty arrays.
            T[] result = new T[cnt];

            cnt = 0;
            foreach (var attr in array)
            {
                if (attr is T a)
                {
                    result[cnt] = a;
                    cnt++;
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the first or default of the custom attributes for this member. Both type and property attributes also inherited attributes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T GetFirstOrDefaultCustomAttribute<T>(this MemberInfo prop) where T : Attribute
        {
            foreach (var attr in GetAllCustomAttributes(prop))
                if (attr is T a)
                    return a;
            return null;
        }

        /// <summary>
        /// Gets the first or default of the custom attributes for this property. Both type and property attributes also inherited attributes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T GetAttribute<T>(this MemberInfo prop) where T : Attribute
        {
            return GetFirstOrDefaultCustomAttribute<T>(prop);
        }

        /// <summary>
        /// return whether the property has a given attribute T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool HasAttribute<T>(this MemberInfo prop) where T : Attribute
        {
            return prop.IsDefined(typeof(T), true);
        }
        /// <summary>
        /// Return whether the attribute has the given attribute T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool HasAttribute<T>(this Type t) where T : Attribute
        {
            return t.IsDefined(typeof(T), true);
        }

        /// <summary>
        /// Returns true if a MemberInfo is Browsable.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static bool IsBrowsable(this MemberInfo m)
        {
            var b = m.GetAttribute<BrowsableAttribute>();
            if (b == null) return true;
            return b.Browsable;
        }



        /// <summary>
        /// Check whether a type 'descends' to otherType or "can be otherType".
        /// </summary>
        /// <param name="t"></param>
        /// <param name="otherType"></param>
        /// <returns></returns>
        public static bool DescendsTo(this Type t, Type otherType)
        {
            if (t == otherType)
                return true;
            if (otherType.IsGenericTypeDefinition)
            {   // In the case otherType is constructed from typeof(X<>), not typeof(X<T>).

                if (otherType.IsInterface)
                {
                    var interfaces = t.GetInterfaces();
                    foreach (var iface in interfaces)
                    {
                        if (iface.IsGenericType)
                        {
                            if (iface.GetGenericTypeDefinition() == otherType)
                                return true;
                        }   
                    }
                    if (t.IsInterface)
                    {
                        if (t.IsGenericType)
                        {
                            if (t.GetGenericTypeDefinition() == otherType)
                                return true;
                        }
                    }
                }
                else
                {
                    Type super = t;
                    while (super != typeof(object) && super != null /*if not a class*/)
                    {
                        if (super.IsGenericType && super.GetGenericTypeDefinition() == otherType)
                            return true;
                        super = super.BaseType;
                    }
                }
            }
            
            return otherType.IsAssignableFrom(t);
        }

        /// <summary>
        /// returns whether t has a given interface T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool HasInterface<T>(this Type t)
        {
            return typeof(T).IsAssignableFrom(t);
        }

        /// <summary>
        /// Returns true if a type is numeric.
        /// </summary>
        public static bool IsNumeric(this Type t)
        {
            if (t.IsEnum)
                return false;
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary> Returns true if a type is numeric. </summary>
        public static bool IsNumeric(this ITypeData t)
        {
            return t.AsTypeData()?.Type.IsNumeric() == true;
        }
        
        /// <summary> Returns true if a type is a C# primitive. </summary>
        public static bool IsPrimitive(this ITypeData t)
        {
            return t.AsTypeData()?.Type.IsPrimitive ?? false;
        }

        /// <summary> Creates an instance of t with no constructor arguments. </summary>
        public static object CreateInstance(this Type t, params object[] args) => Activator.CreateInstance(t, args);

        /// <summary> Creates an instance of type t. If an error occurs it returns null and prints an error message. </summary>
        public static object CreateInstanceSafe(this ITypeData t, params object[] args)
        {
            try
            {
                return t.CreateInstance(args);
            }
            catch(Exception e)
            {
                var log = Log.CreateSource("Reflection");
                log.Error($"Cannot create instance of {t.Name}: '{e.Message}'");
                log.Debug(e);
            }
            return null;
        }

        /// <summary>
        /// If Type is a collection of items, get the element type.
        /// </summary>
        /// <param name="enumType"></param>
        /// <returns></returns>
        static public Type GetEnumerableElementType(this Type enumType)
        {
            if (enumType.IsArray)
                return enumType.GetElementType();
            if (enumType.IsGenericTypeDefinition)
                return null;
            try
            {
                // check if it *has* the interface
                var ienumInterface = enumType.GetInterface(typeof(IEnumerable<>).Name);
                if (ienumInterface != null)
                    return ienumInterface.GetGenericArguments().FirstOrDefault();
            }
            catch (AmbiguousMatchException)
            {
                // the type implements multiple different IEnumerable<> interfaces.
                // this is an odd case that is not commonly seen.
            }

            // check if it *is* the interface.
            if(enumType.IsInterface && enumType.IsGenericType && enumType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return enumType.GetGenericArguments().FirstOrDefault();
            }

            return null;
        }

        static readonly Dictionary<Type, PropertyInfo[]> propslookup = new Dictionary<Type, PropertyInfo[]>(1024);
        static PropertyInfo[] getPropertiesTap(Type t)
        {
            lock (propslookup)
            {
                if (propslookup.ContainsKey(t) == false)
                {
                    try
                    {
                        propslookup[t] = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    }
                    catch
                    {
                        propslookup[t] = Array.Empty<PropertyInfo>();
                    }
                }
                return propslookup[t];
            }
        }

        public static bool ContainsMember(this IParameterMemberData p, (object Source, IMemberData Member) item)
        {
            return p.ParameterizedMembers.Contains(item);
        }
            

        /// <summary> Extracts properties from a Type that are public and not static. Default GetProperties() also returns static properties. </summary>
        public static PropertyInfo[] GetPropertiesTap(this Type type)
        {
            return getPropertiesTap(type);
        }

        static readonly Dictionary<Type, MethodInfo[]> propslookup2 = new Dictionary<Type, MethodInfo[]>(1024);
        static MethodInfo[] getMethodsTap(Type t)
        {
            lock (propslookup2)
            {
                if (propslookup2.ContainsKey(t) == false)
                {
                    try
                    {
                        propslookup2[t] = t.GetMethods();
                    }
                    catch
                    {
                        propslookup2[t] = Array.Empty<MethodInfo>();
                    }
                }
                return propslookup2[t];
            }
        }

        /// <summary> Extracts properties from a Type that are public and not static. Default GetProperties() also returns static properties. </summary>
        public static MethodInfo[] GetMethodsTap(this Type type)
        {
            return getMethodsTap(type);
        }

        public static bool MethodOverridden(this Type t, Type baseType, string methodName)
        {
            var m1 = baseType.GetMethod(methodName).MethodHandle.Value;
            var m2 = t.GetMethod(methodName).MethodHandle.Value;
            return m1 != m2;
        } 

        /// <summary> Get the base C# type of a given type. </summary>
        internal static T As<T>(this ITypeData type) where T: ITypeData
        {
            for(;type != null; type = type.BaseType)
                if (type is T td)
                    return td;
            return default;
        }
        
        public static void GetAttributes<T>(this IReflectionData mem, System.Collections.IList outList)
        {
            foreach (var item in mem.Attributes)
            {
                if (item is T x)
                    outList.Add(x);
            }
        }
    }
    
    internal class Memorizer
    {
        /// <summary>
        /// Enumerates how cyclic invokes can be handled.
        /// </summary>
        public enum CyclicInvokeMode
        {
            /// <summary>
            /// Specifies that an exception should be thrown.
            /// </summary>
            ThrowException,
            /// <summary>
            /// Specifies that default(ResultT) should be returned.
            /// </summary>
            ReturnDefaultValue
        }
    }

    internal interface IMemorizer<ArgT, ResultT>
    {
        ResultT Invoke(ArgT arg);
        void InvalidateAll();

    }

    /// <summary>
    /// Convenient when some memorizer optimizations can be done. 
    /// Includes functionality for decay time and max number of elements.
    /// It assumes that the same ArgT will always result in the same ResultT.
    /// </summary>
    /// <typeparam name="ArgT"></typeparam>
    /// <typeparam name="ResultT"></typeparam>
    /// <typeparam name="MemorizerKey"></typeparam>
    internal class Memorizer<ArgT, ResultT, MemorizerKey> : Memorizer, IMemorizer<ArgT, ResultT>
    {
        /// <summary>
        /// Used for locking the invokation of a specific MemorizerKey. 
        /// This makes it possible to call Invoke in parallel and avoid recalculating the same value multiple times.
        /// </summary>
        class LockObject
        {
            public bool IsLocked;
        }
        
        /// <summary> If a certain time passes a result should be removed. By default, never. </summary>
        public TimeSpan SoftSizeDecayTime = TimeSpan.MaxValue;
        protected Func<ArgT, MemorizerKey> getKey;
        protected Func<ArgT, ResultT> getData = argt => (ResultT)(object)argt;
        
        readonly Dictionary<MemorizerKey, DateTime> lastUse = new Dictionary<MemorizerKey, DateTime>();
        readonly Dictionary<MemorizerKey, ResultT> memorizerTable = new Dictionary<MemorizerKey, ResultT>();
        readonly Dictionary<MemorizerKey, LockObject> locks = new Dictionary<MemorizerKey, LockObject>();

        /// <summary> Can be used to create a validation key for each key in the memorizer.
        /// Validation keys are used for checking if the memory is up to date or if it should be refreshed. </summary>
        public Func<MemorizerKey, object> Validator { get; set; } = null;
        readonly Dictionary<MemorizerKey, object> validatorData = new Dictionary<MemorizerKey, object>();
        
        /// <summary>
        /// Specifies how to handle situations where an Invoke(x) triggers another Invoke(x) in the same thread. 
        /// Since this might cause infinite recursion, it is not allowed. By default an exception is thrown.
        /// </summary>
        public CyclicInvokeMode CylicInvokeResponse = CyclicInvokeMode.ThrowException;

        public Nullable<ulong> MaxNumberOfElements { get; set; }

        public Memorizer(Func<ArgT, MemorizerKey> getKey = null,
            Func<ArgT, ResultT> extractData = null)
        {
            if (extractData != null)
                getData = extractData;
            this.getKey = getKey;
        }

        /// <summary>
        /// Forces manual update of constraints.
        /// </summary>
        public void CheckConstraints()
        {
            while (checkSizeConstraints() == Status.Changed) { }
        }

        enum Status
        {
            Changed,
            Unchanged
        }

        Status checkSizeConstraints()
        {
            if (SoftSizeDecayTime < TimeSpan.MaxValue || MaxNumberOfElements.HasValue
                && (ulong) memorizerTable.Count > MaxNumberOfElements.Value)
            {
                lock (memorizerTable)
                {

                    var removeKey = lastUse.Keys.FindMin(key2 => lastUse[key2]);
                    if (removeKey != null)
                    {
                        if (SoftSizeDecayTime < DateTime.UtcNow - lastUse[removeKey])
                        {
                            lastUse.Remove(removeKey);
                            memorizerTable.Remove(removeKey);
                            return Status.Changed;
                        }
                        else if (MaxNumberOfElements.HasValue
                                 && (ulong) memorizerTable.Count > MaxNumberOfElements.Value)
                        {
                            lastUse.Remove(removeKey);
                            memorizerTable.Remove(removeKey);
                            return Status.Changed;
                        }
                    }
                }
            }

            return Status.Unchanged;
        }
        
        MemorizerKey invokeGetKey(ArgT arg)
        {
            return getKey == null ? (MemorizerKey)(object)arg : getKey(arg);
        }

        public virtual ResultT OnCyclicCallDetected(ArgT key)
        {
            throw new Exception("Cyclic memorizer invoke detected.");
        }

        public ResultT this[ArgT arg] => Invoke(arg);

        public ResultT Invoke(ArgT arg)
        {
            var key = invokeGetKey(arg);
            if(Validator != null){
                var obj = Validator(key);
                lock (memorizerTable)
                {
                    if (validatorData.TryGetValue(key, out object value))
                    {
                        if (false == Equals(value, obj))
                        {
                            Invalidate(arg);
                            validatorData[key] = obj;
                        }   
                    }else
                    {
                        validatorData[key] = obj;
                    }
                }
            }
            
            LockObject lockObj;
            lock (memorizerTable)
            {
                lastUse[key] = DateTime.UtcNow;
                if (!locks.TryGetValue(key, out lockObj))
                {
                    lockObj = new LockObject();
                    locks[key] = lockObj;
                }
            }
            lock (lockObj)
            {
                if (lockObj.IsLocked)
                {   // Avoid running into a StackOverflowException.

                    if (CylicInvokeResponse == CyclicInvokeMode.ThrowException)
                        return OnCyclicCallDetected(arg);
                    return default(ResultT);
                }
                try
                {
                    lockObj.IsLocked = true;
                    lock (memorizerTable)
                    {
                        if (memorizerTable.TryGetValue(key, out ResultT value))
                            return value;
                    }
                    ResultT o = getData(arg);
                    lock (memorizerTable)
                    {
                        memorizerTable[key] = o;
                        checkSizeConstraints();
                    }
                    return o;
                }
                finally
                {
                    lockObj.IsLocked = false;
                }
            }
        }

        public ResultT GetCached(ArgT arg)
        {
            ResultT o = default(ResultT);
            var key = invokeGetKey(arg);

            lock (memorizerTable)
            {
                if (!memorizerTable.TryGetValue(key, out o))
                    return default(ResultT);
                lastUse[key] = DateTime.UtcNow;;
            }
            return o;
        }

        public void Add(ArgT arg, ResultT value)
        {
            var key = invokeGetKey(arg);

            lock (memorizerTable)
            {
                lastUse[key] = DateTime.UtcNow;
                memorizerTable[key] = value;
                checkSizeConstraints();
            }
        }

        public void Invalidate(ArgT value)
        {
            var key = invokeGetKey(value);

            lock (memorizerTable)
            {
                memorizerTable.Remove(key);
                lastUse.Remove(key);
                validatorData.Remove(key);
            }
        }

        /// <summary>
        /// Invalidate the keys where f returns true. This is being done while
        /// the memorizer is locked, so race conditions are avoided.
        /// </summary>
        /// <param name="predicate"></param>
        public void InvalidateWhere(Func<MemorizerKey, ResultT, bool> predicate)
        {
            lock (memorizerTable)
            {
                List<MemorizerKey> keys = null;
                foreach(var item in memorizerTable)
                {
                    if(predicate(item.Key, item.Value))
                    {
                        if(keys == null)
                        {
                            keys = new List<MemorizerKey>();
                        }
                        keys.Add(item.Key);
                    }
                }
                if(keys != null)
                {
                    foreach(var k in keys)
                    {
                        memorizerTable.Remove(k);
                        lastUse.Remove(k);
                    }
                }
            }
        }

        public List<ResultT> GetResults()
        {
            lock (memorizerTable)
            {
                return memorizerTable.Values.ToList();
            }
        }


        public void InvalidateAll()
        {
            lock (memorizerTable)
            {
                memorizerTable.Clear();
                lastUse.Clear();
                validatorData.Clear();
            }
        }
    }

    internal class Memorizer<ArgT, ResultT> : Memorizer<ArgT, ResultT, ArgT>
    {
        public Memorizer(Func<ArgT, ResultT> func) : base(extractData: func)
        {
        }
    }


    static class Utils
    {
        static readonly char[] padding = { '=' };

        public static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd(padding).Replace('+', '-')
                .Replace('/', '_');
        }

        public static IEnumerable<(int, T)> WithIndex<T>(this IEnumerable<T> collection)
        {
            return collection.Select((ele, index) => (index, ele));
        }

        /// <summary>
        /// Thread-safe and lock free value exchange. valueGen depends on the current value.
        /// </summary>
        /// <param name="outPlace"> Where to place the value</param>
        /// <param name="valueGen"> Function generating a value based on the current value. </param>
        /// <typeparam name="T"> The type of object. </typeparam>
        public static void InterlockedSwap<T>(ref T outPlace, Func<T> valueGen) where T : class
        {
            while (true)
            {
                // safely add a new item to the list using the compare-and-swap atomic operation.
                var currentValue = outPlace;
                var nextValue = valueGen();

                if (Interlocked.CompareExchange(ref outPlace, nextValue, currentValue) == currentValue)
                {
                    break;
                }
            }
        }


#if DEBUG
        public static readonly bool IsDebugBuild = true;
#else
        public static readonly bool IsDebugBuild = false;
#endif

        /// <summary> Swaps two variables </summary>
        public static void Swap<T>(ref T a, ref T b) => (a, b) = (b, a);

        /// <summary> Do nothing. </summary>
        public static void Noop()
        {
        }

        /// <summary>
        /// Returns the element for which selector returns the max value.
        /// if IEnumerable is empty, it returns default(T) multiplier gives the direction to search.
        /// </summary>
        static T FindExtreme<T, C>(this IEnumerable<T> sequence, Func<T, C> selector, int multiplier)
            where C : IComparable
        {
            var e = sequence.GetEnumerator();
            if (!e.MoveNext())
                return default(T);

            T selected = e.Current;
            C max = selector(selected);

            while (e.MoveNext())
            {
                var obj = e.Current;
                C comparable = selector(obj);
                if (comparable.CompareTo(max) * multiplier > 0)
                {
                    selected = obj;
                    max = comparable;
                }
            }

            return selected;
        }

        /// <summary> Returns the element for which selector returns the max value. if IEnumerable is empty, it returns default(T). </summary>
        public static T FindMax<T, C>(this IEnumerable<T> ienumerable, Func<T, C> selector) where C : IComparable
        {
            return FindExtreme(ienumerable, selector, 1);
        }

        /// <summary>
        /// Returns the element for which selector returns the minimum value.
        /// if IEnumerable is empty, it returns default(T).
        /// </summary>
        public static T FindMin<T, C>(this IEnumerable<T> ienumerable, Func<T, C> selector) where C : IComparable
        {
            return FindExtreme(ienumerable, selector, -1);
        }

        /// <summary>
        /// Skips last N items.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="n">n last items to skip.</param>
        /// <returns></returns>
        public static IEnumerable<T> SkipLastN<T>(this IEnumerable<T> source, int n)
        {
            var list = source.ToList();
            if ((list.Count - n) > 0)
                return list.Take(list.Count - n);
            else
                return Enumerable.Empty<T>();
        }


        /// <summary>
        /// Removes items of source matching a given predicate.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pred"></param>
        public static void RemoveIf<T>(this IList<T> source, Predicate<T> pred)
        {
            if (source is List<T> lst)
            {
                lst.RemoveAll(pred);
                return;
            }

            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (pred(source[i]))
                {
                    source.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Removes items of source matching a given predicate.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pred"></param>
        public static void RemoveIf(this IList source, Predicate<object> pred)
        {

            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (pred(source[i]))
                {
                    source.RemoveAt(i);
                }
            }
        }

        private static void flattenHeirarchy<T>(IEnumerable<T> lst, Func<T, IEnumerable<T>> lookup, IList<T> result,
            HashSet<T> found)
        {
            foreach (var item in lst)
            {
                if (found != null)
                {
                    if (found.Contains(item))
                        continue;
                    found.Add(item);
                }

                result.Add(item);
                var sublist = lookup(item);
                if (sublist != null)
                    flattenHeirarchy(sublist, lookup, result, found);
            }
        }

        /// <summary>
        /// Flattens a recursive IEnumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lst"></param>
        /// <param name="lookup">Returns a list of the next level of elements. The returned value is allowed to be null and will in this case be treated like an empty list.</param>
        /// <param name="distinct">True if only one of each element should be inserted in the list.</param>
        /// <param name="buffer">Buffer to use instead of creating a new list to store the values. This can be used to avoid allocation.</param>
        /// <returns></returns>
        public static List<T> FlattenHeirarchy<T>(IEnumerable<T> lst, Func<T, IEnumerable<T>> lookup,
            bool distinct = false, List<T> buffer = null)
        {
            if (buffer != null)
                buffer.Clear();
            else
                buffer = new List<T>();
            flattenHeirarchy(lst, lookup, buffer, distinct ? new HashSet<T>() : null);
            return buffer;
        }

        public static void FlattenHeirarchyInto<T>(IEnumerable<T> lst, Func<T, IEnumerable<T>> lookup, ISet<T> set)
        {
            foreach (var item in lst)
            {
                if (set.Add(item))
                {
                    var sublist = lookup(item);
                    if (sublist != null)
                        FlattenHeirarchyInto(sublist, lookup, set);
                }
            }
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> func)
        {
            foreach (var item in source)
            {
                func(item);
            }
        }

        /// <summary>
        /// Appends a range of elements to an IEnumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="newObjects"></param>
        /// <returns></returns>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> source, params T[] newObjects)
        {
            return source.Concat(newObjects);
        }

        /// <summary>  First index where the result of predicate function is true. </summary>
        public static int IndexWhen<T>(this IEnumerable<T> source, Func<T, bool> pred)
        {
            int idx = 0;
            foreach (var item in source)
            {
                if (pred(item))
                {
                    return idx;
                }

                idx++;
            }

            return -1;
        }

        /// <summary>
        /// Returns true if the generic IEnumerable is empty
        /// </summary>
        /// <param name="enu"></param>
        /// <returns></returns>
        public static bool IsEnumerableEmpty(this IEnumerable enu)
        {
            var e = enu.GetEnumerator();
            bool empty = e.MoveNext() == false;
            (e as IDisposable)?.Dispose();
            return empty;
        }

        /// <summary>
        /// Returns true if the source is longer than count elements.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static bool IsLongerThan<T>(this IEnumerable<T> source, long count)
        {
            foreach (var _ in source)
                if (--count < 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Creates a HashSet from an IEnumerable.
        /// </summary>
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        /// <summary>
        /// Creates a HashSet from an IEnumerable, with a specialized comparer.
        /// </summary>
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(source, comparer);
        }

        /// <summary>
        /// The opposite of Where.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> Except<T>(this IEnumerable<T> source, Func<T, bool> selector)
        {
            foreach (var x in source)
                if (selector(x) == false)
                    yield return x;
        }

        /// <summary> As 'Select' but skipping null values.
        /// Short hand for/more efficient version of 'Select(f).Where(x => x != null)' </summary>
        /// <param name="source"></param>
        /// <param name="f"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T2> SelectValues<T1, T2>(this IEnumerable<T1> source, Func<T1, T2> f)
        {
            foreach (var x in source)
            {
                var value = f(x);
                if (value != null)
                    yield return value;
            }
        }

        /// <summary> As 'Select and FirstOrDefault' but skipping null values.
        /// Short hand for/more efficient version of 'Select(f).Where(x => x != null).FirstOrDefault()'
        /// </summary>
        public static T2 FirstNonDefault<T1, T2>(this IEnumerable<T1> source, Func<T1, T2> selector)
        {
            foreach (var x in source)
            {
                var value = selector(x);
                if (Equals(value, default(T2)) == false)
                    return value;
            }

            return default(T2);
        }

        /// <summary>
        /// Merged a dictionary into another, overwriting colliding keys.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="srcDict"></param>
        /// <param name="dstDict"></param>
        public static void MergeInto<T1, T2>(this Dictionary<T1, T2> srcDict, Dictionary<T1, T2> dstDict)
        {
            foreach (var kv in srcDict)
            {
                dstDict[kv.Key] = kv.Value;
            }
        }

        struct OnceLogToken
        {
            public object Token;
            public TraceSource Log;
        }

        static HashSet<OnceLogToken> logOnceTokens = new HashSet<OnceLogToken>();

        /// <summary>
        /// Avoids spamming the log with errors that 
        /// should only be shown once by memorizing token and TraceSource. 
        /// </summary>
        /// <returns>True if an error was logged.</returns>
        public static bool ErrorOnce(this TraceSource log, object token, string message, params object[] args)
        {
            lock (logOnceTokens)
            {
                var logtoken = new OnceLogToken { Token = token, Log = log };
                if (!logOnceTokens.Contains(logtoken))
                {
                    log.Error(message, args);
                    logOnceTokens.Add(logtoken);
                    return true;
                }

                return false;
            }
        }

        public static string ConvertToUnsecureString(this System.Security.SecureString securePassword)
        {
            if (securePassword == null)
                throw new ArgumentNullException("securePassword");

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        public static System.Security.SecureString ToSecureString(this string str)
        {
            System.Security.SecureString result = new System.Security.SecureString();
            foreach (var c in str)
                result.AppendChar(c);
            return result;
        }

        public static bool IsNumeric(object obj)
        {
            switch (obj)
            {
                case float _: return true;
                case double _: return true;
                case decimal _: return true;
                case byte _: return true;
                case char _: return true;
                case sbyte _: return true;
                case short _: return true;
                case ushort _: return true;
                case int _: return true;
                case uint _: return true;
                case long _: return true;
                case ulong _: return true;
                default: return false;
            }

        }

        public static bool Compatible(Version searched, Version referenced)
        {
            if (searched == null) return true;

            if (searched.Major != referenced.Major) return false;
            if (searched.Minor >= referenced.Minor) return true;

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <param name="flag"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        public static T SetFlag<T>(this T e, T flag, bool enabled) where T : struct
        {
            if (e is Enum == false)
                throw new InvalidOperationException("T must be an enum");
            int _e = (int)Convert.ChangeType(e, typeof(int));
            int _flag = (int)Convert.ChangeType(flag, typeof(int));
            int r;
            if (enabled)
                r = (_e | _flag);
            else
                r = (_e & ~_flag);

            return (T)Enum.ToObject(typeof(T), r);
        }

        public static string EnumToReadableString(Enum value)
        {
            if (value == null) return null;
            var enumType = value.GetType();
            var mem = enumType.GetMember(value.ToString()).FirstOrDefault();
            if (mem != null) 
            { 
                return TranslationManager.TranslateEnum(value).Name;
            }
            if (false == enumType.HasAttribute<FlagsAttribute>())
                return value.ToString();
            var zeroValue = Enum.ToObject(enumType, 0);
            if (value.Equals(zeroValue))
                return ""; // this does not happen if zeroValue is declared.

            var flags = Enum.GetValues(enumType).OfType<Enum>();
            var activeFlags = flags.Where(value.HasFlag).Except(f => f.Equals(zeroValue));
            var result = string.Join(" | ", activeFlags.Select(EnumToReadableString));
            if (string.IsNullOrEmpty(result) == false) return result;

            // last resort.
            var val = (long)Convert.ChangeType(value, TypeCode.Int64);
            return val.ToString();
        }

        public static string EnumToDescription(Enum value)
        {
            if (value == null) return null;
            var enumType = value.GetType();
            var mem = enumType.GetMember(value.ToString()).FirstOrDefault();
            // if member is null, fall back to the readable enum string (or description is null)
            return mem?.GetDisplayAttribute().Description ?? EnumToReadableString(value);
        }

        public static string SerializeToString(this TestPlan plan, bool throwOnErrors = false)
        {
            using (var mem = new MemoryStream())
            {
                var serializer = new TapSerializer();
                plan.Save(mem, serializer);
                if (throwOnErrors && serializer.Errors.Any())
                    throw new Exception(string.Join("\n", serializer.Errors));
                return Encoding.UTF8.GetString(mem.ToArray());
            }
        }


        public static object DeserializeFromString(string str)
        {
            return new TapSerializer().DeserializeFromString(str);
        }

        public static T DeserializeFromString<T>(string str) => (T)DeserializeFromString(str);

        class ActionDisposable : IDisposable
        {
            Action action;
            public ActionDisposable(Action action) => this.action = action;

            public void Dispose()
            {
                action();
                action = null;
            }
        }

        public static IDisposable WithDisposable(Action action)
        {
            return new ActionDisposable(action);
        }

        /// <summary> Gets or creates a value based on the key. This is useful for caches. </summary>
        public static V GetOrCreateValue<K, V>(this Dictionary<K, V> dictionary, K key, Func<K, V> createValue)
        {
            if (dictionary.TryGetValue(key, out V value))
                return value;
            return dictionary[key] = createValue(key);
        }

        public static string BytesToReadable(long bytes)
        {
            if (bytes < 1000) return $"{bytes} B";
            if (bytes < 1000000) return $"{bytes / 1000.0:0.00} kB";
            if (bytes < 1000000000) return $"{bytes / 1000000.0:0.00} MB";
            return $"{bytes / 1000000000.0:0.00} GB";
        }

        /// <summary> Backoff strategy for Retry.  </summary>
        static void Backoff(int retry, int sleepBaseMs = 100)
        {
            const double backoffFactor = 2.0;
            const double jitterFactor = 0.2;
            double baseSleep = Math.Pow(backoffFactor, retry) * sleepBaseMs;
            
            // add a bit of jitter to avoid "thundering herd" problems.
            Random random = new Random();
            double jitterAmount = 1.0 + jitterFactor * (random.NextDouble() - 0.5);
            int totalSleep = (int)(baseSleep * jitterAmount);
            
            if (totalSleep > 0)
                TapThread.Sleep(totalSleep);
        }

        /// <summary> Retries calling a function a number of times if calling it fails. </summary>
        /// <param name="function">The function to call.</param>
        /// <param name="retryOn">an exception type to retry on. By default this is typeof(Exception)</param>
        /// <param name="maxRetries">default 5 times</param>
        /// <param name="moreExceptions"> more exceptions to retry on</param>
        /// <param name="sleepBaseMs"> The time to sleep at first iteration. Subsequent sleeps with be longer</param>
        public static void Retry(Action function, Type retryOn = null, int maxRetries = 5, Type[] moreExceptions = null,
            int sleepBaseMs = 100)
        {
            Retry<int>(() =>
            {
                function();
                return 0;
            }, retryOn, maxRetries, moreExceptions, sleepBaseMs);
        }

        /// <summary> Retries calling a function a number of times if calling it fails. </summary>
        /// <param name="function">The function to call.</param>
        /// <param name="retryOn">an exception type to retry on. By default this is typeof(Exception)</param>
        /// <param name="maxRetries">default 5 times</param>
        /// <param name="moreExceptions"> more exceptions to retry on</param>
        /// <param name="sleepBaseMs"> The time to sleep at first iteration. Subsequent sleeps with be longer</param>
        /// <typeparam name="T">the return type.</typeparam>
        /// <returns>The return value </returns>
        public static T Retry<T>(Func<T> function, Type retryOn = null, int maxRetries = 5, Type[] moreExceptions = null,
            int sleepBaseMs = 100)
        {
            if (retryOn == null)
                retryOn = typeof(Exception);
            var allExceptions = new[] { retryOn }.Concat(moreExceptions ?? Array.Empty<Type>()).ToArray();

            for (int i = 0; i < maxRetries - 1; i++)
            {

                try
                {
                    return function();
                }
                catch (Exception ex)
                {

                    while (ex is AggregateException a && a.InnerExceptions.Count == 1)
                    {
                        ex = a.InnerExceptions[0];
                    }

                    var exType = ex.GetType();
                    if (allExceptions.Any(x => x.IsAssignableFrom(exType)))
                    {
                        // if one of the expected exceptions.
                        Backoff(i, sleepBaseMs);
                        continue;
                    }

                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
            
            // on the last attempt just call the function directly
            return function();
        }
    }

    static internal class Sequence
    {
        /// <summary> Turns item into a one element array, unless it is null.</summary>
        public static T[] AsSingle<T>(this T item) => item == null ? Array.Empty<T>() : new[] {item};
        
        /// <summary>
        /// Like distinct but keeps the last item. Returns List because we need to iterate until last element anyway.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        public static List<T> DistinctLast<T>(this IEnumerable<T> items)
        {
            Dictionary<T, int> d = new Dictionary<T, int>();
            int i = 0;
            foreach (var item in items)
            {
                d[item] = i;
                i++;
            }
            return d.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }
        
        internal static int ProcessPattern<T1>(IEnumerator objs, Action<T1> f1)
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t);
                        return 1;
                }
            }
            return 0;
        }
        
        internal static int ProcessPattern<T1, T2>(IEnumerator objs, Action<T1> f1, Action<T2> f2 )
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t); 
                        return 1 + ProcessPattern(objs, f2);
                    case T2 t:
                        f2(t);
                        return 1 + ProcessPattern(objs, f1);
                }
            }
            return 0;
        }
        
        internal static int ProcessPattern<T1, T2, T3>(IEnumerator objs, Action<T1> f1, Action<T2> f2, Action<T3> f3 )
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t); 
                        return 1 + ProcessPattern(objs, f2, f3);
                    case T2 t:
                        f2(t);
                        return 1 + ProcessPattern(objs, f1, f3);
                    case T3 t:
                        f3(t);
                        return 1 + ProcessPattern(objs, f1, f2);
                }
            }
            return 0;
        }

        /// <summary> Adds elements that arent null to the list. </summary>
        internal static void AddExceptNull<T>(this ICollection<T> list, T x)
        {
            if (x != null)
                list.Add(x);
        }
        
        internal static int ProcessPattern<T1, T2, T3, T4>(IEnumerator objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4 )
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t); 
                        return 1 + ProcessPattern(objs, f2, f3, f4);
                    case T2 t:
                        f2(t);
                        return 1 + ProcessPattern(objs, f1, f3, f4);
                    case T3 t:
                        f3(t);
                        return 1 + ProcessPattern(objs, f1, f2, f4);
                    case T4 t:
                        f4(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3);
                }
            }
            return 0;
        }
        
        public static int ProcessPattern<T1, T2, T3, T4, T5>(IEnumerator objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4 , Action<T5> f5 )
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t); 
                        return 1 + ProcessPattern(objs, f2, f3, f4, f5);
                    case T2 t:
                        f2(t);
                        return 1 + ProcessPattern(objs, f1, f3, f4, f5);
                    case T3 t:
                        f3(t);
                        return 1 + ProcessPattern(objs, f1, f2, f4, f5);
                    case T4 t:
                        f4(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3, f5);
                    case T5 t:
                        f5(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3, f4);
                }
            }
            return 0;
        }
        
        public static int ProcessPattern<T1, T2, T3, T4, T5, T6>(IEnumerator objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4 , Action<T5> f5, Action<T6> f6 )
        {
            while (objs.MoveNext())
            {
                switch (objs.Current)
                {
                    case T1 t:
                        f1(t); 
                        return 1 + ProcessPattern(objs, f2, f3, f4, f5, f6);
                    case T2 t:
                        f2(t);
                        return 1 + ProcessPattern(objs, f1, f3, f4, f5, f6);
                    case T3 t:
                        f3(t);
                        return 1 + ProcessPattern(objs, f1, f2, f4, f5,f6);
                    case T4 t:
                        f4(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3, f5,f6);
                    case T5 t:
                        f5(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3, f4,f6);
                    case T6 t:
                        f6(t);
                        return 1 + ProcessPattern(objs, f1, f2, f3, f4, f5);
                }
            }
            return 0;
        }
        
        public static int ProcessPattern<T1, T2>(IEnumerable<object> objs, Action<T1> f1, Action<T2> f2)
        {
            using (var e = objs.GetEnumerator())
                return ProcessPattern(e, f1, f2);
        }
        
        public static int ProcessPattern<T1, T2, T3>(IEnumerable<object> objs, Action<T1> f1, Action<T2> f2, Action<T3> f3)
        {
            using (var e = objs.GetEnumerator())
                return ProcessPattern(e, f1, f2, f3);
        }
        public static int ProcessPattern<T1, T2, T3, T4>(IEnumerable<object> objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4)
        {
            using (var e = objs.GetEnumerator())
                return ProcessPattern(e, f1, f2, f3, f4);
        }
        public static int ProcessPattern<T1, T2, T3, T4, T5>(IEnumerable<object> objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4, Action<T5> f5)
        {
            using (var e = objs.GetEnumerator())
                return ProcessPattern(e, f1, f2, f3, f4, f5);
        }
        public static int ProcessPattern<T1, T2, T3, T4, T5, T6>(IEnumerable<object> objs, Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4, Action<T5> f5, Action<T6> f6)
        {
            using (var e = objs.GetEnumerator())
                return ProcessPattern(e, f1, f2, f3, f4, f5, f6);
        }

        /// <summary>
        /// Count the number of elements in an enumerable.
        /// </summary>
        public static int Count(this IEnumerable enumerable)
        {
            if (enumerable is ICollection col)
                return col.Count;
            int c = 0;
            foreach (var _ in enumerable)
                c++;
            return c;
        }

        /// <summary>
        /// iterates lists and generates pairs of each list. Once the end is reached for one of the lists, execution stops. 
        /// </summary>
        public static IEnumerable<(T1, T2)> Pairwise<T1, T2>(this IEnumerable<T1> a, IEnumerable<T2> b)
        {
            using(var ia = a.GetEnumerator())
            using (var ib = b.GetEnumerator())
            {
                while (ia.MoveNext() && ib.MoveNext())
                {
                    yield return (ia.Current, ib.Current);
                }
            }
        }
        
        /// <summary>
        ///  process the enumerable source N objects at a time.
        /// </summary>
        public static IEnumerable<T> Batch<T>(this IEnumerable<T> src, int N)
        {
                var buffer = new T[N];
                int cnt = 0;
                foreach (var elem in src)
                {
                    buffer[cnt] = elem;
                    cnt += 1;
                    if (cnt == N)
                    {
                        foreach (var elem2 in buffer)
                            yield return elem2;
                        cnt = 0;
                    }
                }
                foreach (var elem2 in buffer.Take(cnt))
                    yield return elem2;
        }

        public static void Append<T>(ref T[]  array, params T[] appendage)
        {
            int preLen = array.Length;
            Array.Resize(ref array, array.Length + appendage.Length);
            Array.Copy(appendage, 0, array, preLen, appendage.Length);
        }
       
        public static IEnumerable<T2> TrySelect<T, T2>(this IEnumerable<T> src, Func<T, T2> f,
            Action<Exception, T> handler) => src.TrySelect<T, T2, Exception>(f, handler);
        public static IEnumerable<T2> TrySelect<T, T2>(this IEnumerable<T> src, Func<T,T2> f,
            Action<Exception> handler) => src.TrySelect<T, T2, Exception>(f, (e,v) => handler(e));
        
        public static IEnumerable<T2> TrySelect<T, T2, T3>(this IEnumerable<T> src, Func<T, T2> f, Action<T3, T> handler) where T3: Exception
        {
            foreach (var x in src)
            {
                T2 y;
                try
                {
                    y = f(x);
                }
                catch(T3 e)
                {
                    handler(e, x);
                    continue;
                }
                yield return y;
            }
        }
    }
    
    internal static class Time
    {
        /// <summary>
        /// A TimeSpan from seconds that does not round to nearest millisecond. (TimeSpan.FromSeconds does that).
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static TimeSpan FromSeconds(double seconds)
        {
            try
            {
                checked
                {
                    // if the multiplication here creates a number greater than
                    // long.Max (or overflows negatively), we just return
                    // TimeSpan.MaxValue/MinValue.
                    long ticks = (long)(seconds * 1e7);
                    return TimeSpan.FromTicks(ticks);
                }
            }
            catch (OverflowException)
            {
                if (double.IsNaN(seconds))
                    throw new ArithmeticException("Nan is not a supported time value.");
                
                if (seconds < 0)
                    return TimeSpan.MinValue;
                
                return TimeSpan.MaxValue;
            }
        }
    }
    
    /// <summary> Invoke an action after a timeout, unless canceled. </summary>
    class TimeoutOperation : IDisposable
    {
        /// <summary> Estimate of how long it takes for the user to loose patience.</summary>
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
        
        TimeoutOperation(TimeSpan timeout, Action action)
        {
            this.timeout = timeout;
            this.action = action;
            tokenSource = new CancellationTokenSource(timeout);
        }
        readonly Action action;
        readonly TimeSpan timeout;
        readonly CancellationTokenSource tokenSource;
        
        bool isCompleted;
        void wait()
        {
            try
            {
                var token = tokenSource.Token;
                if (!token.IsCancellationRequested && WaitHandle.WaitTimeout == WaitHandle.WaitAny(new [] { token.WaitHandle, TapThread.Current.AbortToken.WaitHandle }, timeout))
                    action();
            }
            finally
            {
                lock (tokenSource)
                {
                    tokenSource.Dispose();
                    isCompleted = true;
                }
            }
        }

        /// <summary> Creates a new TimeoutOperation with a specific timeout. </summary>
        /// <param name="timeout"></param>
        /// <param name="actionOnTimeout"></param>
        /// <returns></returns>
        public static TimeoutOperation Create(TimeSpan timeout, Action actionOnTimeout)
        {
            TimeoutOperation operation = new TimeoutOperation(timeout, actionOnTimeout);
            TapThread.Start(operation.wait, "Timeout");
            return operation;
        }

        /// <summary> Creates a timeout operation with the default timeout. </summary>
        /// <param name="actionOnTimeout"></param>
        /// <returns></returns>
        public static TimeoutOperation Create(Action actionOnTimeout)
        {
            return Create(DefaultTimeout, actionOnTimeout);
        }

        /// <summary>
        /// Cancel invoking the action after the timeout.
        /// </summary>
        public void Cancel()
        {
            try
            {
                lock (tokenSource)
                {
                    if (!isCompleted)
                        tokenSource.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {

            }
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}

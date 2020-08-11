//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    /// <summary> Represents a .NET type. </summary>
    public partial class TypeData : ITypeData
    {
        /// <summary> Creates a string value of this.</summary>
        public override string ToString() => Name;

        static ConditionalWeakTable<Type, TypeData> dict = new ConditionalWeakTable<Type, TypeData>();

        Type type;

        /// <summary>
        /// Gets the System.Type that this represents. Same as calling <see cref="Load()"/>.
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public Type Type => Load();

        // add assembly is not thread safe.
        static object loadTypeDictLock = new object();
        
        /// <summary> Creates a new TypeData object to represent a dotnet type. </summary>
        public static TypeData FromType(Type type)
        {
            checkCacheValidity();
            if (dict.TryGetValue(type, out var i))
                return i;
            TypeData td = null;
            lock (loadTypeDictLock)
            {
                var searcher = PluginManager.GetSearcher();

                searcher?.AllTypes.TryGetValue(type.FullName, out td);
                if (td == null && searcher != null)
                {
                    // This can occur for some types inside mscorlib such as System.Net.IPAddress.
                    try
                    {
                        if (type.Assembly != null && type.Assembly.IsDynamic == false &&
                            type.Assembly.Location != null)
                        {
                            searcher.AddAssembly(type.Assembly.Location, type.Assembly);
                            if (searcher.AllTypes.TryGetValue(type.FullName, out td))
                                return td;
                        }
                    }
                    catch
                    {
                    }

                    td = new TypeData(type);
                }
            }

            return dict.GetValue(type, x => td);
        }
        
        TypeData(Type type)
        {
            this.type = type;
            this.Name = type.FullName;
            postload();
        }

        bool postLoaded = false;
        object loadLock = new object();
        void postload()
        {
            if (postLoaded) return;
            lock (loadLock)
            {
                if (postLoaded) return;
                Load();
                if (type != typeof(string))
                {
                    var elementType = type.GetEnumerableElementType();
                    if (elementType != null)
                        this.elementType = FromType(elementType);
                }
                typecode = Type.GetTypeCode(type);
                hasFlags = this.HasAttribute<FlagsAttribute>();
                isValueType = type.IsValueType;
                postLoaded = true;
            }
        }

        bool isValueType;
        /// <summary> Cached IsValueType for speeding up annotation. </summary>
        internal bool IsValueType
        {
            get
            {
                postload();
                return isValueType;
            }
        }

        internal bool IsNumeric
        {
            get
            {
                postload();
                if (type.IsEnum)
                    return false;
                switch (typecode)
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
        }

        internal bool IsString
        {
            get
            {
                postload();
                return typecode == TypeCode.String;
            }
        }
        TypeCode typecode = TypeCode.Object;

        object[] attributes = null;
        /// <summary> 
        /// The attributes of this type. 
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IEnumerable<object> Attributes => attributes ?? (attributes = Load().GetAllCustomAttributes(false));

        /// <summary> The base type of this type. </summary>
        public ITypeData BaseType
        {
            get
            {
                if (BaseTypes != null)
                    return BaseTypes.First();
                return Load().BaseType == null ? null : TypeData.FromType(Load().BaseType);
            }
        }

        TypeData elementType;

        /// <summary> If this is a collection type, then this is the element type. Otherwise null. </summary>
        internal TypeData ElementType
        {
            get
            {
                postload();
                return elementType;
            }
        }

        bool? canCreateInstance;
        /// <summary> 
        /// returns true if an instance possibly can be created. 
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public bool CanCreateInstance {
            get
            {
                if (canCreateInstance.HasValue) return canCreateInstance.Value;
                if (_FailedLoad) return false;
                var type = Load();
                canCreateInstance = type.IsAbstract == false && type.IsInterface == false && type.ContainsGenericParameters == false && type.GetConstructor(Array.Empty<Type>()) != null;
                return canCreateInstance.Value;
            }       
        }

        string assemblyQualifiedName;
        internal string AssemblyQualifiedName
        {
            get
            {
                if (_FailedLoad) return "";
                return assemblyQualifiedName ?? (assemblyQualifiedName = Load().AssemblyQualifiedName);
            }
        }

        /// <summary>
        /// Creates a new object instance of this type.
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public object CreateInstance(object[] arguments)
        {
            return Activator.CreateInstance(Load(), arguments);
        }

        /// <summary>
        /// Gets a member by name.
        /// Causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IMemberData GetMember(string name)
        {
            var members = (IMemberData[]) GetMembers();
            foreach(var member in members)
            {
                if(member.Name == name)
                    return member;
            }
            
            // In some cases it could be useful to match in display name as well
            // we should consider removing this behavior for consistency and performance reasons..
            foreach(var member in members)
            {
                if(member.GetDisplayAttribute().GetFullName() == name)
                    return member;
            }
            return null;
        }
        
        IMemberData[] members;

        /// <summary>
        /// Gets all the members of this type. 
        /// Causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IEnumerable<IMemberData> GetMembers()
        {
            if (members == null)
            {
                var props = Load().GetPropertiesTap();
                List<IMemberData> m = new List<IMemberData>(props.Length);
                foreach (var mem in props)
                {
                    try
                    {
                        if (mem.GetMethod != null && mem.GetMethod.GetParameters().Length > 0)
                            continue;

                        if (mem.SetMethod != null && mem.SetMethod.GetParameters().Length != 1)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    m.Add(MemberData.Create(mem));
                }

                foreach (var mem in Load().GetMethodsTap())
                {
                    if (mem.GetAttribute<BrowsableAttribute>()?.Browsable ?? false)
                    {
                        var member = MemberData.Create(mem);
                        m.Add(member);
                    }
                }
                members = m.ToArray();
            }
            return members;
        }

        bool hasFlags;
        internal bool HasFlags()
        {
            postload();
            return hasFlags;
        }

        /// <summary> Compares two TypeDatas by comparing their inner Type instances. </summary>
        /// <param name="obj"> Should be a TypeData</param>
        /// <returns>true if the two Type properties are equals.</returns>
        public override bool Equals(object obj)
        {
            if (obj is TypeData td && td.type != null && type != null )
                return td.type == type;
            return ReferenceEquals(this, obj);
        }

        /// <summary> Calculates the hash code based on the .NET Type instance. </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var asm = Assembly?.GetHashCode() ?? 0;
            return (asm + 586093897) * 759429795 + 
                   (Name.GetHashCode() + 836431542) * 678129773;
        }
    }

    /// <summary>
    /// Represents the members of C#/dotnet types.
    /// </summary>
    public class MemberData : IMemberData
    {
        
        struct MemberName
        {
            public string Name { get; set; }
            public Type DeclaringType { get; set; }
        }
        static ConcurrentDictionary<MemberName, MemberData> dict
            = new ConcurrentDictionary<MemberName, MemberData>();
        internal static void InvalidateCache()
        {
            dict.Clear();
        }
        /// <summary>
        /// Creates a new MemberData for a member of a C#/dotnet type.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        static public MemberData Create(MemberInfo info)
        {
            lock(dict)
                return dict.GetOrAdd(new MemberName { Name = info.Name, DeclaringType = info.DeclaringType }, x => new MemberData(x.Name, TypeData.FromType(x.DeclaringType)));
        }

        /// <summary> The System.Reflection.MemberInfo this represents. </summary>
        public readonly MemberInfo Member;

        private MemberData(string name, TypeData declaringType) : this(declaringType.Type.GetMember(name)[0], declaringType)
        { }

        private MemberData(MemberInfo info, TypeData declaringType)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            this.Member = info;
            this.DeclaringType = declaringType;
            
        }
        IEnumerable<object> attributes = null;

        /// <summary> The attributes of this member. </summary>
        public IEnumerable<object> Attributes => attributes ?? (attributes = Member.GetCustomAttributes());

        static Type createDelegateType(MethodInfo method)
        {
            var parameters = method.GetParameters().Select(x => x.ParameterType);
            if (method.ReturnType != typeof(void))
                return Expression.GetFuncType(parameters.Append(method.ReturnType).ToArray());
            return Expression.GetActionType(parameters.ToArray());
        }
        
        static Func<object, object> buildGetter(PropertyInfo propertyInfo)
        {
            var instance = Expression.Parameter(typeof(object), "i");
            UnaryExpression convert1;
            if(propertyInfo.DeclaringType.IsValueType)
                convert1 = Expression.Convert(instance, propertyInfo.DeclaringType);
            else
                convert1 = Expression.TypeAs(instance, propertyInfo.DeclaringType);
            var property = Expression.Property(convert1, propertyInfo);
            var convert = Expression.TypeAs(property, typeof(object));
            
            var lambda = Expression.Lambda<Func<object, object>>(convert, instance);
            var action = lambda.Compile();
            return action;
        }

        Func<object, object> propertyGetter = null; 

        /// <summary> Gets the value of this member.</summary> 
        /// <param name="owner"></param>
        /// <returns></returns>
        public object GetValue(object owner)
        {
            switch (Member)
            {
                case PropertyInfo Property:
                    if (propertyGetter == null)
                        propertyGetter = buildGetter(Property);
                    //Building a lambda expression is an order of magnitude faster than Property.GetValue.
                    return propertyGetter(owner);
                    
                case FieldInfo Field: return Field.GetValue(owner);
                case MethodInfo Method: return Delegate.CreateDelegate(createDelegateType(Method), owner, Method, true);
                default: throw new InvalidOperationException("Unsupported member type: " + Member);
            }
        }

        /// <summary>
        /// Sets the value of this member on an object.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="value"></param>
        public void SetValue(object owner, object value)
        {
            switch (Member)
            {
                case PropertyInfo Property:
                    Property.SetValue(owner, value);
                    break;
                case FieldInfo Field:
                    Field.SetValue(owner, value);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported member type: " + Member);
            }
        }

        /// <summary> The name of this member. </summary>
        public string Name => Member.Name;

        /// <summary>
        /// The declaring type of this member.
        /// </summary>
        public ITypeData DeclaringType { get; }

        /// <summary> Gets if the member is writable. </summary>
        public bool Writable
        {
            get
            {
                switch (Member)
                {
                    case PropertyInfo Property: return Property.CanWrite && Property.GetSetMethod() != null;
                    case FieldInfo Field: return Field.IsInitOnly == false;
                    default: return false;
                }
            }
        }

        bool? readable; 
        
        /// <summary> Gets if the member is readable.  </summary>
        public bool Readable
        {
            get
            {
                switch (Member)
                {
                    case PropertyInfo Property: return (readable ?? (readable = Property.CanRead && Property.GetGetMethod() != null)).Value;
                    case FieldInfo _: return true;
                    default: return false;
                }
            }
        }

        ITypeData typeDescriptor;

        /// <summary> The type descriptor for the object that this member can hold. </summary>
        public ITypeData TypeDescriptor => typeDescriptor ?? (typeDescriptor = getTypeDescriptor());
        
        ITypeData getTypeDescriptor()
        {
            switch (Member)
            {
                case PropertyInfo Property: return TypeData.FromType(Property.PropertyType);
                case FieldInfo Field: return TypeData.FromType(Field.FieldType);
                case MethodInfo Method: return TypeData.FromType(createDelegateType(Method));
                default: throw new InvalidOperationException("Unsupported member type: " + Member);
            }   
        }

        /// <summary> Gets a string representation of this CSharpType. </summary>
        public override string ToString() => $"[{Name}]";
    }
}

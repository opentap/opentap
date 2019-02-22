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
    /// <summary> Type info for C# objects. </summary>
    public class CSharpTypeInfo : ITypeInfo
    {
        /// <summary> Creates a string value of this.</summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"C#: {Type.FullName}";
        }
        static ConditionalWeakTable<Type, CSharpTypeInfo> dict
    = new ConditionalWeakTable<Type, CSharpTypeInfo>();

        Type type;
        /// <summary>
        /// The type this C# type info represents.
        /// </summary>
        public Type Type => type;

        /// <summary> Creates a new CSharpTypeInfo. </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static CSharpTypeInfo Create(Type type)
        {
            return dict.GetValue(type, x => new CSharpTypeInfo(x));
        }

        CSharpTypeInfo(Type type)
        {
            this.type = type;
        }


        /// <summary> The name of this type. </summary>
        public string Name => Type.FullName;

        /// <summary> The attributes of this type. </summary>
        public IEnumerable<object> Attributes => Type.GetAllCustomAttributes();

        /// <summary> The base type of this type. </summary>
        public ITypeInfo BaseType => CSharpTypeInfo.Create(Type.BaseType);

        /// <summary> returns true if an instance possibly can be created. </summary>
        public bool CanCreateInstance => Type.IsAbstract == false && Type.IsInterface == false && Type.GetConstructor(Array.Empty<Type>()) != null;

        /// <summary>
        /// Creates a new object instance of this type.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public object CreateInstance(object[] arguments)
        {
            return Activator.CreateInstance(Type, arguments);
        }

        /// <summary>
        /// Gets a member by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IMemberInfo GetMember(string name)
        {
            return GetMembers().FirstOrDefault(x => x.Name == name);
        }
        IEnumerable<IMemberInfo> members = null;

        /// <summary>
        /// Get all the members of this type. 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMemberInfo> GetMembers()
        {
            if (members == null)
            {
                List<IMemberInfo> m = new List<IMemberInfo>();
                foreach (var prop in Type.GetPropertiesTap())
                {
                    m.Add(CSharpMemberInfo.Create(prop));
                }

                foreach (var mem in Type.GetMethodsTap())
                {
                    if (mem.GetAttribute<BrowsableAttribute>()?.Browsable ?? false)
                        m.Add(CSharpMemberInfo.Create(mem));
                }
                members = m.ToArray();
            }
            return members;
        }
    }

    /// <summary>
    /// Represents the members of C# types.
    /// </summary>
    public class CSharpMemberInfo : IMemberInfo
    {
        
        struct MemberName
        {
            public string Name { get; set; }
            public Type DeclaringType { get; set; }
        }
        static ConcurrentDictionary<MemberName, CSharpMemberInfo> dict
            = new ConcurrentDictionary<MemberName, CSharpMemberInfo>();

        /// <summary>
        /// Creats a new CSharpMemberInfo.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        static public CSharpMemberInfo Create(MemberInfo info)
        {
            lock(dict)
                return dict.GetOrAdd(new MemberName { Name = info.Name, DeclaringType = info.DeclaringType }, x => new CSharpMemberInfo(x.Name, CSharpTypeInfo.Create(x.DeclaringType)));
        }

        /// <summary>
        /// The member this represents.
        /// </summary>
        public readonly MemberInfo Member;

        private CSharpMemberInfo(string name, CSharpTypeInfo declaringType) : this(declaringType.Type.GetMember(name)[0], declaringType)
        {

        }
        private CSharpMemberInfo(MemberInfo info, CSharpTypeInfo declaringType)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            this.Member = info;
            this.DeclaringType = declaringType;

        }
        IEnumerable<object> attributes = null;
        /// <summary>
        /// The attributes of this member.
        /// </summary>
        public IEnumerable<object> Attributes => attributes ?? (attributes = Member.GetCustomAttributes());

        

        static Type createDelegateType(MethodInfo method)
        {
            var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
            if (method.ReturnType == typeof(void))
            {
                return Expression.GetActionType(parameters);
            }
            else
            {
                return Expression.GetFuncType(parameters.Append(method.ReturnType).ToArray());
            }
        }

        /// <summary> Gets the value of this member.</summary> 
        /// <param name="owner"></param>
        /// <returns></returns>
        public object GetValue(object owner)
        {
            switch (Member)
            {
                case PropertyInfo Property: return Property.GetValue(owner);
                case FieldInfo Field: return Field.GetValue(owner);
                case MethodInfo Method: return Delegate.CreateDelegate(createDelegateType(Method), owner, Method, false);
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
        public ITypeInfo DeclaringType { get; private set; }

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

        /// <summary> Gets if the member is readable.  </summary>
        public bool Readable
        {
            get
            {
                switch (Member)
                {
                    case PropertyInfo Property: return Property.CanRead && Property.GetGetMethod() != null;
                    case FieldInfo Field: return true;
                    default: return false;
                }
            }
        }

        /// <summary>
        /// The type descriptor for the object that this member can hold.
        /// </summary>
        public ITypeInfo TypeDescriptor
        {
            get
            {
                switch (Member)
                {
                    case PropertyInfo Property: return CSharpTypeInfo.Create(Property.PropertyType);
                    case FieldInfo Field: return CSharpTypeInfo.Create(Field.FieldType);
                    case MethodInfo Method: return CSharpTypeInfo.Create(createDelegateType(Method));
                    default: throw new InvalidOperationException("Unsupported member type: " + Member);
                }
            }
        }

        /// <summary> Gets a string representation of this CSharpTYpe. </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"[{Name}]";
        }
    }

    /// <summary> Type info provider for C# types. </summary>
    public class CSharpTypeInfoProvider : ITypeInfoProvider
    {
        /// <summary> The priority of this type info provider.  </summary>
        public double Priority => 0;
        /// <summary> Gets the C# type info for a string.  </summary>
        /// <param name="res"></param>
        /// <param name="identifier"></param>
        public void GetTypeInfo(TypeInfoResolver res, string identifier)
        {
            
            var type = PluginManager.LocateType(identifier);
            if (type != null)
            {
                res.Stop(CSharpTypeInfo.Create(type));
            }
        }

        /// <summary> Gets the C# type info for an object. </summary>
        /// <param name="res"></param>
        /// <param name="obj"></param>
        public void GetTypeInfo(TypeInfoResolver res, object obj)
        {
            var type = obj.GetType();
            res.Stop(CSharpTypeInfo.Create(type));
        }
    }

}

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
                    if(this.Readable == false) throw new Exception("Cannot get the value of a read-only property.");
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
                    case MethodInfo _: return true;
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

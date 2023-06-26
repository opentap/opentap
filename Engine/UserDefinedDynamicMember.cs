using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary> Class describing a user defined member. </summary>
    public class UserDefinedDynamicMember : DynamicMember
    {
        IEnumerable<object> attributes;
        /// <summary> User defined attributes. </summary>
        [XmlIgnore]
        public override IEnumerable<object> Attributes
        {
            get => attributes;
            set => attributes = value;
        }

        /// <summary> Return user defined members for an object.  </summary>
        public static UserDefinedDynamicMember[] GetUserDefinedMembers(object obj) => 
            TypeData.GetTypeData(obj).GetMembers().OfType<UserDefinedDynamicMember>().ToArray();

        /// <summary> Compare this to another UserDefinedDynamicMember. </summary>
        public override bool Equals(object obj)
        {
            if (obj is UserDefinedDynamicMember other)
                return other.Name == Name 
                       && Equals(other.TypeDescriptor, TypeDescriptor) 
                       && Equals(other.DeclaringType, DeclaringType);
            return false;
        }

        /// <summary> Calculate a hash code. </summary>
        public override int GetHashCode()
        {
            var a = Name.GetHashCode() * 37219321;
            var b = TypeDescriptor?.GetHashCode() ?? 0 + 7565433;
            var c = DeclaringType?.GetHashCode() ?? 0 + 180374830;
            return ((a * 732013 + b) * 3073212 + c * 32103212);
        }
    }
}
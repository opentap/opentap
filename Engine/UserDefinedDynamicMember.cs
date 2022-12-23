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
        /// <summary> User defined attributes. </summary>
        [XmlIgnore]
        public override IEnumerable<object> Attributes
        {
            get
            {
                if (!string.IsNullOrEmpty(Group) || !string.IsNullOrEmpty(DisplayName) ||
                    !string.IsNullOrEmpty(Description) || Math.Abs(Order -  -10000.0) >= 0.01)
                {
                    var group  = Group ?? "";
                    string displayName = DisplayName;
                    if (string.IsNullOrEmpty(displayName))
                        displayName = Name;
                    var description = Description ?? "";
                    yield return new DisplayAttribute(displayName, description, group, Order);
                }

                if (Output)
                    yield return new OutputAttribute();
            }
            set{}
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

        /// <summary> Name shown to the user. </summary>
        [DefaultValue(null)]
        public string DisplayName { get; set; }
        /// <summary> Description may be null. </summary>
        [DefaultValue(null)]
        public string Description { get; set; }
        
        /// <summary> Group may be null. </summary>
        [DefaultValue(null)]
        public string Group { get; set; }

        /// <summary> Order, default -10000. </summary>
        [DefaultValue(-10000.0)] public double Order { get; set; } = -10000.0;
        
        /// <summary> Whether this property is an output. </summary>
        [DefaultValue(false)]
        public bool Output { get; set; }
    }
}
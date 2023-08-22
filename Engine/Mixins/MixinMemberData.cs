using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    /// <summary> Mixin member data </summary>
    public class MixinMemberData : IMemberData
    {
        /// <summary> Attributes </summary>
        public IEnumerable<object> Attributes { get; set; } = Array.Empty<object>();
        /// <summary> Name of the member </summary>
        public string Name { get; set; }
        /// <summary> Declaring type. This should be the type of the target object. </summary>
        public ITypeData DeclaringType { get; set; }
        /// <summary> Describes the value of the member. </summary>
        public ITypeData TypeDescriptor { get; set; }
        /// <summary> If the member is writable. </summary>
        public bool Writable { get; set; }
        /// <summary> If the member is readable. This should generally be true. </summary>
        public bool Readable { get; set; }

        readonly ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();
        
        readonly Func<object> make;


        /// <summary> Sets the value of the member. </summary>
        public void SetValue(object owner, object value)
        {
            dict.Remove(owner);
            dict.GetValue(owner, (owner) => value);
        }
        /// <summary> Gets the value of the member. </summary>
        public object GetValue(object owner)
        {
            if (dict.TryGetValue(owner, out var value))
                return value;
            return DefaultValue;
        }

        /// <summary> The default value of the member. </summary>
        public object DefaultValue { get; set; }
        /// <summary> The object which was used to construct this. </summary>
        public IMixinBuilder Source { get; }

        internal object NewInstance()
        {
            if (DefaultValue != null) return DefaultValue;
            if (TypeDescriptor.DescendsTo(typeof(string)))
                return null;
            if (make != null)
                return make();
            return TypeDescriptor.CreateInstance();
        }

        /// <summary> Creates a new instance of mixin member data. </summary>
        public MixinMemberData(IMixinBuilder source, Func<object> make = null)
        {
            this.make = make;
            Writable = true;
            Readable = true;
            Source = source;
        }
    }
}

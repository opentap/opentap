using System;
namespace OpenTap
{
    /// <summary> This attribute marks a mixin builder with the types that it supports. </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MixinBuilderAttribute : Attribute
    {
        internal Type[] Types { get; }
        
        /// <summary> Creates a new instance of MixinBuilderAttribute. </summary>
        /// <param name="types">The types that the mixin builder supports.</param>
        public MixinBuilderAttribute(params Type[] types) => Types = types;
    }
}

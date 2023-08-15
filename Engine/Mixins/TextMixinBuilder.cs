using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    [Display("Text")]
    [MixinBuilder(typeof(object))]
    class TextMixinBuilder : IMixinBuilder
    {
        public string Name
        {
            get;
            set;
        } = "Text";
        
        [Flags]
        public enum Option
        {
            [Display("Result", "Set this if the number should be a result.")]
            Result = 1,
            [Display("Output", "Set this if the number should be an output.")]
            Output = 2
        }
        
        [DefaultValue(0)]
        [Display("Options", "Select options for this mixin.")]
        public Option Options { get; set; }

        
        IEnumerable<Attribute> GetAttributes()
        {
            if (Options.HasFlag(Option.Result))
                yield return new ResultAttribute();
            if(Options.HasFlag(Option.Output))
                yield return new OutputAttribute();
        }

        public void Initialize(ITypeData targetType)
        {
            
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            
            return new MixinMemberData(this)
            {
                Name = Name,
                TypeDescriptor = TypeData.FromType(typeof(string)),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Attributes = GetAttributes().ToArray()
            };

        }
        public IMixinBuilder Clone() => (IMixinBuilder)this.MemberwiseClone();
    }
}
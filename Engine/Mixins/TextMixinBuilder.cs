using System;
using System.Collections.Generic;
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

        
        IEnumerable<Attribute> GetAttributes()
        {
            yield break;
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
    }
}
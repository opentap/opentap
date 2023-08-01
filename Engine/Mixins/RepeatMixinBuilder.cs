using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    class RepeatMixin : IMixin
    {
        public int Count { get; set; }
    }
    
    class RepeatMixinBuilder : IMixinBuilder
    {
        IEnumerable<Attribute> GetAttributes()
        {
            yield return new EmbedPropertiesAttribute();
        }

        public MixinMember ToDynamicMember()
        {
            return new MixinMember(this)
            {
                TypeDescriptor = TypeData.FromType(typeof(RepeatMixin)),
                Attributes = GetAttributes().ToArray(),
                Writable = true,
                Readable = true,
                DeclaringType = TypeData.FromType(GetType())
            };
        }
    }
}
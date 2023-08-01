using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
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
        
        public MixinMember ToDynamicMember()
        {
            
            return new MixinMember(this)
            {
                Name = Name,
                TypeDescriptor = TypeData.FromType(typeof(string)),
                Writable = true,
                Readable = true,
                DeclaringType = TypeData.FromType(GetType()),
                Attributes = GetAttributes().ToArray()
            };

        }
    }
}
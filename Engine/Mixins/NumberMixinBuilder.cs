using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    public class MixinMember : DynamicMember
    {
        public IMixinBuilder Source { get; }

        public MixinMember(IMixinBuilder source)
        {
            Source = source;
        }
    }
    
    class NumberMixinBuilder : IMixinBuilder
    {
        public string Name
        {
            get;
            set;
        } = "Number";
        
        public string Unit { get; set; }
        public bool Result { get; set; }
        public bool Output { get; set; }
        
        public IMixin CreateInstance()
        {
            return new NumberMixin();
        }

        IEnumerable<Attribute> GetAttributes()
        {
            if (!string.IsNullOrWhiteSpace(Unit))
                yield return new UnitAttribute(Unit);
            if (Result)
                yield return new ResultAttribute();
            if (Output)
                yield return new OutputAttribute();

        }
        
        public MixinMember ToDynamicMember()
        {
            
            return new MixinMember(this)
            {
                Name = Name,
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Writable = true,
                Readable = true,
                DeclaringType = TypeData.FromType(GetType()),
                Attributes = GetAttributes().ToArray()
            };

        }
    }
}
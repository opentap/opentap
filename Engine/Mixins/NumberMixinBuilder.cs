using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{

    [Display("Number", "Adds a number to an object.")]
    [MixinBuilder(typeof(object))]
    class NumberMixinBuilder : ValidatingObject, IMixinBuilder
    {
        
        public string Name { get; set; } = "Number";
        
        [Flags]
        public enum Option
        {
            [Display("Result", "Set this if the number should be a result.")]
            Result = 1,
            [Display("Output", "Set this if the number should be an output.")]
            Output = 2,
            [Display("Unit", "Set this if the number should have a unit. e.g 's'")]
            Unit = 4
        }
        
        [DefaultValue(0)]
        [Display("Options", "Select options for this mixin.")]
        public Option Options { get; set; }

        public bool ShowUnit => Options.HasFlag(Option.Unit);
        
        [EnabledIf(nameof(ShowUnit), true, HideIfDisabled = true)]
        public string Unit { get; set; } = "s";
        public bool Result => Options.HasFlag(Option.Result);
        
        public bool Output => Options.HasFlag(Option.Output);
        
        IEnumerable<Attribute> GetAttributes()
        {
            if (Options.HasFlag(Option.Unit))
                yield return new UnitAttribute(Unit);
            if (Result)
                yield return new ResultAttribute();
            if (Output)
                yield return new OutputAttribute();
        }

        ITypeData targetType;
        bool NameOK
        {
            get
            {
                if (targetType == null) return true;
                var isSame = targetType.GetMember(Name) is MixinMemberData mb && mb.Source == this;
                if (isSame) return true;
                bool anyCollides = targetType.GetMembers().Any(x =>
                {
                    if (x is MixinMemberData mb && mb.Source == this)
                    {
                        return false;
                    } 
                    return x.GetDisplayAttribute().Name == Name;
                });
                return !anyCollides;
            }   
        } 
        public void Initialize(ITypeData targetType)
        {
            Rules.Add(() => NameOK, "This name collides with one already defined.", "OpenTap.NumberMixinBuilder." + nameof(Name));
            this.targetType = targetType;
            if (Name == "Number")
            {
                int it = 1;
                while(!NameOK)
                    Name = $"Number{++it}";
            }
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            return new MixinMemberData(this)
            {
                Name = Name,
                TypeDescriptor = TypeData.FromType(typeof(double)),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Attributes = GetAttributes().ToArray()
            };
        }
        public IMixinBuilder Clone()
        {
            return (IMixinBuilder)this.MemberwiseClone();
        }
    }
}
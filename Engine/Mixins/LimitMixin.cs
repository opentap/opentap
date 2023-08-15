using System;
using System.Collections.Generic;
using System.Linq;
namespace OpenTap
{
    [Display("Limit Mixin")]
    class LimitMixin : ValidatingObject, ITestStepPostRunMixin
    {
        [Display("Upper Limit")]
        public double UpperLimit { get; set; }
        
        [Display("Lower Limit")]
        public double LowerLimit { get; set; }
        
        public bool LowerLimitActive { get; }
        
        public bool UpperLimitActive { get; }
        
        
        readonly string targetMemberName;

        public void OnPostRun(TestStepPostRunEventArgs eventArgs)
        {
            var targetMember = TypeData.GetTypeData(eventArgs.TestStep).GetMember(targetMemberName);
            if (targetMember == null)
            {
                eventArgs.TestStep.UpgradeVerdict(Verdict.Error);
                return;
            }
            var conv = targetMember.GetValue(eventArgs.TestStep) as IConvertible;
            var value = conv.ToDouble(null);
            if (value > UpperLimit)
            {
                eventArgs.TestStep.UpgradeVerdict(Verdict.Fail);
            }else if (value < LowerLimit)
            {
                eventArgs.TestStep.UpgradeVerdict(Verdict.Fail);
            }
            else
            {
                eventArgs.TestStep.UpgradeVerdict(Verdict.Pass);
            }
        }

        public LimitMixin(bool lowerLimitActive, bool upperLimitActive, string targetMemberName, double lowerLimit, double upperLimit)
        {
            LowerLimitActive = lowerLimitActive;
            UpperLimitActive = upperLimitActive;
            this.targetMemberName = targetMemberName;
            UpperLimit = upperLimit;
            LowerLimit = lowerLimit;
        }
    }
    
    [Display("Limit", "This mixin adds a limit check to a test step.")]
    [MixinBuilder(typeof(ITestStep))]
    class LimitMixinBuilder : IMixinBuilder
    {
        IEnumerable<Attribute> GetAttributes(string targetName)
        {
            yield return new EmbedPropertiesAttribute();
            yield return new DisplayAttribute($"Limits for {targetName}", Order: 19999);
        }

        [Flags]
        public enum Option
        {
            [Display("Lower Limit")]
            LowerLimit = 1,
            [Display("Upper Limit")]
            UpperLimit = 2
        }

        public Option Options { get; set; } = Option.LowerLimit | Option.UpperLimit;

        [AvailableValues(nameof(Members))]
        public string Value { get; set; }
        
        [Display("Upper Limit", Order: 1)]
        [EnabledIf(nameof(UpperLimitVisible), true, HideIfDisabled = true)]
        public double UpperLimit { get; set; }
        
        [Display("Lower Limit", Order: 1.1)]
        [EnabledIf(nameof(LowerLimitVisible), true, HideIfDisabled = true)]
        public double LowerLimit { get; set; }

        
        public bool LowerLimitVisible => Options.HasFlag(Option.LowerLimit);
        public bool UpperLimitVisible => Options.HasFlag(Option.UpperLimit);
        public string[] Members { get; private set; }

        public void Initialize(ITypeData targetType)
        {
            Members = targetType.GetMembers().Where(x => x.TypeDescriptor.IsNumeric()).Select(x => x.Name).ToArray();
            if (string.IsNullOrWhiteSpace(Value))
                Value = Members.FirstOrDefault() ?? "";
        }
        
        
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            
            return new MixinMemberData(this, () => new LimitMixin(Options.HasFlag(Option.LowerLimit), Options.HasFlag(Option.UpperLimit), Value, LowerLimit, UpperLimit))
            {
                TypeDescriptor = TypeData.FromType(typeof(LimitMixin)),
                Attributes = GetAttributes(Value).ToArray(),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Name = "LimitMixin"
            };
        }
        public IMixinBuilder Clone()
        {
            return (IMixinBuilder)this.MemberwiseClone();
        }
    }
}

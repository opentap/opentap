using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    [Display("Repeat Mixin")]
    class RepeatMixin : ValidatingObject, ITestStepPostRunMixin, ITestStepPreRunMixin
    {
        [Display("Count", "The number of times to repeat running the test plan")]
        public int Count { get; set; } = 1;
        
        public enum RepeatBehavior
        {
            [Display("Fixed Count", "Repeat iteration a fixed number of times.")]
            FixedCount,
            [Display("While", "Repeat while the specified condition is met.")]
            While,
            [Display("Until", "Repeat until the specified condition is met.")]
            Until
        }
        
        [Display("Repeat")]
        public RepeatBehavior Behavior { get; set; }
        
        [Display("Verdict Is")]
        public Verdict VerdictIs { get; set; }
        

        public RepeatMixin()
        {
            Rules.Add(() => Count >= 0, "Count must be a positive number.", nameof(Count));
        }
        
        int? it; 
        public void OnPostRun(TestStepPostRunEventArgs step)
        {
            if (it == null)
                it = 1;
            if (it >= Count)
                it = null;
            else
            {
                step.TestStep.StepRun.SuggestedNextStep = step.TestStep.Id;
                it += 1;
            }
        }
            
        public void OnPreRun(TestStepPreRunEventArgs step)
        {
            if (Count == 0) step.SkipStep = true;
        }
    }
    
    [Display("Repeat")]
    [MixinBuilder(typeof(ITestStep))]
    class RepeatMixinBuilder : IMixinBuilder
    {
        IEnumerable<Attribute> GetAttributes()
        {
            yield return new EmbedPropertiesAttribute();
            yield return new DisplayAttribute("Repeat", Order: 19999);
        }

        public void Initialize(ITypeData targetType)
        {
            
        }
        public MixinMemberData ToDynamicMember(ITypeData targetType)
        {
            return new MixinMemberData(this)
            {
                TypeDescriptor = TypeData.FromType(typeof(RepeatMixin)),
                Attributes = GetAttributes().ToArray(),
                Writable = true,
                Readable = true,
                DeclaringType = targetType,
                Name = "RepeatMixin"
            };
        }
        public IMixinBuilder Clone()
        {
            return (IMixinBuilder)this.MemberwiseClone();
        }
    }

}
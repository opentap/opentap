using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap
{
    
    class VerdictBehaviorAnnotation : IMembersAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
    {
        class PseudoVerdictBehavior : Enabled<PseudoVerdictBehavior.Values>
        {
            [Flags]
            public enum Values
            {
                /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
                [Display("Break on Error", "If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnError = 2,
                /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
                [Display("Break on Fail", "If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnFail = 4,
                /// <summary> If a step completes with verdict 'Inclusive' the step should break execution.</summary>
                [Display("Break on Inconclusive", "If a step completes with verdict 'inconclusive', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnInconclusive = 8,
                /// <summary> If a step completes with verdict 'Error', the test step should be re-run. </summary>
                [Display("Retry on Error", "If a step completes with verdict 'Error', the test step should be re-run.")]
                RetryOnError = 16,
                /// <summary> If a step completes with verdict 'Fail', the test step should be re-run. </summary>
                [Display("Retry on Fail", "If a step completes with verdict 'Fail', the test step should be re-run.")]
                RetryOnFail = 32,
                /// <summary> If a step completes with verdict 'Inclusive' the step should retry a number of times.</summary>
                [Display("Retry on Inconclusive", "If a step completes with verdict 'Inconclusive', the test step should be re-run.")]
                RetryOnInconclusive = 64
            }
            public override bool IsEnabled
            {
                get => Behavior.HasFlag(TestStepVerdictBehavior.Inherit) == false;
                set
                {
                    Behavior = Behavior.SetFlag(TestStepVerdictBehavior.Inherit, !value);
                }
            }

            public override Values Value
            {
                get => (Values)(int)Behavior.SetFlag(TestStepVerdictBehavior.Inherit, false);
                set => Behavior = (TestStepVerdictBehavior) (int) value | ((!IsEnabled) ? TestStepVerdictBehavior.Inherit : 0);
            }

            public TestStepVerdictBehavior Behavior;
        }


        class PseudoVerdictBehaviorString : IStringReadOnlyValueAnnotation, IValueDescriptionAnnotation
        {       
            
            static TestStepVerdictBehavior[] behaviors = Enum.GetValues(typeof(TestStepVerdictBehavior)).OfType<TestStepVerdictBehavior>().ToArray();

            static TestStepVerdictBehavior[] breakBehaviors =
                behaviors.Where(x => x.ToString().Contains("Break")).ToArray();
            static TestStepVerdictBehavior[] retryBehaviors = behaviors.Where(x => x.ToString().Contains("Retry")).ToArray(); 
            static string getEnumString(TestStepVerdictBehavior value)
            {
                if (value == 0) return "None";
                var sb = new StringBuilder();
                var breakFlags = breakBehaviors.Where(x => value.HasFlag(x));
                if (breakFlags.Any())
                {
                    sb.AppendFormat("Break on {0}",breakFlags.First().ToString().Substring("BreakOn".Length));
                    var breakFlags2 = breakFlags.Skip(1);
                    foreach (var x in breakFlags2)
                    {
                        sb.AppendFormat(" or {0}", x.ToString().Substring("BreakOn".Length));
                    }
                }

                var retryFlags = retryBehaviors.Where(x => value.HasFlag(x));
                if (retryFlags.Any())
                {
                    if (breakFlags.Any())
                    {
                        sb.Append(" and retry");
                    }
                    else
                    {
                        sb.Append("Retry");
                    }
                    
                    sb.AppendFormat(" on {0}",retryFlags.First().ToString().Substring("RetryOn".Length));
                    retryFlags = retryFlags.Skip(1);
                    foreach (var x in retryFlags)
                    {
                        sb.AppendFormat(" or {0}", x.ToString().Substring("RetryOn".Length));
                    }
                }

                return sb.ToString();
            }

            static TestStepVerdictBehavior convertAbortCondition(EngineSettings.AbortTestPlanType abortType)
            {
                return ((abortType.HasFlag(EngineSettings.AbortTestPlanType.Step_Fail)) ? TestStepVerdictBehavior.BreakOnFail : 0) 
                       | (abortType.HasFlag(EngineSettings.AbortTestPlanType.Step_Error) ? TestStepVerdictBehavior.BreakOnError : 0);
            }

            public enum BehaviorSource
            {
                Self,
                Parent,
                Engine
            }

            
            public static (TestStepVerdictBehavior, BehaviorSource) getInheritedVerdict(ITestStepParent _step)
            {
                ITestStepParent src = _step;
                src = src.Parent;
                while (src is ITestStep step)
                {
                    var cond = AbortCondition.GetAbortCondition(step);
                    if (cond.HasFlag(TestStepVerdictBehavior.Inherit) == false)
                    {
                        cond = cond.SetFlag(TestStepVerdictBehavior.RetryOnError, false)
                            .SetFlag(TestStepVerdictBehavior.RetryOnFail, false)
                            .SetFlag(TestStepVerdictBehavior.RetryOnInconclusive, false);
                        return (cond, BehaviorSource.Parent);
                    }

                    src = step.Parent as ITestStep;
                }

                return (convertAbortCondition(EngineSettings.Current.AbortTestPlan), BehaviorSource.Engine);
            }

            public (TestStepVerdictBehavior, BehaviorSource) GetBehavior()
            {
                var behavior = annotation.behavior;
                if (behavior.IsEnabled == false &&  annotation.annotation.Source is ITestStepParent step)
                    return getInheritedVerdict(step);
                    
                var valuemem = (TestStepVerdictBehavior) valueAnnotation.Get<IObjectValueAnnotation>().Value;
                return (valuemem, BehaviorSource.Self);
            }

            public string Value
            {
                get
                {
                    var (behavior, _) = GetBehavior();
                    return getEnumString(behavior);
                }
            }
            
            public VerdictBehaviorAnnotation annotation;
            public AnnotationCollection valueAnnotation;

            public string Describe()
            {
                var (behavior, kind) = GetBehavior();
                var str = getEnumString(behavior);
                switch (kind)
                {
                    case BehaviorSource.Engine: return $"{str} (inherited from engine settings)";
                    case BehaviorSource.Parent: return $"{str} (inherited from parent)";
                    default: return str;
                }
            }
        }

        PseudoVerdictBehaviorString str;
        AnnotationCollection createSubAnnotation()
        {
            var sub = annotation.AnnotateSub(TypeData.GetTypeData(behavior), behavior);
            var fst = sub.Get<IMembersAnnotation>().Members
                .FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name.Contains("IsEnabled") == false);
            fst.Add(str = new PseudoVerdictBehaviorString(){annotation = this, valueAnnotation = fst});
            if (str != null && behavior.IsEnabled == false)
            {
                behavior.Behavior = str.GetBehavior().Item1.SetFlag(TestStepVerdictBehavior.Inherit, true);
            }
            return sub;
        }

        AnnotationCollection subannotations;
        public IEnumerable<AnnotationCollection> Members => (subannotations ?? (subannotations = createSubAnnotation())).Get<IMembersAnnotation>().Members.Reverse();     

        PseudoVerdictBehavior behavior = new PseudoVerdictBehavior();
        AnnotationCollection annotation;
        public void Read(object source)
        {
            subannotations?.Read();
            behavior.Behavior = ((TestStepVerdictBehavior)(annotation.Get<IObjectValueAnnotation>().Value ?? (TestStepVerdictBehavior) 0));
            if (str != null && behavior.IsEnabled == false)
            {
                behavior.Behavior = str.GetBehavior().Item1.SetFlag(TestStepVerdictBehavior.Inherit, true);
            }
            
        }

        public void Write(object source)
        {
            subannotations?.Write();
            if (str != null && behavior.IsEnabled == false)
            {
                behavior.Behavior = str.GetBehavior().Item1.SetFlag(TestStepVerdictBehavior.Inherit, true);
            }
            
            annotation.Get<IObjectValueAnnotation>().Value = behavior.Behavior;
            
        }

        public VerdictBehaviorAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

        public string Value
        {
            get
            {
                if (subannotations == null)
                    subannotations = createSubAnnotation();
                return str?.Value;
            }
        }
    }
}
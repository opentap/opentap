using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap
{
    
    class BreakConditionsAnnotation : IMembersAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
    {
        class PseudoBreakConditions : Enabled<PseudoBreakConditions.Values>
        {
            [Flags]
            public enum Values
            {
                /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
                [Display("On Error", "If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnError = 2,
                /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
                [Display("On Fail", "If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnFail = 4,
                /// <summary> If a step completes with verdict 'Inclusive' the step should break execution.</summary>
                [Display("On Inconclusive", "If a step completes with verdict 'inconclusive', stop execution of any subsequent steps at this level, and return control to the parent step.")]
                BreakOnInconclusive = 8,
            }
            public override bool IsEnabled
            {
                get => Conditions.HasFlag(BreakCondition.Inherit) == false;
                set
                {
                    Conditions = Conditions.SetFlag(BreakCondition.Inherit, !value);
                }
            }

            public override Values Value
            {
                get => (Values)(int)Conditions.SetFlag(BreakCondition.Inherit, false);
                set => Conditions = (BreakCondition) (int) value | ((!IsEnabled) ? BreakCondition.Inherit : 0);
            }

            public BreakCondition Conditions;
        }


        class PseudoBreakConditionsString : IStringReadOnlyValueAnnotation, IValueDescriptionAnnotation
        {       
            
            static BreakCondition[] conditions = Enum.GetValues(typeof(BreakCondition)).OfType<BreakCondition>().ToArray();

            static BreakCondition[] breakConditions =
                conditions.Where(x => x.ToString().Contains("Break")).ToArray();
            static string getEnumString(BreakCondition value)
            {
                if (value == 0) return "None";
                var sb = new StringBuilder();
                var breakFlags = breakConditions.Where(x => value.HasFlag(x));
                if (breakFlags.Any())
                {
                    sb.AppendFormat("Break on {0}",breakFlags.First().ToString().Substring("BreakOn".Length));
                    var breakFlags2 = breakFlags.Skip(1);
                    foreach (var x in breakFlags2)
                    {
                        sb.AppendFormat(" or {0}", x.ToString().Substring("BreakOn".Length));
                    }
                }
                return sb.ToString();
            }

            static BreakCondition convertAbortCondition(EngineSettings.AbortTestPlanType abortType)
            {
                return ((abortType.HasFlag(EngineSettings.AbortTestPlanType.Step_Fail)) ? BreakCondition.BreakOnFail : 0) 
                       | (abortType.HasFlag(EngineSettings.AbortTestPlanType.Step_Error) ? BreakCondition.BreakOnError : 0);
            }
            
            public static (BreakCondition, string) getInheritedVerdict(ITestStepParent _step)
            {
                ITestStepParent src = _step;
                src = src.Parent;
                while (src is ITestStep step)
                {
                    var cond = BreakConditionProperty.GetBreakCondition(step);
                    if (cond.HasFlag(BreakCondition.Inherit) == false)
                        return (cond, $"parent step '{step.Name}'");

                    src = step.Parent as ITestStep;
                }

                return (convertAbortCondition(EngineSettings.Current.AbortTestPlan), "engine settings");
            }

            public (BreakCondition, string) GetCondition()
            {
                var condition = annotation.condition;
                if (condition.IsEnabled == false &&  annotation.annotation.Source is ITestStepParent step)
                    return getInheritedVerdict(step);
                    
                var valuemem = (BreakCondition) valueAnnotation.Get<IObjectValueAnnotation>().Value;
                return (valuemem, null);
            }

            public string Value
            {
                get
                {
                    var (condition, _) = GetCondition();
                    return getEnumString(condition);
                }
            }
            
            public BreakConditionsAnnotation annotation;
            public AnnotationCollection valueAnnotation;

            public string Describe()
            {
                var (condition, kind) = GetCondition();
                var str = getEnumString(condition);
                if (kind == null) return str;
                return $"{str} (inherited from {kind}).";
            }
        }

        PseudoBreakConditionsString str;
        AnnotationCollection createSubAnnotation()
        {
            var sub = annotation.AnnotateSub(TypeData.GetTypeData(condition), condition);
            var fst = sub.Get<IMembersAnnotation>().Members
                .FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name.Contains("IsEnabled") == false);
            fst.Add(str = new PseudoBreakConditionsString(){annotation = this, valueAnnotation = fst});
            if (str != null && condition.IsEnabled == false)
            {
                condition.Conditions = str.GetCondition().Item1.SetFlag(BreakCondition.Inherit, true);
            }
            return sub;
        }

        AnnotationCollection subannotations;
        public IEnumerable<AnnotationCollection> Members => (subannotations ?? (subannotations = createSubAnnotation())).Get<IMembersAnnotation>().Members.Reverse();     

        PseudoBreakConditions condition = new PseudoBreakConditions();
        AnnotationCollection annotation;
        public void Read(object source)
        {
            subannotations?.Read();
            condition.Conditions = ((BreakCondition)(annotation.Get<IObjectValueAnnotation>().Value ?? (BreakCondition) 0));
            if (str != null && condition.IsEnabled == false)
            {
                condition.Conditions = str.GetCondition().Item1.SetFlag(BreakCondition.Inherit, true);
            }
            
        }

        public void Write(object source)
        {
            subannotations?.Write();
            if (str != null && condition.IsEnabled == false)
            {
                condition.Conditions = str.GetCondition().Item1.SetFlag(BreakCondition.Inherit, true);
            }
            
            annotation.Get<IObjectValueAnnotation>().Value = condition.Conditions;
            
        }

        public BreakConditionsAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap
{
    /// <summary> 
    /// Marks a setting that can be enabled/disabled by the user. UIs are expected to render a checkbox in front of the actual value. 
    /// Settings of type <see cref="Enabled{T}"/> gets annotated with an annotation that implements this.
    /// </summary>
    public interface IEnabledValueAnnotation : IAnnotation
    {
        /// <summary>
        /// Indicates whether this setting is enabled.
        /// </summary>
        AnnotationCollection IsEnabled { get; }

        /// <summary>
        /// Annotations describing the actual value.
        /// </summary>
        AnnotationCollection Value { get; }
    }

    internal class BreakConditionsAnnotation : IEnabledValueAnnotation, IOwnedAnnotation, IMembersAnnotation
    {
        internal class BreakConditionValueAnnotation : IStringReadOnlyValueAnnotation, IValueDescriptionAnnotation, IAccessAnnotation
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
                    sb.AppendFormat("Break on {0}", breakFlags.First().ToString().Substring("BreakOn".Length));
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

            private static (BreakCondition Condition, string InheritKind, bool MultiselectDifference) getInheritedVerdict(ITestStepParent _step)
            {
                ITestStepParent src = _step;
                src = src.Parent;
                while (src != null)
                {
                    var cond = BreakConditionProperty.GetBreakCondition(src);
                    if (cond.HasFlag(BreakCondition.Inherit) == false)
                    {
                        if (src is TestPlan)
                            return (cond, $"test plan", false);
                        return (cond, $"parent step '{((ITestStep)src).GetFormattedName()}'", false);
                    }

                    src = src.Parent;
                }

                return (convertAbortCondition(EngineSettings.Current.AbortTestPlan), "engine settings", false);
            }

            public (BreakCondition Condition, string InheritKind, bool MultiselectDifference) GetCondition()
            {
                if (annotation.Conditions.HasFlag(BreakCondition.Inherit))
                {
                    if(annotation.annotation.Source is ITestStepParent step)
                        return getInheritedVerdict(step);
                    if (annotation.annotation.Source is IEnumerable<ITestStepParent> stepList)
                    {
                        return getInheritedVerdict(stepList.First());
                    }
                }

                if (valueAnnotation.Get<IObjectValueAnnotation>().Value == null)
                {
                    var valuemem = (BreakCondition)0;
                    return (valuemem, null, true);
                }
                else
                {
                    var valuemem = (BreakCondition)valueAnnotation.Get<IObjectValueAnnotation>().Value;
                    return (valuemem, null, false);
                }
            }

            public string Value
            {
                get
                {
                    var (condition, _, multiselectDifference) = GetCondition();
                    if (multiselectDifference)
                        return "";
                    return getEnumString(condition);
                }
            }

            public bool IsReadOnly => annotation.Conditions.HasFlag(BreakCondition.Inherit);

            public bool IsVisible => true;

            readonly BreakConditionsAnnotation annotation;
            public AnnotationCollection valueAnnotation;

            public string Describe()
            {
                var (condition, kind, multiselectDifference) = GetCondition();
                if (multiselectDifference)
                    return "Selected Test Steps has different values for this setting.";
                var str = getEnumString(condition);
                if (kind == null) return str;
                return $"{str} (inherited from {kind}).";
            }

            public BreakConditionValueAnnotation(BreakConditionsAnnotation annotation)
            {
                this.annotation = annotation;
            }
        }

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
        
        AnnotationCollection createEnabledAnnotation()
        {
            bool isEnabled = !Conditions.HasFlag(BreakCondition.Inherit);
            var sub = annotation.AnnotateSub(TypeData.GetTypeData(isEnabled), isEnabled);
            sub.Add(new AnnotationCollection.MemberAnnotation(TypeData.FromType(typeof(IEnabled)).GetMember(nameof(IEnabledValue.IsEnabled)))); // for compatibility with 9.8 UIs, emulate that this is a IsEnabled member from a Enabled<T> class
            return sub;
        }
        AnnotationCollection enabledAnnotation;
        public AnnotationCollection IsEnabled => (enabledAnnotation ?? (enabledAnnotation = createEnabledAnnotation()));

        BreakConditionValueAnnotation str;
        AnnotationCollection createValueAnnotation()
        {
            var _value = (Values)(int)Conditions;
            var sub = annotation.AnnotateSub(TypeData.GetTypeData(_value), _value);
            sub.Add(new AnnotationCollection.MemberAnnotation(TypeData.FromType(typeof(Enabled<Values>)).GetMember("Value"))); // for compatibility with 9.8 UIs, emulate that this is a Value member from a Enabled<T> class
            sub.Add(str = new BreakConditionValueAnnotation(this) { valueAnnotation = sub });
            return sub;
        }
        AnnotationCollection subannotations;
        public AnnotationCollection Value => (subannotations ?? (subannotations = createValueAnnotation()));

        internal BreakCondition Conditions
        {
            get => (BreakCondition) annotation.Get<IObjectValueAnnotation>().Value;
            set { annotation.Get<IObjectValueAnnotation>().Value = value; }
        }

        public IEnumerable<AnnotationCollection> Members => new[] { IsEnabled, Value }; // IMembersAnnotationthis is implemented here for compatablility with 9.8 UIs 

        AnnotationCollection annotation;
        internal BreakConditionsAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }

        public void Read(object source)
        {
            if (subannotations != null)
            {
                Value.Get<IObjectValueAnnotation>().Value = (Values)(int)Conditions;
                Value.Read();
            }

            if (enabledAnnotation != null)
            {
                IsEnabled.Get<IObjectValueAnnotation>().Value = false == Conditions.HasFlag(BreakCondition.Inherit);
                IsEnabled.Read();
            }
        }

        public void Write(object source)
        {
            if (subannotations == null && enabledAnnotation == null) return;
            Value?.Write();
            var cond = (BreakCondition)(int)subannotations.Get<IObjectValueAnnotation>().Value;
            var dontInherit = (bool)(enabledAnnotation?.Get<IObjectValueAnnotation>().Value ?? false);
            
            if (dontInherit && cond.HasFlag(BreakCondition.Inherit))
            {
                var cond2 = str.GetCondition();
                cond = cond2.Condition;
            } 
            else if (dontInherit == false)
            {
                cond = BreakCondition.Inherit;
            }
            
            cond = cond.SetFlag(BreakCondition.Inherit, !dontInherit);
            Conditions = cond;
            annotation.Get<IObjectValueAnnotation>().Value = cond;
            Read(source);
        }
    }
}
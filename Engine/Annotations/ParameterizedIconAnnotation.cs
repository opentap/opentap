using System;
using System.Linq;

namespace OpenTap
{
    class ParameterizedIconAnnotation :IIconAnnotation, IEnabledAnnotation, ISettingReferenceIconAnnotation
    {
        public string IconName => IconNames.Parameterized;
        /// <summary> Parameterized properties are disabled is controlled by the parent parameter </summary>
        public bool IsEnabled => false;

        public Guid TestStepReference { get; set; }

        public string MemberName { get; set; }
    }

    class ParameterIconAnnotation : IInteractiveIconAnnotation
    {
        public string IconName => IconNames.EditParameter;
        public AnnotationCollection Action => annotation.Get<MenuAnnotation>().MenuItems
            .FirstOrDefault(x => x.Get<IconAnnotationAttribute>().IconName == IconName);
        public ParameterIconAnnotation(AnnotationCollection annotation) => this.annotation = annotation;
        
        readonly AnnotationCollection annotation;
    }
    
    class InputIconAnnotation : IIconAnnotation, IEnabledAnnotation, ISettingReferenceIconAnnotation
    {
        public string IconName => IconNames.Input;
        
        /// <summary> Inputs are disabled in the GUI and is controlled by the output parameter </summary>
        public bool IsEnabled => false;

        public Guid TestStepReference { get; set; }

        public string MemberName { get; set; }
    }

    class OutputAnnotation : IIconAnnotation
    {
        public string IconName => IconNames.Output;
    }
    
    class OutputAssignedAnnotation : IIconAnnotation, ISettingReferenceIconAnnotation
    {
        public string IconName => IconNames.OutputAssigned;

        public Guid TestStepReference { get; set; }

        public string MemberName { get; set; }
    }

    class HasExpressionAnnotation : IIconAnnotation, IEnabledAnnotation, IInteractiveIconAnnotation
    {
        readonly AnnotationCollection annotation;
        public string IconName => IconNames.HasExpression;
        public bool IsEnabled => false;
        public AnnotationCollection Action => annotation.ParentAnnotation;
        

        public HasExpressionAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
    }

    class IconAnnotationHelper
    {
        public static void AddParameter(AnnotationCollection annotation, IMemberData member, object source)
        {
            var stepModel = TestStepMenuModel.FromSource(member, annotation.Source);
            if (stepModel != null)
            {
                if (stepModel.IsParameterized)
                {
                    if (annotation.Source is ITestStepParent step)
                    {
                        (IParameterMemberData parameter, object parent) = ParameterizedMembersCache.GetParameterFor(step, member);   
                        annotation.Add(new ParameterizedIconAnnotation
                        {
                            TestStepReference = (parent as ITestStep)?.Id ?? Guid.Empty,
                            MemberName = parameter?.Name
                        });
                    }
                    else // this is the multiselect case (source is an array)
                    {
                        annotation.Add(new ParameterizedIconAnnotation());
                    }
                }
                if (stepModel.HasExpression)
                    annotation.Add(new HasExpressionAnnotation(annotation));
                if (stepModel.IsParameter)
                    annotation.Add(new ParameterIconAnnotation(annotation));
                if (stepModel.IsOutput)
                    annotation.Add(new OutputAnnotation());
                if (stepModel.IsAnyInputAssigned) // this is an assigned output. In case of multiselect, at least one of the selected step has this as an assigned output
                {
                    if (annotation.Source is ITestStepParent step)
                    {
                        var relation = InputOutputRelation.GetRelations(step).FirstOrDefault(r => r.OutputMember == member && r.OutputObject == source);
                        annotation.Add(new OutputAssignedAnnotation
                        {
                            TestStepReference = (relation.InputObject as ITestStep)?.Id ?? Guid.Empty,
                            MemberName = relation.InputMember.Name
                        });
                    }
                    else // this is the multiselect case (source is an array)
                    {
                        annotation.Add(new OutputAssignedAnnotation()); // In case of multiselect, don't populate the ISettingReferenceIconAnnotation part

                    }
                }
                if (stepModel.IsAnyOutputAssigned) // this is an input. In case of multiselect, at least one of the selected step has this as an input
                {
                    if (annotation.Source is ITestStepParent step)
                    {
                        var relation = InputOutputRelation.GetRelations(step).FirstOrDefault(r => r.InputMember == member && r.InputObject == source);
                        annotation.Add(new InputIconAnnotation
                        {
                            TestStepReference = (relation.OutputObject as ITestStep)?.Id ?? Guid.Empty,
                            MemberName = relation.OutputMember.Name
                        });
                    }
                    else // this is the multiselect case (source is an array)
                    {
                        annotation.Add(new InputIconAnnotation()); // In case of multiselect, don't populate the ISettingReferenceIconAnnotation part
                    }
                }
            }
        }
    }
}
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
    
    class InputIconAnnotation : IIconAnnotation, IEnabledAnnotation
    {
        public string IconName => IconNames.Input;
        
        /// <summary> Inputs are disabled in the GUI and is controlled by the output parameter </summary>
        public bool IsEnabled => false;
    }

    class OutputAnnotation : IIconAnnotation
    {
        public string IconName => IconNames.Output;
    }
    
    class OutputAssignedAnnotation : IIconAnnotation
    {
        public string IconName => IconNames.OutputAssigned;
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
                    ITestStep step = annotation.Source as ITestStep;
                    ITestStepParent parent = step;
                    ParameterMemberData parameter = null;
                    while (parameter == null)
                    {
                        parent = parent.Parent;
                        parameter = member.GetParameter(parent, step);
                    }
                    annotation.Add(new ParameterizedIconAnnotation
                    {
                        TestStepReference = (parent as ITestStep)?.Id ?? Guid.Empty,
                        MemberName = parameter.Name
                    });
                }
                if (stepModel.IsParameter)
                    annotation.Add(new ParameterIconAnnotation(annotation));
                if(stepModel.IsOutput)
                    annotation.Add(new OutputAnnotation());
                if(stepModel.IsAnyInputAssigned)
                    annotation.Add(new OutputAssignedAnnotation());
                if (stepModel.IsAnyOutputAssigned)
                    annotation.Add(new InputIconAnnotation());
            }
        }
    }
}
using System;
using System.Collections.Generic;
using OpenTap.Expressions;
namespace OpenTap
{
    class HasExpressionAnnotation : IIconAnnotation, IEnabledAnnotation, IInteractiveIconAnnotation, IExpressionAnnotation, IOwnedAnnotation, IErrorAnnotation
    {
        readonly AnnotationCollection annotation;
        public string IconName => IconNames.HasExpression;
        public bool IsEnabled => false;
        public AnnotationCollection Action => annotation.ParentAnnotation;
        
        public HasExpressionAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
        public string Expression { get; set; }
        public string Error { get; private set; }

        public void Read(object source)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member;
            if (source is ITestStepParent step && member != null)
            {
                Expression = ExpressionManager.GetExpression(step, annotation.Get<IMemberAnnotation>()?.Member);
                Error = ExpressionManager.ExpressionError(Expression, step, annotation.Get<IMemberAnnotation>()?.Member?.TypeDescriptor);
            }
        }
        public void Write(object source)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member;
            if(source is ITestStepParent parent  && member != null)
                ExpressionManager.SetExpression(parent, annotation.Get<IMemberAnnotation>()?.Member, Expression);
        }
        public IEnumerable<string> Errors => string.IsNullOrWhiteSpace(Error) ? Array.Empty<string>() : new []
        {
            $"Expression: {Error}"
        };
    }
}

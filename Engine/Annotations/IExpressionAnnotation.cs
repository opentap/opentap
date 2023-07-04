using OpenTap.Expressions;
namespace OpenTap
{
    /// <summary>  Annotation added when a setting has an expression. </summary>
    public interface IExpressionAnnotation
    {
        /// <summary>  The current expression value. </summary>
        public string Expression { get; set; }
    }
    
    class HasExpressionAnnotation : IIconAnnotation, IEnabledAnnotation, IInteractiveIconAnnotation, IExpressionAnnotation, IOwnedAnnotation
    {
        readonly AnnotationCollection annotation;
        public string IconName => IconNames.HasExpression;
        public bool IsEnabled => false;
        public AnnotationCollection Action => annotation.ParentAnnotation;
        
        public HasExpressionAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
        public string Expression
        {
            get;
            set;
        }

        public void Read(object source)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member;
            if(source is ITestStepParent parent && member != null)
                Expression = ExpressionManager.GetExpression(parent, annotation.Get<IMemberAnnotation>()?.Member);
        }
        public void Write(object source)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member;
            if(source is ITestStepParent parent  && member != null)
                ExpressionManager.SetExpression(parent, annotation.Get<IMemberAnnotation>()?.Member, Expression);
        }
    }

}

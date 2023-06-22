using System;
namespace OpenTap
{
    /// <summary>
    /// Verdict attribute can be used to set the verdict based on an expression. For more information on expressions see the developer guide.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class VerdictAttribute : Attribute
    {
        /// <summary>
        /// The expression used for validation. This should be a boolean expression.
        /// </summary>
        public string Expression { get; }
        
        /// <summary>
        /// If the expression evaluated to 'true', the verdict of the test step will be set to this, if it current verdict is less 'important'. See Verdict for more information.
        /// </summary>
        public Verdict Verdict { get; } 
        
        /// <summary> Creates a new instance of VerdictAttribute. </summary>
        /// <param name="expression"></param>
        /// <param name="verdict"></param>
        public VerdictAttribute(string expression, Verdict verdict = Verdict.Pass)
        {
            Expression = expression;
            Verdict = verdict;
        }
    }

}

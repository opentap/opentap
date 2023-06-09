using System;
namespace OpenTap
{
    /// <summary>
    /// Verdict attribute can be used to set the verdict based on an expression.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class VerdictAttribute : Attribute
    {
        public string Expression { get; }
        public Verdict Verdict { get; } 
        public VerdictAttribute(string expression, Verdict verdict = Verdict.Pass)
        {
            Expression = expression;
            Verdict = verdict;
        }
    }

}

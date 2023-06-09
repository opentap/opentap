using System;
namespace OpenTap
{
    /// <summary>
    /// Validation attribute. Load validation rules from attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ValidationAttribute : Attribute
    {
        public ValidationAttribute(string expression)
        {
            Expression = expression;
        }
        public string Expression { get; }
    }
}
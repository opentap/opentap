using System;
namespace OpenTap
{
    /// <summary>
    /// Validation attribute. Load validation rules from expressions in attributes. For more information on expressions see the developer guide.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ValidationAttribute : Attribute
    {
        /// <summary> Creates a new instance of ValidationAttribute. </summary>
        /// <param name="expression"></param>
        /// <param name="message"></param>
        public ValidationAttribute(string expression, string message = null)
        {
            Expression = expression;
            Message = message;
        }
        
        
        /// <summary> Gets or sets the expression used for validation. </summary>
        public string Expression { get; }
        
        /// <summary> Gets or sets the expression returned when validation fails. This may contain interpolations, such as "{Frequency} is lower than {LowerLimit}." </summary>
        public string Message { get; }
    }
}
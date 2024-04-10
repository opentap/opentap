using System;
namespace OpenTap
{
    /// <summary> Specifies an optional min and max value for a property. </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RangeAttribute : Attribute
    {
        /// <summary>  The minimum value for the property. </summary>
        public double? Minimum { get; }
        /// <summary> The maximum value for the property. </summary>
        public double? Maximum { get; }
        
        /// <summary> Creates a new instance of the RangeAttribute. </summary>
        public RangeAttribute(double minimum = double.NaN, double maximum = double.NaN)
        {
            if (!double.IsNaN(minimum))
                Minimum = minimum;
            if(!double.IsNaN(maximum))
                Maximum = maximum;
        }
    }
}

using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Determines the order of plugin activation related to other plugins.
    /// </summary>
    public class PluginOrderAttribute : Attribute
    {
        static readonly PluginOrderAttribute @default = new PluginOrderAttribute();
        /// <summary> The type marked with the PluginOrderAttribute should come before this type. </summary>
        public Type Before { get; }
        /// <summary> The type marked with the PluginOrderAttribute should come after this type. </summary>
        public Type After { get; }
        /// <summary> The type marked with the PluginOrderAttribute should be weighted by this order if everything else is the same. Sorting by smallest order first (default sort order for double) </summary>
        public double Order { get; }

        /// <summary>
        /// Creates a new instance of this attribute. 
        /// </summary>
        /// <param name="before"> Order of this plugin is before another plugin.</param>
        /// <param name="after"> Order of this plugin is after another plugin.</param>
        /// <param name="order">Everything else being the same, use a number sort order.</param>
        public PluginOrderAttribute(Type before = null, Type after = null, double order = 0.0)
        {
            Before = before;
            After = after;
            Order = order;
        }
        
        /// <summary>
        /// This comparer can be used for sorting a list of objects based on their plugin order attributes.
        /// </summary>
        internal static Comparison<object> Comparer
        {
            get
            {
                var lut = new Dictionary<object, PluginOrderAttribute>();
                return (o, o1) =>
                {
                    if (lut.TryGetValue(o, out var x) == false)
                        lut[o] = x = o.GetType().GetAttribute<PluginOrderAttribute>() ?? @default;
                    if (lut.TryGetValue(o1, out var y) == false)
                        lut[o1] = y = o1.GetType().GetAttribute<PluginOrderAttribute>() ?? @default;

                    if (x.Before != null && o1.GetType().DescendsTo(x.Before))
                        return -1;
                    if (x.After != null && o1.GetType().DescendsTo(x.After))
                        return 1;
                    if (y.Before != null && o.GetType().DescendsTo(y.Before))
                        return 1;
                    if (y.After != null && o.GetType().DescendsTo(y.After))
                        return -1;
                    
                    return x.Order.CompareTo(y.Order);
                };
            }
        }
    }
}
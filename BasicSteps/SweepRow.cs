using System.Collections.Generic;
using System.ComponentModel;

namespace OpenTap.Plugins.BasicSteps
{
    
    /// <summary> An element representing a row in a sweep loop. This has a bunch of dynamically added elements.</summary>
    public class SweepRow
    {
        /// <summary> Gets or sets if the row is enabled.</summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary> The sweep step owning this row. This is needed to figure out which properties the object has. </summary>
        [Browsable(false)]
        public SweepParameterStep Loop { get; set; }
        
        /// <summary> Dictionary for storing dynamic property values. </summary>
        public Dictionary<string, object> Values = new Dictionary<string, object>();
    }
}
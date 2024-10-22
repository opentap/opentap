using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    
    /// <summary> An element representing a row in a sweep loop. This has a bunch of dynamically added elements.</summary>
    public class SweepRow
    {
        /// <summary> Gets or sets if the row is enabled.</summary>
        [Display("Enabled", "Marks if this row should be included during the sweep.")]
        public bool Enabled { get; set; } = true;
        
        /// <summary> The sweep step owning this row. This is needed to figure out which properties the object has. </summary>
        [Browsable(false), XmlIgnore]
        public SweepParameterStep Loop { get; set; }
        
        /// <summary> Dictionary for storing dynamic property values. </summary>
        public Dictionary<string, object> Values = new Dictionary<string, object>();

        internal string GetIterationString() => string
            .Join(", ", 
                Values.OrderBy(x => x.Key).Select(x => $"{x.Key} = {x.Value}"));

        /// <summary> Create a sweep row instance. </summary>
        public SweepRow()
        {
            
        }
        
        /// <summary> Create a sweep row instance with a step. </summary>
        public SweepRow(SweepParameterStep loop)
        {
            this.Loop = loop;
        }
    }
}
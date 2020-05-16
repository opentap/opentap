using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OpenTap;

namespace PluginDevelopment.Advanced_Examples
{

    public class ReadOnlyListElement
    {
        [Browsable(true)] public int X => GetHashCode(); 
        [Browsable(true)] public string Message => X + " example";
        public string Note { get; set; } = "Write notes here..";
    }
    
    [Display("Read-Only List Example", "Demonstrates how to use a list where items cannot be added or removed." +
                                       " Elements themselves can be changed, but they are also mostly read-only.",
        Groups: new[] { "Examples", "Plugin Development", "Advanced Examples" })]
    public class ReadOnlyListExample : TestStep
    {
        List<ReadOnlyListElement> elements = new List<ReadOnlyListElement>
        {
            new ReadOnlyListElement(), new ReadOnlyListElement(), new ReadOnlyListElement(),
        };
        
        [Display("Elements", "Editing is very limited. Size is fixed but elements can be modified.")]
        public IReadOnlyList<ReadOnlyListElement> Elements
        {
            get => elements.AsReadOnly();
            // still support XML serialization/deserialization.
            set { elements = value.ToList(); }
        }
    
        public override void Run()
        {
            
        }
    }
}
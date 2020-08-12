//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
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
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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    // The Display attribute is used to customize the appearance of an item in the GUI.  
    // Parameters can be set to configure the name, hierarchical grouping, display order, and help description.
    // The Display attribute can be applied to classes such as TestStep, Instrument, and DUT as well as their associated Settings.
    [Display(Name: "Display Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "An example of using the Display attribute.")]
    public class DisplayAttributeExample : TestStep
    {
        #region Settings

        // Name (Required)
        // Name is the string displayed in the GUI. 
        // This allows for names to be displayed with spaces, not have to look like they were derived from code.
        // It allows for spaces and other special characters within the name.
        [Display("Display Name")]
        public string DisplayName { get; set; }

        // Description (Optional, default value of "")
        // Description is shown in editors and tools tips on the GUI.
        [Display("Display Name w/ Description", Description: "Example of a help description.")]
        public string DisplayNameWithDescription { get; set; }

        // Group  (Optional, default value of "")
        // This can be used for hierarchical sorting. 
        // For 1 level of nesting use the singular Group.
        // This will appear as:
        //      Group1 >
        //          Display Name w/ Group
        [Display("Display Name w/ Group", Group: "Group1")]
        public string DisplayNameWithGroup { get; set; }

        // Groups  (Optional, default value of string [] = null)
        // Note this only applies to TestSteps, Instruments, and DUTs. It does not apply to Step Settings.
        // This can be used for hierarchical sorting of multiple levels. 
        // Multiple Groups can be defined using they array initializer syntax.
        // Either Group or Groups can be used, but not both.
        // See the top of this page for an example.
        // In the New Step window this Test Step will appear as:
        //      Examples >
        //          Plugin Development >
        //              Attributes >
        //                  Display Attribute Example


        // Collapsed (Optional, default value of false)
        // Above all of our Groups defaulted to being expanded.
        // Collapsed is a bool, and indicates if a group/groups default appearance should be collapsed.
        [Display("Display Name Collapsed Group", Group: "Collapsed Group", Collapsed: true)]
        public string DisplayNameCollapsedGroup { get; set; }

        // Order (Optional, default value of -10000)
        // Order is used to sort items within a group (ungrouped items are also sorted by Order).
        // Items are sorted lowest value to highest value.  Meaning the lowest Order value will appear at the top of the Group.
        // Items with equal order magnitudes are sorted alphabetically.
        // This will appear as:
        //      Ordered Group >
        //          DisplayNameOrder1
        //          DisplayNameOrder2         
        [Display(Name: "Display Name Order 1", Group: "Ordered Group", Order: 1)]
        public string DisplayNameOrder1 { get; set; }

        [Display(Name: "Display Name Order 2", Group: "Ordered Group", Order: 2)]
        public string DisplayNameOrder2 { get; set; }

        // Groups are sorted by the average value of all the Settings they contain.
        // The average order of Ordered Group1 is 10.05, where Ordered Group2 is 20.05.
        // This means Ordered Group1 will appear first.  Items within a group as still sorted as described by Order above.
        // This will appear as:
        //      Ordered Group1 >
        //          First In Group1
        //          Second In Group2
        //      Ordered Group2 >
        //          First In Group2
        //          Second in Group2

        // Notice Double values can be used. This is a recommended practice for sorting items within a group.
        [Display(Name: "Second In Group1", Group: "Ordered Group1", Order: 10.1)]
        public string SecondInGroup1 { get; set; }
        
        [Display(Name: "First In Group1", Group: "Ordered Group1", Order: 10.0)]
        public string FirstInGroup1 { get; set; }

        [Display(Name: "First In Group2", Group: "Ordered Group2", Order: 20)]
        public string FirstInGroup2 { get; set; }

        [Display(Name: "Second In Group2", Group: "Ordered Group2", Order: 20.1)]
        public string SecondInGroup2 { get; set; }

        #endregion

        public override void Run()
        {
            // Do nothing
        }
    }
}

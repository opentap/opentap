//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{

    /// <summary> 
    /// Identifies the TestSteps that can be selected for a TestStep property. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class StepSelectorAttribute : Attribute
    {
        /// <summary> For selecting which steps are available in selection. It's seen relative to the step having the property that is marked with the <see cref="StepSelectorAttribute"/>.</summary>
        public enum FilterTypes
        {
            /// <summary>  All steps in the entire test plan. </summary>
            All,
            /// <summary>   Show only children of this step. </summary>
            Children,
            /// <summary>  Show only siblings of this step. </summary>
            Sibling,
            /// <summary> All steps in the entire test plan, except for the step itself. </summary>
            AllExcludingSelf
        }
        /// <summary> Selects the available items for selection on the TestStep property. </summary>
        /// <value> The filter. </value>
        public FilterTypes Filter { get; private set; }

        /// <summary>   Default constructor for StepSelectorAttribute. </summary>
        public StepSelectorAttribute()
        {
            Filter = FilterTypes.All;
        }

        /// <summary> Constructor for StepSelectorAttribute. </summary>
        /// <param name="filter">   The filter. </param>
        public StepSelectorAttribute(FilterTypes filter)
        {
            Filter = filter;
        }
    }
}

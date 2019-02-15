//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.ComponentModel;

namespace OpenTap
{
    /// <summary>
    /// Provides the functionality to validate an element when the property changes. 
    /// </summary>
    public interface IValidatingObject : IDataErrorInfo, INotifyPropertyChanged
    {
        /// <summary>
        /// A collection of all the currently defined validation rules. Add new rules here in order to get runtime value validation. 
        /// </summary>
        ValidationRuleCollection Rules { get; }
        /// <summary>
        /// Triggers the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">String name of which property has been changed.</param>
        void OnPropertyChanged(string propertyName);
    }
}

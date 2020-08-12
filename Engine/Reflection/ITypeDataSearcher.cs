//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Searches for "types" and returns them as ITypeData objects. The OpenTAP type system calls all implementations of this.
    /// </summary>
    public interface ITypeDataSearcher
    {
        /// <summary> Get all types found by the search. 
        /// Null will cause Search() to be called (again) before accessing this. </summary>
        IEnumerable<ITypeData> Types { get; }
        /// <summary>
        /// Performs an implementation specific search for types. 
        /// Generates ITypeData objects for all types and populates the Types property with these. 
        /// Always sets the Types property to some value (not null).
        /// </summary>
        void Search();
    }
}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary> Searches for .NET types. This is the default TypeData searcher. </summary>
    [Display(".NET Type Data Searcher", "Provides .NET plugin types.")]
    public class DotNetTypeDataSearcher : ITypeDataSourceProvider
    {
        /// <summary>
        /// Get all types found by the search. 
        /// </summary>
        IEnumerable<ITypeData> ITypeDataSearcher.Types => types;

        IEnumerable<ITypeData> types;
        
        /// <summary> Performs an implementation specific search for types. Generates ITypeData objects for all types found Types property. </summary>
        void ITypeDataSearcher.Search()
        {
            types = PluginManager.GetSearcher().AllTypes.Values;
        }

        ITypeDataSource ITypeDataSourceProvider.GetSource(ITypeData typeData)
        {
            if (typeData is TypeData td && td.Assembly.Location != null)
                return td.Assembly;
            return null;
        }
    }
}

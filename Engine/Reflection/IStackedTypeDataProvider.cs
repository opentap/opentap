//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    /// <summary>Hook into type reflection system. Provides type data for a given object or identifier. This variant is aware of the stack of other providers running after itself.</summary>
    [Display("Stacked TypeData Provider")]
    public interface IStackedTypeDataProvider : ITapPlugin
    {
        /// <summary> Gets the type data from an identifier. </summary>
        /// <param name="identifier">The identifier to get type information for.</param>
        /// <param name="stack">Stack containing remaining ITypeDataProviders that have not yet been called.</param>
        /// <returns>A representation of the type specified by identifier or null if this provider cannot handle the specified identifier.</returns>
        ITypeData GetTypeData(string identifier, TypeDataProviderStack stack);

        /// <summary> Gets the type data from an object. </summary>
        /// <param name="obj">The object to get type information for.</param>
        /// <param name="stack">Stack containing remaining ITypeDataProviders that have not yet been called.</param>
        /// <returns>A representation of the type of the specified object or null if this provider cannot handle the specified type of object.</returns>
        ITypeData GetTypeData(object obj, TypeDataProviderStack stack);

        /// <summary> The priority of this type info provider. Note, this decides the order in which the type info is resolved. </summary>
        double Priority { get; }
    }
}

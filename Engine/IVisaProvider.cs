//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap 
{
    /// <summary>
    /// A mechanism for retrieving IVisa implementations.
    /// </summary>
    public interface IVisaProvider : ITapPlugin {

        /// <summary>
        /// The order in which IVisaProviders will be tested. Lower numbers go first
        /// </summary>
        /// <value></value>
        double Order {get;} // gets the priority in case there are more than one IVisaProvider
        /// <summary>
        /// Retrieves the IVisa interface
        /// </summary>
        /// <value>IVisa implemenation or null</value>
        IVisa Visa {get;} // gets the visa implementation
    }
}
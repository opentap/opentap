//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary> Defines a help link for an object.</summary>
    /// <remarks>General behavior is to search up the visual tree, until the first HelpLink is found.</remarks>
    /// <seealso cref="T:System.Attribute"/>
    public class HelpLinkAttribute : Attribute
    {
        /// <summary>
        /// The HelpLink specified in this attribute.
        /// </summary>
        public string HelpLink { get; private set; }

        /// <summary>
        /// Sets the help link for this object.
        /// </summary>
        /// <param name="helpLink"></param>
        public HelpLinkAttribute(string helpLink)
        {
            HelpLink = helpLink;
        }
        /// <summary> 
        /// Creates a HelpLink without a specified link.
        /// This can be used if information about this item exists in the help for a parent scope. 
        /// </summary>
        public HelpLinkAttribute() :this(null)
        {

        }
    }
}

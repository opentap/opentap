//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Defines how a property, class, enum, or other item is presented to the user. 
    /// Also configures the description and allows items to be grouped and ordered.
    /// </summary>
    public class DisplayAttribute : Attribute
    {
        /// <summary> Optional text that provides a description of the item. 
        /// Consider using HelpLinkAttribute if a link to documentation is needed. 
        /// </summary>
        public string Description { get; private set; }

        /// <summary> Optional text used to group displayed items. 
        /// Use 'Groups' if more than one level of grouping is needed.
        /// </summary>
        public string[] Group { get; private set; }

        /// <summary> Name displayed by the UI.</summary>
        public string Name { get; private set; }

        /// <summary> Optional integer that ranks items and groups in ascending order relative to other items/groups. 
        /// Default is -10000. For a group, the order is the average order of the elements inside the group. 
        /// Any double value is allowed. Items with same order are ranked alphabetically.
        /// </summary>
        /// <remarks>
        /// This applies only to properties.  Classes will ignore this setting and be ordered alphabetically.
        /// </remarks>
        public double Order { get; private set; }

        /// <summary> Boolean setting that indicates whether a group's default appearance is collapsed. 
        /// Default is 'false' (group is expanded).
        /// </summary>
        public bool Collapsed { get; private set; }

        /// <summary> Gets the Group (or Groups) and Name concatenated with a backslash (\).</summary>
        public string GetFullName()
        {
            if(Group.Length == 0)
                return Name;
            return string.Join(" \\ ", Group.Append(Name));
        }

        /// <summary>
        /// Creates a new instance of <see cref="DisplayAttribute"/>.  Ensures that Name is never null.
        /// </summary>
        /// <param name="Name">Name displayed by the UI.</param>
        /// <param name="Description"> Optional text that provides a description of the item. Consider using HelpLinkAttribute if a link to documentation is needed. </param>
        /// <param name="Group"> Optional text used to group displayed items. Use 'Groups' if more than one level of grouping is needed. </param>
        /// <param name="Order"> Optional integer that ranks items and groups in ascending order relative to other items/groups. Default is -10000. 
        /// For a group, the order is the average order of the elements inside the group. Any double value is allowed. Items with same order are ranked alphabetically. </param>
        /// <param name="Collapsed"> Boolean setting that indicates whether a group's default appearance is collapsed. Default is 'false' (group is expanded). </param>
        /// <param name="Groups"> Optional array of text strings to specify multiple levels of grouping. Use 'Group' if only one level of grouping is needed. </param>
        public DisplayAttribute(string Name, string Description = null, string Group = null,  double Order = -10000, bool Collapsed = false, string[] Groups = null)
        {
            if (Name == null)
                throw new ArgumentNullException("Name");
            this.Description = Description;
            this.Name = Name;
            if (Groups != null)
                this.Group = Groups;
            else if (Group != null)
                this.Group = new string[] { Group };
            else this.Group = Array.Empty<string>();
            this.Order = Order;
            this.Collapsed = Collapsed;
        }
    }
}

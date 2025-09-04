//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Globalization;
using System.Linq;
using OpenTap.Translation;

namespace OpenTap
{
    /// <summary>
    /// Defines how a property, class, enum, or other item is presented to the user. 
    /// Also configures the description and allows items to be grouped and ordered.
    /// </summary>
    public class DisplayAttribute : Attribute, IDisplayAnnotation
    {
        internal const string GroupSeparator = " \\ ";
        /// <summary> Optional text that provides a description of the item. 
        /// Consider using HelpLinkAttribute if a link to documentation is needed. 
        /// </summary>
        public string Description { get; }

        /// <summary> Optional text used to group displayed items. 
        /// Use 'Groups' if more than one level of grouping is needed.
        /// </summary>
        public string[] Group { get; }

        /// <summary> Name displayed by the UI.</summary>
        public string Name { get; }

        /// <summary> The language of this attribute..</summary>
        public CultureInfo Language { get; }

        /// <summary> Optional double that ranks items and groups in ascending order relative to other items/groups. 
        /// Default is -10000. For a group, the order is the average order of the elements inside the group. 
        /// Any double value is allowed. Items with same order are ranked alphabetically.
        /// </summary>
        /// <remarks>
        /// This applies only to properties.  Classes will ignore this setting and be ordered alphabetically.
        /// </remarks>
        public double Order { get; }

        /// <summary> Boolean setting that indicates whether a group's default appearance is collapsed. 
        /// Default is 'false' (group is expanded).
        /// </summary>
        public bool Collapsed { get; }

        /// <summary> If the owning DisplayAttribute is a translated display attribute, this property will be set to the neutral (non-translated) variant.
        /// If the owning DisplayAttribute is not translated, this will be null.
        /// </summary>
        public DisplayAttribute NeutralDisplayAttribute { get; set; }

        string fullName = null;
        /// <summary> Gets the Group (or Groups) and Name concatenated with a backslash (\).</summary>
        public string GetFullName()
        {
            if(fullName == null)
            {
                if (Group.Length == 0)
                    fullName = Name;
                else
                    fullName = string.Join(GroupSeparator, Group.Append(Name));
            }
            return fullName;
        }

        /// <summary>
        /// The default order for display attribute. This is set in this way to highlight the fact that
        ///  an order is not set and out of the range of normally selected order values. '0' for example
        ///  is a commonly selected value for order, so it could not be that.
        /// </summary>
        public const double DefaultOrder = -10000.0;

        /// <summary>
        /// Creates a new instance of <see cref="DisplayAttribute"/>.  Ensures that Name is never null.
        /// </summary>
        /// <param name="Name">Name displayed by the UI.</param>
        /// <param name="Description"> Optional text that provides a description of the item. Consider using HelpLinkAttribute if a link to documentation is needed. </param>
        /// <param name="Group"> Optional text used to group displayed items. Use 'Groups' if more than one level of grouping is needed. </param>
        /// <param name="Order"> Optional double that ranks items and groups in ascending order relative to other items/groups. Default is defined by DisplayAttribute.DefaultOrder. 
        /// For a group, the order is the average order of the elements inside the group. Any double value is allowed. Items with same order are ranked alphabetically. </param>
        /// <param name="Collapsed"> Boolean setting that indicates whether a group's default appearance is collapsed. Default is 'false' (group is expanded). </param>
        /// <param name="Groups"> Optional array of text strings to specify multiple levels of grouping. Use 'Group' if only one level of grouping is needed. </param>
        public DisplayAttribute(string Name, string Description = null, string Group = null, double Order = DefaultOrder, bool Collapsed = false, string[] Groups = null)
        {
            this.Name = Name ?? throw new ArgumentNullException(nameof(Name));
            this.Description = Description;
            if (Groups != null)
                this.Group = Groups;
            else if (Group != null)
                this.Group = new [] { Group };
            else this.Group = Array.Empty<string>();
            this.Order = Order;
            this.Collapsed = Collapsed;
            this.Language = TranslationManager.NeutralLanguage;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DisplayAttribute"/>.  Ensures that Name is never null.
        /// </summary>
        /// <param name="Name">Name displayed by the UI.</param>
        /// <param name="Description"> Optional text that provides a description of the item. Consider using HelpLinkAttribute if a link to documentation is needed. </param>
        /// <param name="Group"> Optional text used to group displayed items. Use 'Groups' if more than one level of grouping is needed. </param>
        /// <param name="Order"> Optional double that ranks items and groups in ascending order relative to other items/groups. Default is defined by DisplayAttribute.DefaultOrder. 
        /// For a group, the order is the average order of the elements inside the group. Any double value is allowed. Items with same order are ranked alphabetically. </param>
        /// <param name="Collapsed"> Boolean setting that indicates whether a group's default appearance is collapsed. Default is 'false' (group is expanded). </param>
        /// <param name="Groups"> Optional array of text strings to specify multiple levels of grouping. Use 'Group' if only one level of grouping is needed. </param>
        /// <param name="Language"> The language of this attribute. </param>
        public DisplayAttribute(CultureInfo Language, string Name, string Description = null, string Group = null, double Order = DefaultOrder, bool Collapsed = false, string[] Groups = null) : this(Name, Description, Group, Order, Collapsed, Groups)
        {
            this.Language = Language;
        }

        /// <summary> Overriding Equals to fix strange equality issues between instances of DisplayAttribute. </summary>
        public override bool Equals(object obj)
        {
            if(object.ReferenceEquals(this, obj)) return true;
            if (obj is DisplayAttribute other)
                return other.Name == Name && Group.SequenceEqual(other.Group) && other.Description == Description && other.Order == Order;
            return false;
        }

        /// <summary> Generates a hash code based on the display attribute values.</summary> 
        public override int GetHashCode()
        {
            int h = Name.GetHashCode();
            foreach (var elem in Group)
                h = h * 31241231 + elem.GetHashCode();
            return h;
        }
    }
}

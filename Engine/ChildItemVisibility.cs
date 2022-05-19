using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>  Child item visibility implements the concept of showing or hiding a child item. This could for example be the child steps of a test step. </summary>
    public static class ChildItemVisibility
    {
        /// <summary> Child item visibility.</summary>
        public enum Visibility
        {
            /// <summary> Child items are visible. This is the default state. </summary>
            Visible, 
            /// <summary> Child items are collapsed.</summary>
            Collapsed
        }
        
        internal static readonly DynamicMember VisibilityProperty = new DynamicMember
        {
            Name = "OpenTap.Visibility",
            Attributes = new object[] { new BrowsableAttribute(false), new DefaultValueAttribute(Visibility.Visible), new XmlAttributeAttribute() },
            DefaultValue = Visibility.Visible,
            TypeDescriptor = TypeData.FromType(typeof(Visibility)),
            DeclaringType = TypeData.FromType(typeof(ChildItemVisibility)),
            Writable = true,
            Readable = true
        };

        /// <summary>  Set visibility to collapsed or Visible. </summary>
        /// <param name="obj">The object for which to set visibility.</param>
        /// <param name="visibility"> </param>
        public static void SetVisibility(object obj, Visibility visibility)
        {
            VisibilityProperty.SetValue(obj, visibility);
        }
        
        /// <summary>  Sets the current collapsed or visible state. If not previously set it will return Visible. </summary>
        /// <param name="obj">The object from which to get visibility.</param>
        public static Visibility GetVisibility(object obj)
        {
            return (Visibility) VisibilityProperty.GetValue(obj);
        }
    }
}
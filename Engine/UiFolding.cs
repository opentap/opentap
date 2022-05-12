using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>  UI folding implements the concept of collapsing or expanding something. </summary>
    public static class UiFolding
    {
        // The UI Folding property can have 3 states, null, collapsed and expanded.
        // in order to produce nice XML code, we save the value as a string, but
        // from the API perspective it looks like a bool?.
        
        
        
        internal static readonly DynamicMember UiFoldingProperty = new DynamicMember
        {
            Name = "ui.fold",
            Attributes = new object[] { new BrowsableAttribute(false), new DefaultValueAttribute(null), new XmlAttributeAttribute() },
            DefaultValue = null,
            TypeDescriptor = TypeData.FromType(typeof(string)),
            DeclaringType = TypeData.FromType(typeof(UiFolding)),
            Writable = true,
            Readable = true
        };

        /// <summary>  Set fold to collapsed or expanded. </summary>
        /// <param name="obj">The object for which to set folding.</param>
        /// <param name="expanded">True: expand, False: collapse, null: revert to default.</param>
        public static void SetFolding(object obj, bool? expanded)
        {
            UiFoldingProperty.SetValue(obj, expanded switch
            {
                null => null,
                true => "expand",
                false => "collapse"
            });
        }
        /// <summary>  Sets the current collapsed or folding state. If not set it will return null. </summary>
        /// <param name="obj">The object from which to get folding.</param>
        public static bool? GetFolding(object obj)
        {
            return (UiFoldingProperty.GetValue(obj) as string) switch
            {
                null => null,
                "expand" => true,
                "collapse" => false,
                var otherString => throw new InvalidOperationException("Unsupported value" + otherString)
            };
        }
    }
}
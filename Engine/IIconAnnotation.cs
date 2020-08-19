using System;

namespace OpenTap
{
    /// <summary> Icon annotation, for giving a name of an icon associated with a setting. This is optionally supported by GUIs.</summary>
    public interface IIconAnnotation : IAnnotation
    {
        
        /// <summary> The name of the Icon. e.g OpenFile </summary>
        string IconName { get; }
    }
    
    /// <summary> Icon Annotation attribute. for attaching icon information to a setting. </summary>
    public class IconAnnotationAttribute : Attribute, IIconAnnotation
    {
        /// <summary> The name of the Icon. </summary>
        public string IconName { get; }
        
        /// <summary> Create a new instance of IconAnnotationAttribute. </summary>
        /// <param name="iconName"></param>
        public IconAnnotationAttribute(string iconName) => IconName = iconName;
    }
}
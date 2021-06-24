using System;

namespace OpenTap
{
    /// <summary>
    /// Marking a property with the <see cref="FileResourceAttribute"/> attribute indicates that the property specifies a file.
    /// Files specified by this property are included as dependencies during serialization.
    /// </summary>
    public class FileResourceAttribute : Attribute
    {
        
    }
}
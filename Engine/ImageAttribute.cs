using System;

namespace OpenTap
{
    /// <summary>
    /// Defines an image file 
    /// </summary>
    public class ImageAttribute : Attribute
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        public string ImageSource { get; }
        /// <summary>
        /// Specifies an alternate text for the image, if the image for some reason cannot be displayed
        /// </summary>
        public string AltText { get; }
        
        /// <summary>
        /// Creates an instance of <see cref="ImageAttribute"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="alt"></param>
        public ImageAttribute(string src, string alt)
        {
            ImageSource = src;
            AltText = alt;
        }
    }
}

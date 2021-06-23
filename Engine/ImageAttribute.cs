using System;

namespace OpenTap
{
    /// <summary>
    /// Defines an image file 
    /// </summary>
    public class ImageAttribute : Attribute, IAnnotation
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        public string ImageSource { get; set; }
        /// <summary>
        /// Specifies an alternate text for the image, if the image for some reason cannot be displayed
        /// </summary>
        public string AltText { get; set; }
        
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

        /// <summary>
        /// Default constructor for <see cref="ImageAttribute"/>.
        /// </summary>
        public ImageAttribute()
        {
            
        }
    }
}

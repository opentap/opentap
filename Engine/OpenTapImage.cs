namespace OpenTap
{
    /// <summary> Marks that the annotated object is an image. </summary>
    public interface IImageAnnotation : IAnnotation
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        string ImageSource { get; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        string Description { get; }

    }

    class ImageAnnotation : IImageAnnotation, IOwnedAnnotation
    {
        public ImageAnnotation(string imageSource, string description)
        {
            ImageSource = imageSource;
            Description = description;
        }

        public string ImageSource { get; set; }

        public string Description { get; set; }

        public void Read(object source)
        {
            if (source is IOpenTapImage img)
            {
                ImageSource = img.ImageSource;
                Description = img.Description;
            }
        }

        public void Write(object source)
        {
            if (source is IOpenTapImage img)
            {
                img.ImageSource = ImageSource;
                img.Description = Description;
            }
        }
    }

    /// <summary>
    /// Represents an image resource
    /// </summary>
    public interface IOpenTapImage
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        string ImageSource { get; set; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        string Description { get; set; }
    }

    /// <summary>
    /// Represents an image resource
    /// </summary>
    public class OpenTapImage : IOpenTapImage
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        [FilePath]
        [Display("Image File")]
        public string ImageSource { get; set; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        public string Description { get; set; }
    }
}

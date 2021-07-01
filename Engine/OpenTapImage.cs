namespace OpenTap
{
    /// <summary> Marks that the annotated object is an image. </summary>
    public interface IImageAnnotation : IAnnotation
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        string Description { get; }

    }

    class ImageAnnotation : IImageAnnotation, IOwnedAnnotation
    {
        public ImageAnnotation(string source, string description)
        {
            Source = source;
            Description = description;
        }

        public string Source { get; set; }

        public string Description { get; set; }

        public void Read(object source)
        {
            if (source is IPicture picture)
            {
                Source = picture.Source;
                Description = picture.Description;
            }
        }

        public void Write(object source)
        {
            if (source is IPicture picture)
            {
                picture.Source = Source;
                picture.Description = Description;
            }
        }
    }

    /// <summary>
    /// Represents an image resource
    /// </summary>
    public interface IPicture
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        string Source { get; set; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        string Description { get; set; }
    }

    /// <summary>
    /// Represents an image resource
    /// </summary>
    public class Picture : IPicture
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        [FilePath]
        [Display("Image File")]
        public string Source { get; set; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        public string Description { get; set; }
    }
}

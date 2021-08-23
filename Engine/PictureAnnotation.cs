namespace OpenTap
{
    /// <summary> Marks that the annotated object is an image. </summary>
    public interface IPictureAnnotation : IAnnotation
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        string Source { get; }
    }

    class PictureAnnotation : IPictureAnnotation, IOwnedAnnotation
    {
        public PictureAnnotation(string source)
        {
            Source = source;
        }

        public string Source { get; set; }
        public void Read(object source)
        {
            if (source is IPicture picture)
            {
                Source = picture.Source;
            }
        }

        public void Write(object source)
        {
            if (source is IPicture picture)
            {
                picture.Source = Source;
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
    }
}

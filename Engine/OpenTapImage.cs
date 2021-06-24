namespace OpenTap
{
    /// <summary>
    /// Represents an image resource
    /// </summary>
    public class OpenTapImage
    {
        /// <summary>
        /// Specifies the path to the image
        /// </summary>
        [FilePath]
        public string ImageSource { get; set; }

        /// <summary>
        /// Specifies a description of the image
        /// </summary>
        public string Description { get; set; }
    }
}

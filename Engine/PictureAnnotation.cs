
namespace OpenTap
{
    /// <summary> Marks that the annotated object is a picture. </summary>
    public interface IPictureAnnotation : IAnnotation
    {
        /// <summary>
        /// Specifies the path to the picture
        /// </summary>
        string Source { get; }
        
        /// <summary>
        /// Specifies a description of the picture. Can be used in non-gui applications as an alternative to showing the picture.
        /// </summary>
        string Description { get; }
    }

    class PictureAnnotation : IPictureAnnotation, IOwnedAnnotation
    {
        readonly AnnotationCollection annotation;

        public PictureAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }

        public string Source { get; set; }
        public string Description { get; set; }

        public void Read(object source)
        {
            if (source == null) return;
            var mem = annotation.Get<IObjectValueAnnotation>()?.Value;
            if (mem is IPicture picture)
            {
                Source = picture.Source;
                Description = picture.Description;
            }
        }

        public void Write(object source)
        {
            var objSource = annotation.Get<IObjectValueAnnotation>();
            if (objSource == null) return;
            var mem = objSource?.Value;
            if (mem == null)
                mem = new Picture();
            if (mem is Picture picture)
            {
                picture.Source = Source;
                picture.Description = Description;
            }

            if (mem != null)
                annotation.Get<IObjectValueAnnotation>().Value = mem;
        }
    }

    /// <summary>
    /// Represents a picture resource
    /// </summary>
    public interface IPicture
    {
        /// <summary>
        /// Specifies the path to the picture
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Specifies a description of the picture
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// Represents a picture resource
    /// </summary>
    public class Picture : IPicture
    {
        /// <summary> Specifies the path to the picture. </summary>
        [FilePath]
        [Display("Picture File")]
        public string Source { get; set; }
        
        
        /// <summary> Specifies a description of the picture. </summary>
        public string Description { get; set; }

        /// <summary> Returns true if the two pictures are the same with respect to Source and Description. </summary>
        public override bool Equals(object obj) =>
            obj is Picture pic && pic.Source == Source && pic.Description == Description;

        /// <summary> Calculates a hash based on source and description. </summary>
        public override int GetHashCode() => (int)(0xaacd012
                                                   + (Description?.GetHashCode() ?? 0) * 0x801423bb
                                                   + (Source?.GetHashCode() ?? 0) * 0xf00b0834);
    }
}

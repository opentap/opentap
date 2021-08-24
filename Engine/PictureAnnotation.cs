using System.Linq;

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
        private AnnotationCollection annotation;

        public PictureAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
            Read(annotation.Source);
        }

        public string Source { get; set; }
        public string Description { get; set; }

        public void Read(object source)
        {
            var mem = annotation.Get<IMemberAnnotation>()?.Member;
            var memVal = mem?.GetValue(source);
            
            if (memVal is IPicture picture)
            {
                Source = picture.Source;
                Description = picture.Description;
            }
        }

        public void Write(object source)
        {
            var mem = annotation.Get<IMemberAnnotation>()?.Member;
            var memVal = mem?.GetValue(source);

            var a = AnnotationCollection.Annotate(memVal);
            var members = a.Get<IMembersAnnotation>().Members.Select(m => m.Get<IMemberAnnotation>().Member);

            foreach (var m in members)
            {
                if (!m.Writable) continue;
                if (m.Name == nameof(IPicture.Source))
                    m.SetValue(memVal, Source);
                else if (m.Name == nameof(IPicture.Description))
                    m.SetValue(memVal, Description);
            }
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
        /// <summary>
        /// Specifies the path to the picture
        /// </summary>
        [FilePath]
        [Display("Picture File")]
        public string Source { get; set; }
        
        
        /// <summary>
        /// Specifies a description of the picture
        /// </summary>
        public string Description { get; set; }

    }
}

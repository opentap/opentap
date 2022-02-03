using System.Collections.Generic;
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
            if (source == null) return;
            var sources = (source as IEnumerable<object>)?.ToArray() ?? new[] { source };

            if (sources.Length == 0) return;

            var mem = annotation.Get<IMemberAnnotation>()?.Member;
            var memVal = mem?.GetValue(sources[0]);
            
            if (memVal is IPicture picture)
            {
                Source = picture.Source;
                Description = picture.Description;
            }
        }

        public void Write(object source)
        {
            if (source == null) return;

            var mem = annotation.Get<IMemberAnnotation>()?.Member;
            if (mem == null) return;

            var sources = (source as IEnumerable<object>)?.ToArray() ?? new[] { source };
            if (sources.Length == 0) return;

            foreach (var s in sources)
            {
                var memVal = mem.GetValue(s);

                var a = AnnotationCollection.Annotate(memVal);
                var members = a.Get<IMembersAnnotation>().Members.Select(m => m.Get<IMemberAnnotation>().Member)
                    .Where(m => m.Writable)
                    .ToLookup(m => m.Name);

                members[nameof(IPicture.Source)].FirstOrDefault()?.SetValue(memVal, Source);
                members[nameof(IPicture.Description)].FirstOrDefault()?.SetValue(memVal, Description);
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

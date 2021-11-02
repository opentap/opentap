using System.Collections.Generic;

namespace OpenTap
{
    class AnnotationCache : IAnnotation
    {
        private readonly Dictionary<object, AnnotationCollection> cache = new Dictionary<object, AnnotationCollection>();
        public AnnotationCollection Annotate(object[] items)
        {
            var subannotation = AnnotationCollection.Annotate(items.Length == 1 ? items[0] : items, extraAnnotations: this);
            if(items.Length == 1)
                Register(subannotation);
            return subannotation;
        }

        public AnnotationCollection GetCached(object o)
        {
            if (cache.TryGetValue(o, out var a)) 
                return a;
            return null;
        }

        public void Register(AnnotationCollection c)
        {
            cache[c.Source] = c;
        }
    }
}
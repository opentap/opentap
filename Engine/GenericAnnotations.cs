//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;

namespace OpenTap
{
    /// <summary> Base interface for generic annotations. </summary>
    public interface IGenericAnnotation : IAnnotation
    {
        /// <summary> Called when the annotator is initalized. </summary>
        /// <param name="annotation"></param>
        void Initialize(AnnotationCollection annotation);
        /// <summary> returns true if the annotation can be added to the annotation. </summary>
        /// <param name="annotations"></param>
        /// <returns></returns>
        bool CanAnnotate(AnnotationCollection annotations);
    }

    class GenericAnnotationResolver : IAnnotator
    {
        public double Priority => 5;

        static object tryCreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        public void Annotate(AnnotationCollection annotations)
        {
            var annotators = PluginManager.GetPlugins<IGenericAnnotation>()
                .Select(tryCreateInstance).OfType<IGenericAnnotation>().ToList();
            while (true)
            {
                // iterate until 
                var cnt = annotators.Count;
                for(int i = 0; i < annotators.Count; i++)
                {
                    var a = annotators[i];
                    if (a.CanAnnotate(annotations))
                    {
                        a.Initialize(annotations);
                        annotations.Add(a);
                        annotators.RemoveAt(i);
                    }
                }
                if (cnt == annotators.Count)
                    break; // when no more change, break.
            }
        }
    }
}

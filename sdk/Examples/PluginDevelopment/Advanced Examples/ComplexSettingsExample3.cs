using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenTap;

namespace PluginDevelopment.Advanced_Examples
{
    // This examples shows how to do some of the same things as ComplexSettingsExample1,
    // but with Annotations instead of modifying the original data structure.

    public class ComplexSettingsElement3
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    public class ComplexSettingsExample3 : Dut
    {
        public List<int> AvailableValuesForElements { get; set; } = new List<int>() {1, 2, 3};
        
        // backing list. Not editable by the user.
        public List<ComplexSettingsElement3> ListOfElements { get; set; } = new List<ComplexSettingsElement3>();
        public int Count
        {
            get => ListOfElements.Count;
            set
            {
                while (ListOfElements.Count < value)
                {
                    ListOfElements.Add(new ComplexSettingsElement3());
                }
                while (ListOfElements.Count >= value)
                {
                    ListOfElements.RemoveAt(ListOfElements.Count - 1);
                }
            } 
        }

        public int SubViewStart { get; set; } = 0;
        public int SubViewStop { get; set; } = 0;

        public IReadOnlyList<ComplexSettingsElement3> SubView
        {
            get => ListOfElements.Skip(SubViewStop).Take(SubViewStop - SubViewStart)
                .ToList().AsReadOnly();
            set
            {
                
            }
        }
        
        public void OnElementWrite(ComplexSettingsElement3 elem)
        {
            Log.Debug("Element written {0}", ListOfElements.IndexOf(elem));
        }

        public void OnListWrite(List<ComplexSettingsElement3> value)
        {
            Log.Debug("List written");   
        }
    }

    public class ComplexSettings3Annotator : IAnnotator
    {
        class AvailableIntsAnnotation : IAvailableValuesAnnotation, IOwnedAnnotation
        {
            AnnotationCollection annotation;
            public IEnumerable AvailableValues
            {
                get
                {
                    var dut = annotation.ParentAnnotation?.ParentAnnotation?.Source as ComplexSettingsExample3;
                    if (dut != null) return dut.AvailableValuesForElements;
                    return Enumerable.Empty<int>();
                }
            }
            public AvailableIntsAnnotation(AnnotationCollection annotation) 
                => this.annotation = annotation;
            
            public void Read(object source) {  } // we dont need Read.
            public void Write(object source)
            {
                var dut = annotation.ParentAnnotation?.ParentAnnotation?.Source as ComplexSettingsExample3;
                var elem = (ComplexSettingsElement3) annotation.Source;
                dut.OnElementWrite(elem);
            }
        }

        class ListValueChanged : IOwnedAnnotation
        {
            AnnotationCollection annotation;
            public ListValueChanged(AnnotationCollection annotation) => this.annotation = annotation;
            
            public void Read(object source) {  } // we dont need Read.
            public void Write(object source)
            {
                var v = annotation.Get<IObjectValueAnnotation>(false, this).Value;
                var dut = annotation.Source as ComplexSettingsExample3;
                dut.OnListWrite((List<ComplexSettingsElement3>)v);
            }
        }

        IMemberData listmember = TypeData.FromType(typeof(ComplexSettingsExample3))
            .GetMember(nameof(ComplexSettingsExample3.ListOfElements));
        
        public void Annotate(AnnotationCollection annotations)
        {
            if (Equals(annotations.Get<IMemberAnnotation>()?.Member, listmember))
            {
                annotations.Add(new ListValueChanged(annotations));
                return;
            }
            
            var type = annotations.Get<IReflectionAnnotation>().ReflectionInfo;
            if (type.DescendsTo(typeof(int)) == false)
                return;
            var listAnnotation = annotations.ParentAnnotation;
            var listType = listAnnotation?.Get<IReflectionAnnotation>().ReflectionInfo;
            if (listType == null) return;

            var dutAnnotation = listAnnotation.ParentAnnotation;
            var listElem = dutAnnotation?.Get<IMemberAnnotation>();
            if (listElem?.Member ==  listmember)
            {
                annotations.Add(new AvailableIntsAnnotation(annotations));
            }
            
        }

        public double Priority => 5;
    }
}
//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenTap;

namespace PluginDevelopment.Advanced_Examples
{
    // This examples shows how to do some of the same things as ComplexSettingsExample1,
    // but with Annotations instead of modifying the original data structure.

    public class ComplexSettingsElement2
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    [Display("Complex Settings DUT2", "Demonstrates how to use Annotations to handle complex data.",
        Groups: new[] { "Examples", "Plugin Development", "Advanced Examples" })]
    public class ComplexSettingsExample2 : Dut
    {
        public List<int> AvailableValuesForElements { get; set; } = new List<int>() {1, 2, 3};
        
        public List<ComplexSettingsElement2> ListOfElements { get; set; } = new List<ComplexSettingsElement2>{};

        public void OnElementWrite(ComplexSettingsElement2 elem)
        {
            Log.Debug("Element written {0}", ListOfElements.IndexOf(elem));
        }

        public void OnListWrite(List<ComplexSettingsElement2> value)
        {
            Log.Debug("List written");   
        }
    }

    public class ComplexSettingsAnnotator : IAnnotator
    {
        class AvailableIntsAnnotation : IAvailableValuesAnnotation, IOwnedAnnotation
        {
            AnnotationCollection annotation;
            public IEnumerable AvailableValues
            {
                get
                {
                    var dut = annotation.ParentAnnotation?.ParentAnnotation?.Source as ComplexSettingsExample2;
                    if (dut != null) return dut.AvailableValuesForElements;
                    return Enumerable.Empty<int>();
                }
            }
            public AvailableIntsAnnotation(AnnotationCollection annotation) 
                => this.annotation = annotation;
            
            public void Read(object source) {  } // we dont need Read.
            public void Write(object source)
            {
                var dut = annotation.ParentAnnotation?.ParentAnnotation?.Source as ComplexSettingsExample2;
                var elem = (ComplexSettingsElement2) annotation.Source;
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
                var dut = annotation.Source as ComplexSettingsExample2;
                dut.OnListWrite((List<ComplexSettingsElement2>)v);
            }
        }

        IMemberData listmember = TypeData.FromType(typeof(ComplexSettingsExample2))
            .GetMember(nameof(ComplexSettingsExample2.ListOfElements));
        
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
            if (listElem?.Member == listmember)
            {
                annotations.Add(new AvailableIntsAnnotation(annotations));
            }
            
        }

        public double Priority => 5;
    }
}
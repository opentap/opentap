using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using System.ComponentModel;

namespace OpenTap.Engine.UnitTests
{
    public interface IExpandedObject
    {
        object GetValue(string name);
        void SetValue(string name, object value);
        string[] Names { get; }
    }

    public class ExpandedMemberData : IMemberInfo
    {
        public ITypeInfo DeclaringType { get; set; }

        public IEnumerable<object> Attributes => Array.Empty<object>();

        public string Name { get; set; }

        public bool Writable => true;

        public bool Readable => true;

        public ITypeInfo TypeDescriptor
        {
            get
            {
                var desc = DeclaringType as ExpandedTypeInfo;
                var value = desc.Object.GetValue(Name);
                if (value == null) return null;
                return TypeInfo.GetTypeInfo(value);
            }
        }

        public object GetValue(object owner)
        {
            return ((IExpandedObject)owner).GetValue(Name);
        }

        public void SetValue(object owner, object value)
        {
            ((IExpandedObject)owner).SetValue(Name, value);
        }
    }

    public class ExpandedTypeInfo : ITypeInfo
    {
        public ITypeInfo InnerDescriptor;
        public IExpandedObject Object;
        public ITypeInfoProvider Provider { get; set; }

        public string Name => ExpandMemberDataProvider.exp + InnerDescriptor.Name;

        public IEnumerable<object> Attributes => InnerDescriptor.Attributes;

        public ITypeInfo BaseType => InnerDescriptor;

        public bool CanCreateInstance => InnerDescriptor.CanCreateInstance;

        public object CreateInstance(object[] arguments)
        {
            return InnerDescriptor.CreateInstance(arguments);
        }

        public IMemberInfo GetMember(string name)
        {
            return new ExpandedMemberData { DeclaringType = this, Name = name };
        }

        public IEnumerable<IMemberInfo> GetMembers()
        {
            var innerMembers = InnerDescriptor.GetMembers();
            
            foreach(var name in Object.Names)
            {
                yield return new ExpandedMemberData { DeclaringType = this, Name = name };
            }
            foreach (var mem in innerMembers)
                yield return mem;
        }        
    }

    public class ExpandMemberDataProvider : ITypeInfoProvider
    {
        public double Priority => 1;
        internal const string exp = "exp@";
        public void GetTypeInfo(TypeInfoResolver resolver, string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeInfo.GetTypeInfo(identifier.Substring(exp.Length));
                if(tp != null)
                {
                    resolver.Stop(new ExpandedTypeInfo() { InnerDescriptor = tp, Object = null, Provider = this });
                }
            }
                
        }

        public void GetTypeInfo(TypeInfoResolver res, object obj)
        {
            if (obj is IExpandedObject exp)
            {
                res.Iterate(obj);
                if (res.FoundType != null)
                {
                    var expDesc = new ExpandedTypeInfo();
                    expDesc.InnerDescriptor = res.FoundType;
                    expDesc.Provider = this;
                    expDesc.Object = exp;
                    res.Stop(expDesc);
                }
            }
        }
    }


    public class MyExpandedObject : IExpandedObject
    {
        public double MyProp1 { get; set; }

        Dictionary<string, object> extra = new Dictionary<string, object>();
        public string[] Names => extra.Keys.ToArray();

        public object GetValue(string name)
        {
            object @out = null;
            extra.TryGetValue(name, out @out);
            return @out;
        }

        public void SetValue(string name, object value)
        {
            extra[name] = value;
        }
    }

    [TestFixture]
    public class MemberDataProviderTests
    {
        [Test]
        public void AnnotationsTest()
        {
            var ds = new DelayStep();
            var tp = AnnotationCollection.Annotate(ds);
            var mems = tp.Get<IMembersAnnotation>().Members;
            var cnt = mems.Count(x =>
            {
                var access = x.Get<IAccessAnnotation>();
                if (access == null)
                    return true;
                if (x.Get<IMemberAnnotation>().Member.HasAttribute<XmlIgnoreAttribute>())
                    return false;
                var brows = x.Get<IMemberAnnotation>().Member.GetAttribute<BrowsableAttribute>();
                if (brows != null && !brows.Browsable) return false;
                if (access.IsReadOnly == false && access.IsVisible == true)
                    return true;
                return false;
            });
            
            
            Assert.AreEqual(1, cnt);
        }
        
        [Test]
        public void MemberDataTest()
        {
            var obj = new DelayStep();
            var td = TypeInfo.GetTypeInfo(obj);
            var td2 = TypeInfo.GetTypeInfo(obj);

            Assert.AreEqual(td, td2);
            Assert.IsNotNull(td);
            foreach (var mem in td.GetMembers())
            {
                if (mem.Name == "Parent")
                {
                    Debug.Assert(mem.HasAttribute<XmlIgnoreAttribute>());
                }
                Debug.WriteLine(string.Format("Member: {0} {1}", mem.Name, mem.GetValue(obj)));
            }
        }

        [Test]
        public void MemberData2Test()
        {
            var obj = new MyExpandedObject() { MyProp1 = 10 };
            obj.SetValue("asd", 10);

            var td = TypeInfo.GetTypeInfo(obj);
            Assert.IsNotNull(td);
            foreach (var mem in td.GetMembers())
            {
                Debug.WriteLine(string.Format("Member: {0} {1}", mem.Name, mem.GetValue(obj)));
            }

            var td2 = TypeInfo.GetTypeInfo("System.Windows.WindowState");

        }

        [Test]
        public void MemberDataSerializeTest()
        {
            var obj = new MyExpandedObject() { MyProp1 = 10 };
            var obj2 = new MyExpandedObject() { MyProp1 = 10 };
            var obj3 = new MyExpandedObject() { MyProp1 = 10 };
            //obj.SetValue("asd", 10);
            obj.SetValue("sub2", obj2);
            obj2.SetValue("sub3", obj3);

            obj3.SetValue("_10", 10);
            obj3.SetValue("_20", 20);
            obj3.SetValue("_array_test", new double[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            var ser = new TapSerializer();
            var str = ser.SerializeToString(obj);

            var outobj = ser.DeserializeFromString(str);
            var str2 = ser.SerializeToString(outobj);
        }

        [Display("I am a class")]
        class DataInterfaceTestClass
        {
            [Unit("Hz")]
            public double SimpleNumber { get; set; }

            [AvailableValues("AvailableNumbers")]
            [Display("From Available")]
            [Unit("s")]
        
            public double FromAvailable { get; set; }

            [Unit("s")]
            [AvailableValues("AvailableNumbers")]
            public Enabled<double> FromAvailable2 { get; set; } = new Enabled<double>();

            [Unit("s")]
            public IEnumerable<double> AvailableNumbers { get; set; } = new double[] { 1, 2, 3, 4, 5 };

            public bool ThingEnabled { get; set; }
            [EnabledIf("ThingEnabled", true, HideIfDisabled = true)]
            public string ICanBeEnabled { get; set; }
            
            [Flags]
            public enum MultiSelectEnum
            {
                
                A = 1,
                B = 2,
                C = 4
            }

            public MultiSelectEnum EnumValues { get; set; } = MultiSelectEnum.A;

            public List<MultiSelectEnum> SelectableValues { get; set; } = new List<MultiSelectEnum> { MultiSelectEnum.A, MultiSelectEnum.A | MultiSelectEnum.B };

            public enum SingleEnum
            {
                [Display("AAA")]
                A,
                B
            }

            public SingleEnum TheSingleEnum { get; set; }

            public List<ScpiInstrument> Instruments { get; set; } = new List<ScpiInstrument>();

        }
        
        [Test]
        public void DataInterfaceProviderTest2()
        {
            var sval = AnnotationCollection.Annotate(DataInterfaceTestClass.SingleEnum.A).Get<IStringValueAnnotation>().Value;
            Assert.AreEqual("AAA", sval);
            InstrumentSettings.Current.Add(new RawSCPIInstrument());
            DataInterfaceTestClass testobj = new DataInterfaceTestClass();

            AnnotationCollection annotations = AnnotationCollection.Annotate(testobj, Array.Empty<object>());
            var disp = annotations.Get<DisplayAttribute>();
            Assert.IsNotNull(disp);
            var objectValue = annotations.Get<IObjectValueAnnotation>();
            Assert.AreEqual(testobj, objectValue.Value);

            var members = annotations.Get<IMembersAnnotation>();
            foreach(var member in members.Members)
            {
                Assert.AreEqual(member.ParentAnnotation, annotations);
                var mem = member.Get<IMemberAnnotation>();
                if(mem.Member.Name == nameof(DataInterfaceTestClass.EnumValues))
                {
                    var proxy = member.Get<IMultiSelectAnnotationProxy>();
                    var selected = proxy.SelectedValues.ToArray();
                    proxy.SelectedValues = member.Get<IAvailableValuesAnnotationProxy>().AvailableValues;
                }
                if(mem.Member.Name == nameof(DataInterfaceTestClass.SelectableValues))
                {

                }
                if(mem.Member.Name == nameof(DataInterfaceTestClass.FromAvailable))
                {
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    var available = avail.AvailableValues.ToArray();
                    
                    avail.SelectedValue = available[2];
                    var subavail = avail.SelectedValue.Get<IAvailableValuesAnnotationProxy>();
                    Assert.IsNull(subavail);
                    var unit = avail.SelectedValue.Get<UnitAttribute>();
                    Assert.IsNotNull(unit);

                    var str = avail.SelectedValue.Get<IStringValueAnnotation>();
                    Assert.IsNotNull(str);
                    var val = str.Value;
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.FromAvailable2))
                {
                    var subMembers = member.Get<IMembersAnnotation>();
                    var valueMember = subMembers.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(Enabled<int>.Value));
                    var unit = valueMember.Get<UnitAttribute>();
                    Assert.IsNotNull(unit);
                    var subunit = valueMember.Get<IAvailableValuesAnnotationProxy>().AvailableValues.FirstOrDefault().Get<UnitAttribute>();
                    Assert.AreEqual(unit, subunit);
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.TheSingleEnum))
                {
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    var aEnum = avail.AvailableValues.First();
                    var disp2 = aEnum.Get<IStringValueAnnotation>();
                    Assert.AreEqual("AAA", disp2.Value);
                }
                if(mem.Member.Name == nameof(DataInterfaceTestClass.Instruments))
                {
                    var prox = member.Get<IAvailableValuesAnnotationProxy>();
                    var instprox = prox.AvailableValues.FirstOrDefault();
                    var col = instprox.Get<ICollectionAnnotation>();
                    Assert.IsNull(col);
                }
            }
            annotations.Write(testobj);
        }

        [Test]
        public void SweepLoopProviderTest()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var sweep = new SweepLoop();
            var delay1 = new DelayStep();
            sweep.ChildTestSteps.Add(delay1);

            var annotation = AnnotationCollection.Annotate(sweep);
            var mem = annotation.Get<IMembersAnnotation>();

            var smem = mem.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == nameof(SweepLoop.SweepMembers));
            {
                var select = smem.Get<IMultiSelect>();
                var avail = smem.Get<IAvailableValuesAnnotation>();

                Assert.AreEqual(2, avail.AvailableValues.Cast<object>().Count()); // DelayStep only has on property.
                select.Selected = smem.Get<IAvailableValuesAnnotation>().AvailableValues;
                annotation.Write(sweep);
                annotation.Read(sweep);
            }

            var smem2 = mem.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == nameof(SweepLoop.SweepParameters));
            {
                var collection = smem2.Get<ICollectionAnnotation>();
                var new_element = collection.NewElement();
                var new_element_members = new_element.Get<IMembersAnnotation>();
                Assert.AreEqual(1, new_element_members.Members.Count());
                var delay_element = new_element_members.Members.FirstOrDefault();
                var delay_value = delay_element.Get<IStringValueAnnotation>();
                Assert.IsTrue(delay_value.Value.Contains("0.1 s")); // the default value for DelayStep is 0.1s.
                delay_value.Value = "0.1 s";
                annotation.Write(sweep);
                var firstDelay = sweep.SweepParameters.First().Values.ElementAt(0);
                Assert.AreEqual(0.1, (double)firstDelay);
                delay_value.Value = "0.01 s";
                annotation.Write(sweep);
                for(int i = 0; i < 4; i++)
                {
                    var new_element2 = collection.NewElement();
                    var new_element2_members = new_element2.Get<IMembersAnnotation>();
                    
                    var delay_element2 = new_element2_members.Members.FirstOrDefault();
                    var delay_value2 = delay_element2.Get<IStringValueAnnotation>();
                    // SweepLoop should copy the previous value for new rows.
                    Assert.IsTrue(delay_value2.Value.Contains("0.1 s"));
                    collection.AnnotatedElements = collection.AnnotatedElements.Append(new_element2).ToArray();
                }

            }
            annotation.Write();
            var rlistener = new OpenTap.Engine.UnitTests.PlanRunCollectorListener() { CollectResults = true };
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(sweep);
            var run = plan.Execute(new[] { rlistener });
            Assert.AreEqual(Verdict.NotSet, run.Verdict);

            Assert.AreEqual(6, rlistener.StepRuns.Count);
        }

        [Test]
        public void DataInterfaceProviderTest()
        {
            DataInterfaceTestClass testobj = new DataInterfaceTestClass();
            var _annotation = AnnotationCollection.Annotate(testobj);

            ITypeInfo desc = TypeInfo.GetTypeInfo(testobj);
            IMemberInfo mem = desc.GetMember("FromAvailable");
            var named = _annotation.Get<INamedMembersAnnotation>();

            var annotation = named.GetMember(mem);
            IAvailableValuesAnnotationProxy prov = annotation.Get<IAvailableValuesAnnotationProxy>();
            IObjectValueAnnotation val = annotation.Get<IObjectValueAnnotation>();
            val.Value = prov.AvailableValues.Cast<object>().FirstOrDefault();

            var display = annotation.Get<DisplayAttribute>();
            var unit = annotation.Get<UnitAttribute>();

            var mem2 = desc.GetMember("SimpleNumber");
            var annotation2 = named.GetMember(mem2);
            var unit2 = annotation2.Get<UnitAttribute>();
            var errors = annotation2.Get<ErrorAnnotation>();
            errors.Errors.Clear();
            var num = annotation2.Get<IStringValueAnnotation>();
            var currentVal = num.Value;
            num.Value = "4";
            try
            {
                num.Value = "asd";
            }catch(Exception e)
            {
                errors.Errors.Add(e.Message);
            }
            currentVal = num.Value;
            Assert.AreEqual(currentVal, "4 Hz");
            {
                var mem3 = desc.GetMember("AvailableNumbers");
                var annotation3 = named.GetMember(mem3);
                var numseq = annotation3.Get<IStringValueAnnotation>();
                var numbersstring = numseq.Value;
                numseq.Value = "1:100";
                annotation3.Write(testobj);
                Assert.AreEqual(100, testobj.AvailableNumbers.Count());
            }
            {
                var mem3 = desc.GetMember("ICanBeEnabled");
                var annotation3 = named.GetMember(mem3);
                var enabled = annotation3.Get<IAccessAnnotation>();
                Assert.IsTrue(enabled.IsReadOnly);
                Assert.IsFalse(enabled.IsVisible);

                var mem4 = desc.GetMember("ThingEnabled");
                var annotation4 = named.GetMember(mem4);
                annotation4.Get<IObjectValueAnnotation>().Value = true;
                annotation4.Write(testobj);
                //annotation4.Read(testobj);
                annotation3.Read(testobj);
                Assert.IsFalse(enabled.IsReadOnly);
                Assert.IsTrue(enabled.IsVisible);
            }

            {
                var mem3 = desc.GetMember("EnumValues");
                var annotation3 = named.GetMember(mem3);
                var select = annotation3.Get<IMultiSelect>();
                //select.Selected = select.AvailableValues.Cast<object>().ToArray();
                //annotation3.Write(testobj);
                //Assert.AreEqual(DataInterfaceTestClass.MultiSelectEnum.A | DataInterfaceTestClass.MultiSelectEnum.B| DataInterfaceTestClass.MultiSelectEnum.C, testobj.EnumValues);
            }

            {
                var mem3 = desc.GetMember("SelectableValues");
                var annotation3 = named.GetMember(mem3);
                var col = annotation3.Get<ICollectionAnnotation>();

                var annotated = col.AnnotatedElements.ToArray();
                Assert.AreEqual(2, annotated.Length);
                col.AnnotatedElements = new[] { col.NewElement(), col.NewElement(), col.NewElement(), col.NewElement(), col.NewElement(), col.NewElement() };
                annotated = col.AnnotatedElements.ToArray();
                var num5 = annotated[3];
                //var avail1 = num5.Get<IMultiSelect>().AvailableValues.Cast<object>().ToArray()[1];
                //num5.Get<IMultiSelect>().Selected = new[] { num5.Get<IMultiSelect>().AvailableValues.Cast<object>().ToArray()[1] };
                //var enumv1 = DataInterfaceTestClass.MultiSelectEnum.B;
                //annotation3.Write(testobj);
                //Assert.AreEqual(enumv1, testobj.SelectableValues[3]);
            }  

            
        }

        [Test]
        public void ExpandedDataInterfaceProviderTest()
        {
            var exp = new MyExpandedObject();
            exp.SetValue("_test_", 10);
            exp.SetValue("_test_array_", new double[] { 1, 2, 3, 4, 5, 6 });
            ITypeInfo desc = TypeInfo.GetTypeInfo(exp);
            foreach(var member in desc.GetMembers())
            {
                AnnotationCollection annotation = AnnotationCollection.Create(exp, member);
               foreach(var anot in annotation.Annotations)
                {
                    Debug.WriteLine("Member {0} Annotation: {1}", member.Name, anot);
                }
            }
        }

        [Test]
        public void MultiSelectAnnotationsInterfaceTest()
        {
            var steps = new[] { new DialogStep(), new DialogStep(), new DialogStep() };
            var typeinfo = TypeInfo.GetTypeInfo(steps[0]);
            foreach (var member in typeinfo.GetMembers()) {
                var mem = AnnotationCollection.Annotate(steps, member);
                var val = mem.Get<IMembersAnnotation>();
                Assert.IsNotNull(val);
            }

        }

        [Test]
        public void DisallowParentInput()
        {
            // #3666 Custom sweeping with parents having output properties.
            // If an input depends on an output from a parent, it will hang forever.
            // this test ensures that the output is not available for selection in the GUI.
            ReadInputStep step1 = new ReadInputStep();
            OutputParentStep theParent = new OutputParentStep();
            OutputParentStep step3 = new OutputParentStep();
            OutputParentStep step4 = new OutputParentStep();
            theParent.ChildTestSteps.Add(step1);
            theParent.ChildTestSteps.Add(step3);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(theParent);
            plan.ChildTestSteps.Add(step4);

            var annotation = AnnotationCollection.Annotate(step1);
            var inputAnnotation = annotation.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(ReadInputStep.Input));
            var avail = inputAnnotation.Get<IAvailableValuesAnnotation>();
            var setVal = avail as IObjectValueAnnotation;
            foreach (var val in avail.AvailableValues.Cast<object>().ToArray())
            {
                setVal.Value = val;
                annotation.Write(step1);
                Assert.IsFalse(step1.Input.Step == theParent);
            }
        }
    }
}

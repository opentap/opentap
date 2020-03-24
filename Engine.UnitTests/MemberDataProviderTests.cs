using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Text;
using OpenTap.Cli;
using System.Threading;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TypeDataTest
    {
        class ClassWithPropertyWithoutGetter
        {
            // previously reflecting this with TypeData would cause an exception to happen.
            public double MyValue { set { } }
        }
        [Test]
        public void ClassPropertyWithoutGetter()
        {
            var type = TypeData.FromType(typeof(ClassWithPropertyWithoutGetter));
            var members = type.GetMembers();
            Assert.IsTrue(members.Any(x => x.Name == nameof(ClassWithPropertyWithoutGetter.MyValue)));
        }

        [Test]
        public void SimpleDerivedTypesTest()
        {
            ITypeData baseType = TypeData.FromType(typeof(IResultListener));
            var types = TypeData.GetDerivedTypes(baseType);
            CollectionAssert.IsNotEmpty(types);
            CollectionAssert.AllItemsAreNotNull(types);
            CollectionAssert.AllItemsAreUnique(types);
            Assert.IsTrue(types.All(t => t.DescendsTo(baseType)));
        }

        public class TypeDataSearcherTestImpl : ITypeDataSearcher, ITypeDataProvider
        {
            public class MemberDataTestImpl : IMemberData
            {
                public ITypeData DeclaringType { get; set; }

                public ITypeData TypeDescriptor { get; set; }

                public bool Writable => true;

                public bool Readable => true;

                public IEnumerable<object> Attributes { get; set; }

                public string Name { get; set; }

                public object GetValue(object owner)
                {
                    return Value;
                }

                public void SetValue(object owner, object value)
                {
                    Value = value;
                }

                public static object Value { get; set; }
            }


            public class TypeDataTestImpl : ITypeData
            {
                public ITypeData BaseType { get; set; }

                public bool CanCreateInstance => Creator != null;

                public IEnumerable<object> Attributes => new object[] { new DisplayAttribute("unittesting") };

                public string Name { get; set; }

                public object CreateInstance(object[] arguments)
                {
                    return Creator.Invoke();
                }


                public IMemberData GetMember(string name)
                {
                    if (name == "Hello")
                        return HelloMember;
                    return null;
                }

                public IEnumerable<IMemberData> GetMembers()
                {
                    return new IMemberData[] { HelloMember };
                }

                private IMemberData HelloMember;
                private Func<object> Creator;
                public TypeDataTestImpl(string name, ITypeData baseType, Func<object> creator)
                {
                    Creator = creator;
                    Name = name;
                    BaseType = baseType;
                    HelloMember = new MemberDataTestImpl()
                    {
                        Name = "Hello",
                        Attributes = new object[] { new Cli.CommandLineArgumentAttribute("test") },
                        TypeDescriptor = TypeData.FromType(typeof(string)),
                        DeclaringType = this
                    };
                }
            }


            private static IEnumerable<ITypeData> _types = new List<ITypeData>
            {
                new TypeDataTestImpl( "UnitTestType", TypeData.FromType(typeof(IResultListener)),null),
                new TypeDataTestImpl( "UnitTestCliActionType", TypeData.FromType(typeof(ICliAction)),() => new SomeTestAction())
            };

            public static bool Enable = false;
            public IEnumerable<ITypeData> Types { get; private set; }

            public void Search()
            {
                if (Enable)
                    Types = _types;
                else
                    Types = null;
            }

            public double Priority => 1;

            public ITypeData GetTypeData(string identifier) => _types.FirstOrDefault(x => x.Name == identifier);

            public ITypeData GetTypeData(object obj)
            {
                if (obj is SomeTestAction)
                    return _types.Last();
                return null;
            }
        }
        
        interface NonDerivedInterface { }

        [Test]
        public void NonDerivedInterfaceDerivedTypes()
        {
            Assert.IsEmpty(TypeData.FromType(typeof(NonDerivedInterface)).DerivedTypes);
        } 

        [Test]
        public void ITypeDataSearcherTest()
        {
            TypeDataSearcherTestImpl.Enable = true;
            ITypeData baseType = TypeData.FromType(typeof(IResultListener));
            var types = TypeData.GetDerivedTypes(baseType);
            TypeDataSearcherTestImpl.Enable = false;
            CollectionAssert.IsNotEmpty(types);
            CollectionAssert.AllItemsAreNotNull(types);
            CollectionAssert.AllItemsAreUnique(types);
            Assert.IsTrue(types.All(t => t.DescendsTo(baseType)));
            Assert.IsTrue(types.Any(t => t.Name == "UnitTestType"));
        }

        [Browsable(false)]
        private class SomeTestAction : ICliAction
        {
            public static bool WasRun = false;
            public int Execute(CancellationToken cancellationToken)
            {
                WasRun = true;
                return 0;
            }
        }



        [Test]
        public void ITypeDataSearcherTest2()
        {
            TypeDataSearcherTestImpl.Enable = true;
            try
            {
                var actionTypes = TypeData.GetDerivedTypes<ICliAction>();
                Assert.IsTrue(actionTypes.Any(t => t.Name.EndsWith("UnitTestCliActionType")));
                SomeTestAction.WasRun = false;
                CliActionExecutor.Execute(new string[] { "unittesting", "--test", "hello" });
                Assert.IsTrue(SomeTestAction.WasRun);
                Assert.AreEqual("hello", TypeDataSearcherTestImpl.MemberDataTestImpl.Value);
            }
            finally
            {
                TypeDataSearcherTestImpl.Enable = false;
            }
        }

    }

    public interface IExpandedObject
    {
        object GetValue(string name);
        void SetValue(string name, object value);
        string[] Names { get; }
    }

    public class ExpandedMemberData : IMemberData
    {
        public ITypeData DeclaringType { get; set; }

        public IEnumerable<object> Attributes => Array.Empty<object>();

        public string Name { get; set; }

        public bool Writable => true;

        public bool Readable => true;

        public ITypeData TypeDescriptor
        {
            get
            {
                var desc = DeclaringType as ExpandedTypeInfo;
                var value = desc.Object.GetValue(Name);
                if (value == null) return null;
                return TypeData.GetTypeData(value);
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

    public class ExpandedTypeInfo : ITypeData
    {
        public ITypeData InnerDescriptor;
        public IExpandedObject Object;
        public ITypeDataProvider Provider { get; set; }

        public string Name => ExpandMemberDataProvider.exp + InnerDescriptor.Name;

        public IEnumerable<object> Attributes => InnerDescriptor.Attributes;

        public ITypeData BaseType => InnerDescriptor;

        public bool CanCreateInstance => InnerDescriptor.CanCreateInstance;

        public object CreateInstance(object[] arguments)
        {
            return InnerDescriptor.CreateInstance(arguments);
        }

        public IMemberData GetMember(string name)
        {
            return new ExpandedMemberData { DeclaringType = this, Name = name };
        }

        public IEnumerable<IMemberData> GetMembers()
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

    public class ExpandMemberDataProvider : ITypeDataProvider
    {
        public double Priority => 1;
        internal const string exp = "exp@";
        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if(tp != null)
                {
                    return new ExpandedTypeInfo() { InnerDescriptor = tp, Object = null, Provider = this };
                }
            }
            return null;
        }

        bool excludeSelf = false;
        public ITypeData GetTypeData(object obj)
        {
            if (obj is IExpandedObject exp && excludeSelf == false)
            {
                excludeSelf = true;
                var FoundType = TypeData.GetTypeData(obj);
                excludeSelf = false;
                if (FoundType != null)
                {
                    var expDesc = new ExpandedTypeInfo();
                    expDesc.InnerDescriptor = FoundType;
                    expDesc.Provider = this;
                    expDesc.Object = exp;
                    return expDesc;
                }
            }
            return null;
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


            Assert.AreEqual(4, cnt);
        }

        [Test]
        public void MemberDataTest()
        {
            var obj = new DelayStep();
            var td = TypeData.GetTypeData(obj);
            var td2 = TypeData.GetTypeData(obj);

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
        public void DerivedTypesTest()
        {
            var ilist = TypeData.FromType(typeof(System.Collections.IList));
            var cmplists = TypeData.FromType(typeof(ComponentSettings)).DerivedTypes.Where(x => x.DescendsTo(ilist));
            var reslists = cmplists.Where(x => x.ElementType.DescendsTo(typeof(IResource))).ToArray();
            var inst = reslists.Select(x => ComponentSettings.GetCurrent(x.Type));
            Assert.IsTrue(reslists.Contains(TypeData.FromType(typeof(InstrumentSettings))));
            Assert.IsFalse(reslists.Contains(TypeData.FromType(typeof(ConnectionSettings))));
            Assert.IsTrue(inst.Contains(InstrumentSettings.Current));
        }

        [Test]
        public void MemberData2Test()
        {
            var obj = new MyExpandedObject() { MyProp1 = 10 };
            obj.SetValue("asd", 10);

            var td = TypeData.GetTypeData(obj);
            Assert.IsNotNull(td);
            foreach (var mem in td.GetMembers())
            {
                Debug.WriteLine(string.Format("Member: {0} {1}", mem.Name, mem.GetValue(obj)));
            }

            var td2 = TypeData.GetTypeData("System.Windows.WindowState");

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
        public class DataInterfaceTestClass
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

            [AvailableValues(nameof(AvailableNumbers))]
            public List<double> SelectedMulti { get; set; } = new List<double> { 1, 2 };


            public IEnumerable<string> AvailableStrings => new[] { "hello", "world", "!" };
            [AvailableValues(nameof(AvailableStrings))]
            public List<string> SelectedMultiStrings { get; set; } = new List<string> { };


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

            public IEnumerable<SingleEnum> OneEnum => new[] { SingleEnum.A };
            [AvailableValues(nameof(OneEnum))]
            public SingleEnum AvailSingleEnum { get; set; } = SingleEnum.A;


            public List<ScpiInstrument> Instruments { get; set; } = new List<ScpiInstrument>();

            [AvailableValues(nameof(Instruments))]
            public IResource ResourceWithAvailable { get; set; }
            public double? NullableDouble { get; set; }
            public class Data1
            {
                public string X { get; set; }
            }

            List<Data1> list = new List<Data1> { new Data1 { X = "5" }, new Data1 { X = "1" } };

            [Browsable(true)]
            [XmlIgnore]
            public IReadOnlyList<Data1> Data1List
            {
                get => list.AsReadOnly();
                set { }
            }

            public List<Data1> Data2List { get; set; } = new List<Data1> { new Data1 { X = "1" } };

            public Data1[] DataArray { get; set; } = new Data1[] { new Data1 { X = "5" }, new Data1 { X = "Y" } };

            [DirectoryPath]
            public Enabled<string> EnabledDirectoryString { get; set; }

            [Display("Do Something")]
            [Browsable(true)]
            public void ButtonExample()
            {
                Clicks++;
            }

            public int Clicks;

            [Browsable(true)]
            public int MethodExample(int X, int Y)
            {
                return X + Y;
            }

        }


        [Test]
        public void DataInterfaceProviderTest2()
        {
            var sval = AnnotationCollection.Annotate(DataInterfaceTestClass.SingleEnum.A).Get<IStringValueAnnotation>().Value;
            Assert.AreEqual("AAA", sval);
            InstrumentSettings.Current.Add(new GenericScpiInstrument());
            DataInterfaceTestClass testobj = new DataInterfaceTestClass();

            AnnotationCollection annotations = AnnotationCollection.Annotate(testobj, Array.Empty<IAnnotation>());
            var disp = annotations.Get<DisplayAttribute>();
            Assert.IsNotNull(disp);
            var objectValue = annotations.Get<IObjectValueAnnotation>();
            Assert.AreEqual(testobj, objectValue.Value);

            var members = annotations.Get<IMembersAnnotation>();
            foreach (var member in members.Members)
            {
                Assert.AreEqual(member.ParentAnnotation, annotations);
                var mem = member.Get<IMemberAnnotation>();
                if (mem.Member.Name == nameof(DataInterfaceTestClass.EnumValues))
                {
                    var proxy = member.Get<IMultiSelectAnnotationProxy>();
                    var selected = proxy.SelectedValues.ToArray();
                    proxy.SelectedValues = member.Get<IAvailableValuesAnnotationProxy>().AvailableValues;
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.SelectableValues))
                {

                }

                if (mem.Member.Name == nameof(DataInterfaceTestClass.ButtonExample))
                {
                    var member2 = member.Get<IMethodAnnotation>();
                    member2.Invoke();
                    Assert.AreEqual(1, testobj.Clicks);
                    var access = member.Get<IAccessAnnotation>();
                    Assert.IsTrue(access.IsVisible);
                    Assert.IsTrue(access.IsReadOnly);
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.MethodExample))
                {
                    var methodAnnotation = member.Get<IMethodAnnotation>();
                    Assert.IsNull(methodAnnotation); // MethodExample has arguments. Should be used by IMethodAnnotation.
                    var del = (Delegate)member.Get<IObjectValueAnnotation>().Value;
                    int result = (int)del.DynamicInvoke(5, 10);
                    Assert.AreEqual(15, result);
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.EnabledDirectoryString))
                {
                    var enabledMembers = member.Get<IMembersAnnotation>().Members.ToArray();
                    Assert.AreEqual(2, enabledMembers.Length);
                    var valueMember = enabledMembers[1];
                    var directoryPathAttr = valueMember.Get<DirectoryPathAttribute>();
                    Assert.IsNotNull(directoryPathAttr);
                }

                if (mem.Member.Name == nameof(DataInterfaceTestClass.AvailSingleEnum))
                {
                    // #4702 : AvailableValuesAttribute should override enum behavior.
                    var avail = member.Get<IAvailableValuesAnnotation>();
                    Assert.AreEqual(1, avail.AvailableValues.Cast<object>().Count());
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.FromAvailable))
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
                    var val = valueMember.Get<IStringValueAnnotation>();
                    val.Value = "123";
                    var nowVal = val.Value;
                    annotations.Write();
                    annotations.Read();
                    Assert.AreEqual(nowVal, val.Value);

                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.TheSingleEnum))
                {
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    var aEnum = avail.AvailableValues.First();
                    var disp2 = aEnum.Get<IStringValueAnnotation>();
                    Assert.AreEqual("AAA", disp2.Value);
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.Instruments))
                {
                    var prox = member.Get<IAvailableValuesAnnotationProxy>();
                    var instprox = prox.AvailableValues.FirstOrDefault();
                    var col = instprox.Get<ICollectionAnnotation>();
                    Assert.IsNull(col);
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.NullableDouble))
                {
                    var num = member.Get<IStringValueAnnotation>();
                    Assert.IsNotNull(num);
                    num.Value = "5";
                    var val = member.Get<IObjectValueAnnotation>();
                    Assert.AreEqual(5, (double)val.Value);
                    num.Value = "";
                    Assert.IsNull(val.Value);

                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.Data1List))
                {
                    var prox = member.Get<ICollectionAnnotation>();
                    var annotated = prox.AnnotatedElements.ToArray();
                    member.Write();
                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.Data2List))
                {


                    var prox = member.Get<ICollectionAnnotation>();


                    void addElement(string text)
                    {
                        var newelem = prox.NewElement();
                        newelem.Get<IMembersAnnotation>().Members.FirstOrDefault().Get<IStringValueAnnotation>().Value = text;
                        prox.AnnotatedElements = prox.AnnotatedElements.Append(newelem);
                    }

                    member.Write();
                    prox.AnnotatedElements = prox.AnnotatedElements.Skip(1);
                    addElement("2");
                    addElement("3");
                    addElement("4");
                    member.Write();
                    var lst = testobj.Data2List;
                    Assert.IsTrue(lst[0].X == "2" && lst[1].X == "3" && lst[2].X == "4" && lst.Count == 3);

                    prox.AnnotatedElements = prox.AnnotatedElements.Reverse();
                    member.Write();
                    Assert.IsTrue(lst[2].X == "2" && lst[1].X == "3" && lst[0].X == "4" && lst.Count == 3);
                    var tmplst = prox.AnnotatedElements.ToList();
                    tmplst.RemoveAt(1);
                    prox.AnnotatedElements = tmplst;
                    member.Write();
                    Assert.IsTrue(lst[1].X == "2" && lst[0].X == "4" && lst.Count == 2);


                }
                if (mem.Member.Name == nameof(DataInterfaceTestClass.DataArray))
                {
                    var prox = member.Get<ICollectionAnnotation>();
                    var annotated = prox.AnnotatedElements.ToArray();
                    var newelem = prox.NewElement();
                    newelem.Get<IObjectValueAnnotation>().Value = new DataInterfaceTestClass.Data1();
                    prox.AnnotatedElements = prox.AnnotatedElements.Append(newelem);
                    try
                    {
                        member.Write();
                        Assert.Fail("This should have thrown an exception");
                    }
                    catch
                    {
                        // index out of bounds
                    }
                    member.Read();
                }

                if (mem.Member.Name == nameof(DataInterfaceTestClass.ResourceWithAvailable))
                {
                    // Verify that "AvailableValuesAnnotation" is chosen over "ResourceAnnotation".
                    Assert.AreEqual(2, member.Count(x => x is IAvailableValuesAnnotation));
                    var firstAvail = member.Get<IAvailableValuesAnnotation>().ToString();
                    var firstAvailName = "AvailableValuesAnnotation";
                    Assert.IsTrue(firstAvail.Contains(firstAvailName));
                    var secondAvail = member.GetAll<IAvailableValuesAnnotation>().Last();
                    var secondAvailName = "ResourceAnnotation";
                    Assert.IsTrue(secondAvail.ToString().Contains(secondAvailName));
                }

                if (mem.Member.Name == nameof(DataInterfaceTestClass.SelectedMulti))
                {
                    var proxy = member.Get<IMultiSelectAnnotationProxy>();
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    proxy.SelectedValues = avail.AvailableValues;
                    annotations.Write(testobj);
                    Assert.IsTrue(testobj.SelectedMulti.ToHashSet().SetEquals(testobj.AvailableNumbers));
                    proxy.SelectedValues = Array.Empty<AnnotationCollection>();
                    annotations.Write(testobj);
                    Assert.AreEqual(0, testobj.SelectedMulti.Count);
                }

                if (mem.Member.Name == nameof(DataInterfaceTestClass.SelectedMultiStrings))
                {
                    var proxy = member.Get<IMultiSelectAnnotationProxy>();
                    var avail = member.Get<IAvailableValuesAnnotationProxy>();
                    proxy.SelectedValues = avail.AvailableValues;
                    annotations.Write(testobj);
                    Assert.IsTrue(testobj.SelectedMultiStrings.ToHashSet().SetEquals(testobj.AvailableStrings));
                    proxy.SelectedValues = Array.Empty<AnnotationCollection>();
                    annotations.Write(testobj);
                    Assert.AreEqual(0, testobj.SelectedMulti.Count);
                }
            }
            annotations.Write(testobj);

        }

        [Flags]
        public enum FlagTestEnum
        {
            A = 1,
            B = 2,
            C = 4
        }
        
        public class Delay2Step : TestStep
        {
            [Display("Time Delay")]
            public double TimeDelay { get; set; }

            [Display("Time Delay")]
            public double TimeDelay2 { get; set; }
            
            [AvailableValues(nameof(AvailableValues))]
            public string SelectedValue { get; set; }
            public IEnumerable<string> AvailableValues => AvailableValuesField;
            public IEnumerable<string> AvailableValuesField = new string[0] ;

            public FlagTestEnum SelectedValues { get; set; } = FlagTestEnum.A;

            public FlagTestEnum ExpectedValues = FlagTestEnum.A | FlagTestEnum.B | FlagTestEnum.C; 

            public override void Run()
            {
                if (TimeDelay != TimeDelay2)
                    throw new Exception($"{nameof(TimeDelay)} != {nameof(TimeDelay2)}");
                if(SelectedValues != ExpectedValues) //SelectedValues must be set to the ExpectedValues.
                    throw new Exception("Expected SelectedValues to be set to all AvailableValues");
            }
        }

        /// <summary>
        /// Delay step that is quick to run.
        /// </summary>
        class FakeDelayStep : DelayStep
        {
            public override void Run()
            {
                
            }
        }
        

        [Test]
        public void SweepLoopProviderTest()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var sweep = new SweepLoop();
            var delay1 = new FakeDelayStep();
            sweep.ChildTestSteps.Add(delay1);

            var delay2 = new Delay2Step() {AvailableValuesField = new[] { "A", "B", "C" }};
            sweep.ChildTestSteps.Add(delay2);

            var delay3 = new Delay2Step() { AvailableValuesField = new[] { "A", "B", "D" }};
            sweep.ChildTestSteps.Add(delay3);

            var annotation = AnnotationCollection.Annotate(sweep);

            var smem = annotation.GetMember(nameof(SweepLoop.SweepMembers));
            {
                {
                    var select = smem.Get<IMultiSelect>();
                    var avail = smem.Get<IAvailableValuesAnnotation>();

                    select.Selected = new object[] { };
                    annotation.Write(sweep);
                    annotation.Read(sweep);
                    Assert.AreEqual(0, select.Selected.Cast<object>().Count());

                    select.Selected = new object[] { avail.AvailableValues.Cast<object>().First() };
                    annotation.Write(sweep);
                    annotation.Read(sweep);
                    Assert.AreEqual(3, select.Selected.Cast<object>().Count());

                    Assert.AreEqual(4, avail.AvailableValues.Cast<object>().Count()); // DelayStep only has on property.
                    select.Selected = smem.Get<IAvailableValuesAnnotation>().AvailableValues;
                    annotation.Write(sweep);
                    annotation.Read(sweep);
                }
            }

            var smem2 = annotation.GetMember(nameof(SweepLoop.SweepParameters));
            {
                var collection = smem2.Get<ICollectionAnnotation>();
                {
                    var new_element = collection.NewElement();
                    collection.AnnotatedElements = collection.AnnotatedElements.Append(new_element).ToArray();
                    var new_element_members = new_element.Get<IMembersAnnotation>();
                    var members = new_element_members.Members.ToArray();
                    Assert.AreEqual(4, members.Length);

                    var enabled_element = members[0];
                    Assert.IsTrue(enabled_element.Get<IMemberAnnotation>().Member.Name == "Enabled");
                    Assert.IsTrue((bool) enabled_element.Get<IObjectValueAnnotation>().Value == true);

                    var delay_element = members[1];
                    var delay_value = delay_element.Get<IStringValueAnnotation>();
                    Assert.IsTrue(delay_value.Value.Contains("0.1 s")); // the default value for DelayStep is 0.1s.
                    delay_value.Value = "0.1 s";

                    var selected_element = members[2];
                    var available_for_Select = selected_element.Get<IAvailableValuesAnnotationProxy>();
                    // ~Since they only have two available values in common, the list should only contain those two elements.~
                    // Actually since this behavior conflicts with Enabled<> behavior, it just has the available values from the first one.
                    Assert.IsTrue(available_for_Select.AvailableValues.Cast<object>().Count() == 3);

                    annotation.Write();
                }
                var firstDelay = sweep.SweepParameters.First().Values.ElementAt(0);
                var delay_value2 = collection.AnnotatedElements.First().Get<IMembersAnnotation>().Members.ElementAt(1)
                    .Get<IStringValueAnnotation>();
                Assert.AreEqual(0.1, (double)firstDelay);
                delay_value2.Value = "0.01 s";
                annotation.Write();
                for (int i = 0; i < 5; i++)
                {
                    var new_element2 = collection.NewElement();
                    collection.AnnotatedElements = collection.AnnotatedElements.Append(new_element2).ToArray();
                    var new_element2_members = new_element2.Get<IMembersAnnotation>().Members.ToArray();

                    var enabled_element2 = new_element2_members[0];

                    Assert.IsTrue(enabled_element2.Get<IMemberAnnotation>().Member.Name == "Enabled");
                    Assert.IsTrue((bool)enabled_element2.Get<IObjectValueAnnotation>().Value == true);
                    if (i == 2 || i == 3)
                    {
                        enabled_element2.Get<IObjectValueAnnotation>().Value = false;
                    }

                    var delay_element2 = new_element2_members.First(x => x.Get<IMemberAnnotation>().Member.Name == "DelaySecs");
                    var delay_value3 = delay_element2.Get<IStringValueAnnotation>();
                    // SweepLoop should copy the previous value for new rows.
                    Assert.IsTrue(delay_value3.Value.Contains("0.01 s"));
                }
        
                foreach (var elem in collection.AnnotatedElements)
                {
                    var selected_values_annotation = elem.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member.Name == "SelectedValues");
                    var sel = selected_values_annotation.Get<IMultiSelectAnnotationProxy>();
                    Assert.IsNotNull(sel);
                    var sel_avail = selected_values_annotation.Get<IAvailableValuesAnnotationProxy>();
                    sel.SelectedValues = sel_avail.AvailableValues;
                }
                
                annotation.Write();
                {
                    // remove an additional disable row.
                    // this verifies that removing rows works.
                    var lst = collection.AnnotatedElements.ToList();
                    lst.RemoveAt(3);
                    collection.AnnotatedElements = lst;
                    annotation.Write();
                }
                annotation.Read();
                {
                    var elem = collection.AnnotatedElements.First();
                    var mem2 = elem.GetMember("DelaySecs");
                    mem2.Get<IStringValueAnnotation>().Value = "1.123 s";
                    annotation.Write();
                    annotation.Read();
                    var nowvalue = mem2.Get<IStringValueAnnotation>().Value;
                    Assert.AreEqual("1.123 s", nowvalue);
                }
            }

            var rlistener = new PlanRunCollectorListener() { CollectResults = true };
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(sweep);
            var run = plan.Execute(new[] { rlistener });
            Assert.AreEqual(Verdict.NotSet, run.Verdict);

            // one of the sweep rows was disabled.
            Assert.AreEqual(13, rlistener.StepRuns.Count);

            { // verify that when child steps are deleted, the list is updated. 
                sweep.ChildTestSteps.Remove(delay2);
                sweep.ChildTestSteps.Remove(delay3);
                annotation.Read();
                var av = smem.Get<IAvailableValuesAnnotation>().AvailableValues.Cast<object>().ToList();
                Assert.AreEqual(2, av.Count); // Select All + Time Delay.
            }
        }
[Test]
        public void SweepLoopRangeCheck()
        {
            var plan = new TestPlan();
            var sweep = new SweepLoopRange();
            var delay = new DelayStep();
            var delay2 = new Delay2Step();
            sweep.ChildTestSteps.Add(delay);
            sweep.ChildTestSteps.Add(delay2);
            plan.ChildTestSteps.Add(sweep);

            var a = AnnotationCollection.Annotate(sweep);
            var member = TypeData.GetTypeData(sweep).GetMember(nameof(SweepLoopRange.SweepProperties));
            var b = a.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member == member);
            var proxy = b.Get<IMultiSelectAnnotationProxy>();
            var avail = b.Get<IAvailableValuesAnnotationProxy>();
            proxy.SelectedValues = avail.AvailableValues;
            a.Write();
            Assert.AreEqual(3, sweep.SweepProperties.Count);
            sweep.ChildTestSteps.Remove(delay); // this should cause the SweepProperty to be removed.
            var delayStepType = TypeData.GetTypeData((delay));
            Assert.AreEqual(2, sweep.SweepProperties.Count);
            foreach (var mem in sweep.SweepProperties)
            {
                Assert.AreNotEqual(delayStepType, mem.DeclaringType);
            }
        }

        [Test]
        public void DataInterfaceProviderTest()
        {
            DataInterfaceTestClass testobj = new DataInterfaceTestClass();
            dataInterfaceProviderInnerTest(testobj);
        }

        void dataInterfaceProviderInnerTest(object testobj)
        {

            var _annotation = AnnotationCollection.Annotate(testobj);

            ITypeData desc = TypeData.GetTypeData(testobj);
            IMemberData mem = desc.GetMember("FromAvailable");
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
            var num = annotation2.Get<IStringValueAnnotation>();
            var currentVal = num.Value;
            num.Value = "4";
            try
            {
                num.Value = "asd";
            }
            catch (Exception)
            {

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
                Assert.AreEqual(100, ((System.Collections.IEnumerable)mem3.GetValue(testobj)).Cast<object>().Count());
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
            ITypeData desc = TypeData.GetTypeData(exp);
            foreach (var member in desc.GetMembers())
            {
                AnnotationCollection annotation = AnnotationCollection.Create(exp, member);
                foreach (var anot in annotation)
                {
                    Debug.WriteLine("Member {0} Annotation: {1}", member.Name, anot);
                }
            }
        }

        [Test]
        public void MultiSelectAnnotationsInterfaceTest()
        {
            var steps = new List<DialogStep> { new DialogStep { UseTimeout = false }, new DialogStep { UseTimeout = false }, new DialogStep { UseTimeout = true } };

            var mem = AnnotationCollection.Annotate(steps);
            var val = mem.Get<IMembersAnnotation>();
            Assert.IsNotNull(val);

            var useTimeoutMember = mem.GetMember(nameof(DialogStep.UseTimeout));
            Assert.IsNull(useTimeoutMember.Get<IObjectValueAnnotation>().Value); // since one is different, this should be null.
            useTimeoutMember.Get<IObjectValueAnnotation>().Value = true; // set all UseTimeout to true.

            var messageMember = mem.GetMember(nameof(DialogStep.Message));
            string theMessage = "My message";
            messageMember.Get<IStringValueAnnotation>().Value = theMessage;
            mem.Write();
            foreach (var step in steps)
            {
                Assert.IsTrue(string.Compare(theMessage, step.Message) == 0);
                Assert.IsTrue(step.UseTimeout);
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
            var inputAnnotation = annotation.GetMember(nameof(ReadInputStep.Input));
            var avail = inputAnnotation.Get<IAvailableValuesAnnotation>();
            var setVal = avail as IAvailableValuesSelectedAnnotation;
            foreach (var val in avail.AvailableValues.Cast<object>().ToArray())
            {
                setVal.SelectedValue = val;
                annotation.Write(step1);
            }

            step1.Input.Step = theParent;
            var run = plan.Execute();

        }

        [Test]
        public void SweepLoopAnnotation()
        {
            var sweep = new SweepLoop();
            var delay = new DelayStep() { DelaySecs = 0.001 };
            var verdict = new IfStep();
            sweep.ChildTestSteps.Add(delay);
            sweep.ChildTestSteps.Add(verdict);
            sweep.SweepParameters.Add(new SweepParam(new[] { MemberData.Create(typeof(DelayStep).GetProperty(nameof(DelayStep.DelaySecs))) }));
            sweep.SweepParameters.Add(new SweepParam(new[] { MemberData.Create(typeof(IfStep).GetProperty(nameof(IfStep.InputVerdict))) }));
            double[] values = new double[] { 0.01, 0.02, 0.03 };
            sweep.SweepParameters[0].Resize(values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                sweep.SweepParameters[0].Values.SetValue(values[i], i);
            }

            sweep.OnDeserialized(); // force sanitize sweep values.

            for (int i = 1; i < values.Length; i++)
            {
                var val = sweep.SweepParameters[1].Values.GetValue(i);
                var val1 = sweep.SweepParameters[1].Values.GetValue(0);
                Assert.AreNotSame(val, val1);
                Assert.IsNull(((IInput)val1).Step);
            }

            var swep = AnnotationCollection.Annotate(sweep);

            swep.Write();
            var sweepMembers = swep.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member.Name == nameof(SweepLoop.SweepMembers));
            var availableValues = sweepMembers.Get<IAvailableValuesAnnotation>().AvailableValues.OfType<IMemberData>().ToArray();
            // DelaySecs, InputVerdict, TargetVerdict, Action. -> Verify that TestStep.Name or Enabled is not in there.
            Assert.AreEqual(4, availableValues.Length);
            Assert.IsFalse(availableValues.Contains(TypeData.FromType(typeof(TestStep)).GetMember(nameof(TestStep.Name))));
            Assert.IsFalse(availableValues.Contains(TypeData.FromType(typeof(TestStep)).GetMember(nameof(TestStep.Enabled))));
            Assert.IsTrue(availableValues.Contains(TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs))));
            Assert.IsTrue(availableValues.Contains(TypeData.FromType(typeof(IfStep)).GetMember(nameof(IfStep.InputVerdict))));
            Assert.IsTrue(availableValues.Contains(TypeData.FromType(typeof(IfStep)).GetMember(nameof(IfStep.TargetVerdict))));
            Assert.IsTrue(availableValues.Contains(TypeData.FromType(typeof(IfStep)).GetMember(nameof(IfStep.Action))));

            var sweepParameters = swep.GetMember(nameof(SweepLoop.SweepParameters));
            var elements = sweepParameters.Get<ICollectionAnnotation>().AnnotatedElements;
            int i2 = 0;
            foreach (var elem in elements)
            {
                {
                    var delayMember = elem.GetMember(nameof(DelayStep.DelaySecs));
                    var currentValue = (double)delayMember.Get<IObjectValueAnnotation>().Value;
                    Assert.AreEqual(values[i2], currentValue);
                }
                {
                    var ifMember = elem.GetMember(nameof(IfStep.InputVerdict));
                    var avail = ifMember.Get<IAvailableValuesAnnotationProxy>();
                    avail.SelectedValue = avail.AvailableValues.Last();
                }
                i2++;
            }
            swep.Write();
            for (int i = 1; i < values.Length; i++)
            {
                var val = sweep.SweepParameters[1].Values.GetValue(i);
                var val1 = sweep.SweepParameters[1].Values.GetValue(0);

                Assert.AreNotSame(val, val1);
                Assert.IsNotNull(((IInput)val1).Step);
            }
        }

        public class EnabledVirtualBaseClass : TestStep
        {

            public virtual Enabled<double> EnabledValue { get; set; } = new Enabled<double>();

            public override void Run()
            {
            }
        }

        public class EnabledVirtualClass : EnabledVirtualBaseClass
        {
            public override Enabled<double> EnabledValue { get; set; }
        }

        /// <summary>
        /// This shows issue #4666 related to Enabled properties that are null and multi selected.
        /// </summary>
        [Test]
        public void AnnotationVirtualEnabledProperty()
        {
            var obj = new EnabledVirtualClass();
            var obj2 = new EnabledVirtualClass();

            var annotation = AnnotationCollection.Annotate(new[] { obj, obj2 });
            annotation.Read();
            var enabledValueAnnotation = annotation.GetMember("EnabledValue");
            var val = enabledValueAnnotation.Get<IObjectValueAnnotation>().Value;
            var members2 = enabledValueAnnotation.Get<IMembersAnnotation>();
            var mems = members2.Members.ToArray();
            mems[0].Get<IObjectValueAnnotation>().Value?.ToString();
            mems[1].Get<IObjectValueAnnotation>().Value?.ToString();

            mems[0].Write();
            mems[1].Write();
        }
        class InputAnnotationStep : TestStep
        {
            public Input<Verdict> Input { get; set; } = new Input<Verdict>();
            public override void Run() { }
        }
        [Test]
        public void AnnotatedInputTest()
        {
            var plan = new TestPlan();
            plan.Steps.Add(new DelayStep());
            InputAnnotationStep step;
            plan.Steps.Add(step = new InputAnnotationStep());

            var annotation = AnnotationCollection.Annotate(step);
            var inputMember = annotation.Get<INamedMembersAnnotation>().GetMember(TypeData.FromType(typeof(InputAnnotationStep)).GetMember(nameof(InputAnnotationStep.Input)));
            var proxy = inputMember.Get<IAvailableValuesAnnotationProxy>();
            proxy.SelectedValue = proxy.AvailableValues.Skip(1).FirstOrDefault(); //skip 'None'.
            annotation.Write(step);

            Assert.IsTrue(step.Input.Step == plan.Steps[0]);
        }

        [Test]
        public void IfVerdictReadError()
        {
            TestPlan plan = new TestPlan();
            IfStep ifStep = new IfStep();
            plan.ChildTestSteps.Add(ifStep);
            var annotations = AnnotationCollection.Annotate(ifStep);
            if (annotations.Get<IMembersAnnotation>() is IMembersAnnotation members)
            {
                var ifAnnotations = members.Members.FirstOrDefault();
                if (ifAnnotations.Get<IMembersAnnotation>() is IMembersAnnotation thisIsGoingToCrash)
                {
                    var err = thisIsGoingToCrash.Members;
                    err.FirstOrDefault().Read();
                }
                ifAnnotations.Read();
            }
        }


        public class EmbeddedTest
        {
            // this should give EmbeddedTest all the virtual properties of DataInterfaceTestClass.
            [Display("Embedded Things")]
            [EmbedProperties(PrefixPropertyName = false)]
            public DataInterfaceTestClass EmbeddedThings { get; set; } = new DataInterfaceTestClass();
        }

        [Test]
        public void EmbeddedPropertiesReflectionAndAnnotation()
        {
            var obj = new EmbeddedTest();
            obj.EmbeddedThings.SimpleNumber = 3145.2;
            var type = TypeData.GetTypeData(obj);
            var display = type.GetMembers().First().GetDisplayAttribute();
            Assert.IsTrue(display.Group[0] == "Embedded Things"); // test that the name gets transformed.
            var emba = type.GetMember(nameof(DataInterfaceTestClass.SimpleNumber));
            Assert.AreEqual(obj.EmbeddedThings.SimpleNumber, (double)emba.GetValue(obj));
            var embb = type.GetMember(nameof(DataInterfaceTestClass.FromAvailable));
            Assert.AreEqual(obj.EmbeddedThings.FromAvailable, (double)embb.GetValue(obj));

            var annotated = AnnotationCollection.Annotate(obj);
            annotated.Read();
            var same = annotated.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == emba);
            Assert.AreEqual("3145.2 Hz", same.Get<IStringValueAnnotation>().Value);
        }

        [Test]
        public void EmbeddedPropertiesReflectionAndAnnotationBig()
        {
            dataInterfaceProviderInnerTest(new EmbeddedTest());
        }

        [Test]
        public void EmbeddedPropertiesSerialization()
        {
            var ts = new TapSerializer();
            var obj = new EmbeddedTest();
            obj.EmbeddedThings.SimpleNumber = 500;
            var str = ts.SerializeToString(obj);
            obj = (EmbeddedTest)ts.DeserializeFromString(str);
            Assert.AreEqual(500, obj.EmbeddedThings.SimpleNumber);
        }

        public class EmbA
        {
            public double X { get; set; }
        }

        public class EmbB
        {
            [EmbedProperties(PrefixPropertyName = false)]
            public EmbA A { get; set; } = new EmbA();

            [EmbedProperties(Prefix = "A")]
            public EmbA A2 { get; set; } = new EmbA();
        }

        public class EmbC
        {
            [EmbedProperties]
            public EmbB B { get; set; } = new EmbB();
        }

        [Test]
        public void NestedEmbeddedTest()
        {
            var c = new EmbC();
            c.B.A2.X = 5;
            c.B.A.X = 35;
            var embc_type = TypeData.GetTypeData(c);

            var members = embc_type.GetMembers();
            Assert.AreEqual(2, members.Count());

            var mem = embc_type.GetMember("B.A.X");
            
            Assert.AreEqual(c.B.A2.X, (double)mem.GetValue(c));
            mem.SetValue(c, 20);
            Assert.AreEqual(c.B.A2.X, 20.0);

            var mem2 = embc_type.GetMember("B.X");
            Assert.AreEqual(c.B.A.X, (double)mem2.GetValue(c));

            var ts = new TapSerializer();
            var str = ts.SerializeToString(c);
            var c2 = (EmbC)ts.DeserializeFromString(str);
            Assert.AreNotEqual(c, c2);
            Assert.AreEqual(c.B.A.X, c2.B.A.X);

        }

        public class EmbD
        {
            [EmbedProperties]
            public EmbD B { get; set; }
            public int X { get; set; }
        }

        [Test]
        public void RecursiveEmbeddedTest()
        {
            var d = new EmbD();
            var t = TypeData.GetTypeData(d);
            var members = t.GetMembers(); // this will throw a StackOverflowException if the Embedding does not take care of the potential problem.
            Assert.AreEqual(2, members.Count());
        }

        interface IReferencingStep : ITestStep
        {
            IReferencingStep ReferencedStep { get; set; }
        }
        class ReferencingStep : TestStep, IReferencingStep
        {
            public override void Run()
            {
                throw new NotImplementedException();
            }

            public IReferencingStep ReferencedStep { get; set; }
        }

        [Test]
        public void ReferencedStepAnnotation()
        {
            var step1 = new ReferencingStep();
            var step2 = new DelayStep();
            var step3 = new ReferencingStep();
            var plan = new TestPlan();
            var member = TypeData.FromType(typeof(ReferencingStep)).GetMember(nameof(ReferencingStep.ReferencedStep));
            plan.ChildTestSteps.AddRange(new ITestStep[] { step1, step2, step3 });
            var a = AnnotationCollection.Annotate(step3);
            var avail = a.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member == member).Get<IAvailableValuesAnnotation>();
            var values = avail.AvailableValues;
            if(values.Cast<ITestStep>().Any(x => (x is IReferencingStep) == false))
            {
                Assert.Fail("List should only contain " + nameof(IReferencingStep));
            }
            Assert.AreEqual(plan.ChildTestSteps.Count(x => x is ReferencingStep), values.Cast<object>().Count());
        }

        public class StepMultiSelectStep : TestStep
        {
            public DelayStep DelayStep { get; set; }
            public List<ITestStep> DelaySteps { get; set; } = new List<ITestStep>();
            public override void Run()
            {
                
            }
        }

        [Test]
        public void StepListMultiSelect()
        {
            var tp = new TestPlan();
            tp.ChildTestSteps.Add(new DelayStep() { Name = "Delay 1" });
            tp.ChildTestSteps.Add(new DelayStep() { Name = "Delay 2" });
            StepMultiSelectStep step1 = new StepMultiSelectStep();
            tp.ChildTestSteps.Add(step1);

            var a = AnnotationCollection.Annotate(step1);
            var delaySteps = a.GetMember("DelaySteps");
            delaySteps.Get<IMultiSelectAnnotationProxy>().SelectedValues = delaySteps.Get<IAvailableValuesAnnotationProxy>().AvailableValues;
            a.Write();
            Assert.AreEqual(3, step1.DelaySteps.Distinct().Count());

            var serializer = new TapSerializer();
            var testplanxml = serializer.SerializeToString(tp);
            Assert.IsTrue(testplanxml.Contains("<DelayStep>")); // ensure that it doesnt say type="" in the by-reference elements in the list.
            var tp2 = (TestPlan)serializer.DeserializeFromString(testplanxml);
            var d1 = tp2.ChildTestSteps[0];
            var d2 = tp2.ChildTestSteps[1];
            var m = (StepMultiSelectStep)tp2.ChildTestSteps[2];
            Assert.IsTrue(m.DelaySteps[0] == d1);
            Assert.IsTrue(m.DelaySteps[1] == d2);
        }
        
        /// <summary>  Class for testing embedding the same class twice and using attributes. </summary>
        public class EmbeddedTest2
        {
            
            [EmbedProperties(PrefixPropertyName = true, Prefix = "Emba")]
            [Display("A")]
            public DataInterfaceTestClass EmbeddedThingsA { get; private set; } = new DataInterfaceTestClass();
            
            
            [EmbedProperties(PrefixPropertyName = true, Prefix = "Embb")]
            [Display("B")]
            public DataInterfaceTestClass EmbeddedThingsB { get; private set; } = new DataInterfaceTestClass();
        }

        
        /// <summary>
        /// This verifies that attributes sensitive to property names gets properly transformed.
        /// This test verifies AvailableValuesAttribute and EnabledIfAttribute.
        /// </summary>
        [Test]
        public void EmbeddedPropertiesReflectionAndAnnotation2()
        { 
            
            var obj = new EmbeddedTest2();
            var td = TypeData.GetTypeData(obj);
            var annotated = AnnotationCollection.Annotate(obj);
            annotated.Read();
            var same = annotated.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member.Name == "Emba.FromAvailable");
            var availableValues = same.Get<IAvailableValuesAnnotation>().AvailableValues.Cast<object>().ToArray(); // this will most likely fail.
            var prox = same.Get<IAvailableValuesAnnotationProxy>();
            prox.SelectedValue = prox.AvailableValues.Last();
            annotated.Write();
            
            var enabledAnnotation = annotated.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member.Name == "Embb.ICanBeEnabled");
            Assert.IsFalse(enabledAnnotation.Get<IAccessAnnotation>().IsVisible);
            var enablingAnnotation = annotated.Get<IMembersAnnotation>().Members.First(x => x.Get<IMemberAnnotation>().Member.Name == "Embb.ThingEnabled");
            enablingAnnotation.Get<IObjectValueAnnotation>().Value = true;
            annotated.Write();
            annotated.Read();
            Assert.IsTrue(enabledAnnotation.Get<IAccessAnnotation>().IsVisible);
        }

        public class EmbeddedValidatingObject : ValidatingObject
        {
            public double[] TestValues => new double[] {1, 2, 3, 4, 5};
            [AvailableValues(nameof(TestValues))]
            public double Test { get; set; }

            public const string ErrorMessage = "Test must be positive.";
            public EmbeddedValidatingObject()
            {
                Rules.Add(() => Test > 0, ErrorMessage, nameof(Test));
            }
        }

        public class EmbeddedValidatingObjectTestClass : TestStep
        {
            [EmbedProperties]
            public EmbeddedValidatingObject Embedded { get; set; } = new EmbeddedValidatingObject();
            
            public EmbeddedValidatingObject OtherNonEmbedded { get; set; }
            
            public double Test2 { get; set; }
            public override void Run()
            {
                
            }
        }

        [Test]
        [TestCase(-0.0, EmbeddedValidatingObject.ErrorMessage)]
        [TestCase(1.0, "")]
        public void EmbeddedValidatingObjectTest(double value, string error)
        {
            // try to get the error message
            EmbeddedValidatingObjectTestClass val = new EmbeddedValidatingObjectTestClass();
            val.Embedded.Test = value;
            var a = AnnotationCollection.Annotate(val).Get<IMembersAnnotation>().Members;
            StringBuilder errors = new StringBuilder();
            foreach (var member in a)
            {
                if (member.Get<IMemberAnnotation>().Member.Name != "Embedded.Test") continue;
                
                foreach (var att in member.OfType<IErrorAnnotation>())
                {
                    foreach (var line in att.Errors)
                        errors.AppendLine(line);
                }
            } 
            Assert.IsTrue(errors.ToString().Contains(error));
        }

        class BlankStep : TestStep
        {
            public override void Run()
            {
                
            }
        }

        [Test]
        public void AnnotateBlankStep()
        {
            var step = new BlankStep();
            var a = AnnotationCollection.Annotate(step);
            var members = a.Get<IMembersAnnotation>().Members;
            var members2 = members.ToDictionary(x => x.Get<IMemberAnnotation>().Member.Name);
            var v = members2["BreakConditions"];

        }
    }
}


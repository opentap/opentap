using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Package;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SerializerTests
    {
        public class TestPlanWithMetaData : TestPlan
        {
            [MetaData(Name= "X Setting")]
            public string X { get; set; }
            
            [MetaData]
            public string Y { get; set; }
        } 
        [Test]
        public void TestSerializeTestPlanMetaData()
        {
            var plan = new TestPlanWithMetaData();
            var xml = plan.SerializeToString();
            var xdoc = XDocument.Parse(xml);
            Assert.AreEqual("X Setting", xdoc.Root.Element("X").Attribute("Metadata").Value);
            Assert.AreEqual("Y", xdoc.Root.Element("Y").Attribute("Metadata").Value);
        }
        public class PackageVersionTestStep : TestStep
        {
            public PackageVersion[] PackageVersion { get; set; }
            public override void Run()
            {
                
            }
        }

        [Test]
        public void TestPackageVersionLicenseSerializer()
        {
            var packageVersion = new PackageVersion("pkg", SemanticVersion.Parse("1.0.0"), "Linux", CpuArchitecture.AnyCPU, DateTime.Now, 
                new List<string>()
                {
                    "License 1",
                    "License 2"
                });
            var ser = new TapSerializer();
            var str = ser.SerializeToString(packageVersion);
            var des = ser.DeserializeFromString(str);
            
            Assert.AreEqual(packageVersion, des);
            CollectionAssert.AreEqual(packageVersion.Licenses, ((des as PackageVersion)!).Licenses);
        }

        [Test]
        public void SerializeInputInTestPlanReferenceTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                TestTraceListener tapTraceListener = new TestTraceListener();
                Log.AddListener(tapTraceListener);
                
                const string PlanName = "ParameterizedIfStep.TapPlan";
                { // Create child plan
                    var childPlan = new TestPlan();
                    var ifstep = new IfStep();
                    childPlan.ChildTestSteps.Add(ifstep);
                    var a = AnnotationCollection.Annotate(ifstep);
                    var m = a.GetMember(nameof(ifstep.InputVerdict));
                    var items = m.Get<MenuAnnotation>().MenuItems.ToArray();
                    var icons = items.ToLookup(item =>
                        item.Get<IIconAnnotation>()?.IconName ?? "");
                    var parameterizeOnTestPlan = icons[IconNames.ParameterizeOnTestPlan].First();
                    var method = parameterizeOnTestPlan.Get<IMethodAnnotation>();
                    method.Invoke();
                
                    childPlan.Save(PlanName);
                }

                tapTraceListener.Flush();
                Assert.IsEmpty(tapTraceListener.ErrorMessage);
                
                // Try to load the child plan
                {
                    var plan = new TestPlan()
                    {
                        ChildTestSteps =
                        {
                            new TestPlanReference()
                            {
                                Filepath = new MacroString() { Text = PlanName }
                            }
                        }
                    };
                    
                    tapTraceListener.Flush();
                    Assert.IsEmpty(tapTraceListener.ErrorMessage);

                    var a = AnnotationCollection.Annotate(plan.ChildTestSteps[0]);
                    var m = a.Get<IMembersAnnotation>().Members.First(m => m.Name == "Load Test Plan");
                    var load = m.Get<IMethodAnnotation>();
                    load.Invoke();
                    tapTraceListener.Flush();
                    Assert.IsEmpty(tapTraceListener.ErrorMessage);
                }
            }
        }
        
        [Test]
        public void TestPackageDependencySerializer()
        {
            var plan = new TestPlan()
            {
                ChildTestSteps = { new DelayStep() }
            };
            var packageVersion = new PackageVersion("pkg", SemanticVersion.Parse("1.0.0"), "Linux", CpuArchitecture.AnyCPU, DateTime.Now, new List<string>());

            { // verify that a serialized plan has package dependencies
                var ser = new TapSerializer();
                var str = ser.SerializeToString(plan);   
                CollectionAssert.IsEmpty(ser.Errors);
                var elem = XElement.Parse(str);
                Assert.AreEqual(1, elem.Elements("Package.Dependencies").Count());
            }
            { // verify that a serialized collection of plans has package dependencies
                var ser = new TapSerializer();
                var plans = new TestPlan[]
                {
                    plan,
                    plan
                };
                var str = ser.SerializeToString(plans);
                CollectionAssert.IsEmpty(ser.Errors);
                var elem = XElement.Parse(str);
                Assert.AreEqual(1, elem.Elements("Package.Dependencies").Count());            
            }
            { // verify that a serialized package version does not have package dependencies
                var ser = new TapSerializer();
                var str = ser.SerializeToString(packageVersion);
                CollectionAssert.IsEmpty(ser.Errors);
                var elem = XElement.Parse(str);
                Assert.AreEqual(0, elem.Elements("Package.Dependencies").Count());

                var deserialized = ser.DeserializeFromString(str);
                Assert.AreEqual(packageVersion, deserialized);
            }
            { // verify that a serialized list of package versions does not have package dependencies
                var ser = new TapSerializer();
                var versions = new PackageVersion[] { packageVersion };
                var str = ser.SerializeToString(versions);
                CollectionAssert.IsEmpty(ser.Errors);
                var elem = XElement.Parse(str);
                Assert.AreEqual(0, elem.Elements("Package.Dependencies").Count());

                var deserialized = ser.DeserializeFromString(str);
                if (deserialized is PackageVersion[] versions2)
                {
                    Assert.AreEqual(1, versions2.Count());
                    CollectionAssert.AreEqual(versions, versions2, "Deserialized versions were different from the serialized versions.");
                }
                else
                {
                    Assert.Fail($"Failed to deserialize serialized version array.");
                }
            }
            { // Verify that a test plan still has package dependencies when it contains a PackageVersion property
                var ser = new TapSerializer();
                var plan2 = new TestPlan()
                {
                    ChildTestSteps =
                    {
                        new PackageVersionTestStep()
                        {
                            PackageVersion = new[] { packageVersion }
                        }
                    }
                };
                var str = ser.SerializeToString(plan2);   
                CollectionAssert.IsEmpty(ser.Errors);
                var elem = XElement.Parse(str);
                Assert.AreEqual(1, elem.Elements("Package.Dependencies").Count());
            }
        }

        public interface IPSU : IInstrument
        {
            
        }

        public class InstrA : Instrument, IPSU
        {
            
        }
        public class InstrB : Instrument, IPSU
        {
            
        }

        public class StepA : TestStep
        {
            public IPSU Instr { get; set; }
            public List<IPSU> Instrs { get; set; } = new List<IPSU>();
            
            public override void Run() { }
        }

        [Test]
        public void TestSerializingResourceReferences()
        {
            using (Session.Create())
            {
                InstrumentSettings.Current.Clear();
                var instr = new InstrA {Name = "AABBCC"};
                var instr2 = new InstrB {Name = "AABBCC"};
                var instr3 = new InstrB();
                var instr4 = new InstrB();
                InstrumentSettings.Current.Add(instr);
                var step = new StepA()
                {
                    Instr = instr
                };
                step.Instrs.Add(instr);
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(step);

                var x = plan.SerializeToString();
                InstrumentSettings.Current.Remove(instr);
                InstrumentSettings.Current.Add(instr3);
                InstrumentSettings.Current.Add(instr2);
                InstrumentSettings.Current.Add(instr4);
                var serializer = new TapSerializer();
                var plan2 = (TestPlan)serializer.DeserializeFromString(x);
                var step2 = (StepA)plan2.ChildTestSteps[0];
                Assert.AreEqual(instr2, step2.Instr);
                Assert.AreEqual(instr2, step2.Instrs[0]);
                var msg = serializer.XmlMessages.FirstOrDefault();
                StringAssert.Contains("Selected resource 'AABBCC' of type InstrB instead of declared type InstrA.", msg.ToString());
            }
        }

        public class StepWithLicenseException : TestStep
        {
            public static bool ThrowLicense; 
            public StepWithLicenseException()
            {
                if (ThrowLicense)
                    throw new LicenseException(GetType());
            }
            public override void Run() { }
        }
        

        [Test]
        public void DeserializeLicensedTest()
        {
            TestTraceListener tapTraceListener = new TestTraceListener();

            using (Session.Create(SessionOptions.RedirectLogging))
            {
                Log.AddListener(tapTraceListener);
                var step = new StepWithLicenseException();
                var plan = new TestPlan();
                plan.Steps.Add(step);
                var str = plan.SerializeToString();
                try
                {
                    StepWithLicenseException.ThrowLicense = true;
                    new TapSerializer().DeserializeFromString(str);
                }
                finally
                {
                    StepWithLicenseException.ThrowLicense = false;
                }
            }
            // there should only be one error message (the license error)
            Assert.AreEqual(1, tapTraceListener.ErrorMessage.Count);
            Assert.AreEqual(1,
                tapTraceListener.ErrorMessage.Count(x =>
                    x.Contains("Unable to read StepWithLicenseException. A valid license cannot be granted")));
        }

        public class DynamicDependencySerializerPlugin : TapSerializerPlugin, ITapSerializerPluginDependencyMarker
        {

            public override double Order => 1000;
            HashSet<XElement> visitedNodes = new HashSet<XElement>();
            static XName testPlanName = "TestPlan";
            public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
            {
                if (node.Name == testPlanName)
                {
                    if (visitedNodes.Add(node))
                        return Serializer.Deserialize(node, setter, t);
                }
                return false;
            }
            public override bool Serialize(XElement node, object obj, ITypeData expectedType)
            {
                if (visitedNodes.Add(node) == false) return false;
                return Serializer.Serialize(node, obj, expectedType);
            }
            
            public bool NeededForDeserialization
            {
                get;
                set;
            }
        }

        public class NestedObject2
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
        
        public class NestedObject
        {
            public NestedObject2 A { get; set; } = new NestedObject2() {X = 1, Y = 2};
            public NestedObject2 B { get; set; } = new NestedObject2() {X = 4, Y = 10};
            public List<NestedObject2> Objects { get; set; }= new List<NestedObject2>();
            public double C { get; set; } = 5.0;
        }

        [Test]
        public void TestSerializeNestedObject()
        {
            var obj0 = new NestedObject();
            obj0.C = 10;
            obj0.A = new NestedObject2 {X = 3, Y = 3};
            obj0.B = new NestedObject2 {X = 10, Y = 11};
            obj0.Objects.Add(new NestedObject2 {X = 1, Y = -1});
            var serializer = new TapSerializer();
            var xml = serializer.SerializeToString(obj0);
            var obj1 = (NestedObject) serializer.DeserializeFromString(xml);
            
            Assert.AreEqual(10.0, obj1.C);
            Assert.AreEqual(3.0, obj1.A.X);
            Assert.AreEqual(3.0, obj1.A.Y);
            Assert.AreEqual(10.0, obj1.B.X);
            Assert.AreEqual(11.0, obj1.B.Y);

            Assert.AreEqual(1, obj1.Objects.Count);
            Assert.AreEqual(1, obj1.Objects[0].X);
            Assert.AreEqual(-1, obj1.Objects[0].Y);

        }

        [TestCase(true)]
        [TestCase(false)]
        public void SerializerMaybeUsedType(bool addSerializerPluginDependency)
        {
            var serializer = new TapSerializer();
            var obj = new TestPlan();
            serializer.GetSerializer<DynamicDependencySerializerPlugin>().NeededForDeserialization = addSerializerPluginDependency;
            serializer.SerializeToString(obj);
            var usedTypes = serializer.GetUsedTypes();
            var wasUsed = usedTypes.Contains(TypeData.FromType(typeof(DynamicDependencySerializerPlugin)));
            if (addSerializerPluginDependency)
            {
                Assert.IsTrue(wasUsed);
            }
            else
            {
                Assert.IsFalse(wasUsed);
            }
        }
        
    }
}

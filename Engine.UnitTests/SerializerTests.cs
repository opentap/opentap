using System;
using System.Collections.Generic;
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
    }
}

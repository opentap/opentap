//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.IO;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class PluginSearcherTests
    {
        [Test]
        public void PluginSearcherBasics()
        {
            PluginSearcher searcher = new PluginSearcher();

            searcher.Search(new string[] { "OpenTap.UnitTests.dll", "OpenTap.dll" });
            CollectionAssert.AllItemsAreInstancesOfType(searcher.PluginTypes.ToList(), typeof(TypeData));
            CollectionAssert.AllItemsAreNotNull(searcher.PluginTypes.ToList());
            CollectionAssert.AllItemsAreUnique(searcher.PluginTypes.ToList());

            var instrType = searcher.PluginTypes.FirstOrDefault(st => st.Name == "OpenTap.IInstrument");
            Assert.IsNotNull(instrType);
            // Test of SearchType.PluginTypes (should be the type itself, as this is IInstrument is a plugin type - it directly implements ITapPlugin)
            Assert.AreEqual(instrType, instrType.PluginTypes.First());
            // Test of SearchType.Display
            Assert.IsNotNull(instrType.Display);
            Assert.AreEqual("Instrument", instrType.Display.Name);
            // Test of SearchType.Assembly
            Assert.AreEqual("OpenTap", instrType.Assembly.Name);

            var instrImplType = searcher.PluginTypes.FirstOrDefault(st => st.Name == "OpenTap.Engine.UnitTests.InstrumentTest");
            Assert.IsNotNull(instrImplType);
            // Test of SearchType.PluginTypes
            Assert.AreEqual(1, instrImplType.PluginTypes.Count());
            Assert.AreEqual(instrType, instrImplType.PluginTypes.First());
            // Test of SearchType.DerivedTypes
            CollectionAssert.Contains(instrType.DerivedTypes.ToArray(), instrImplType);
            // Test of SearchAssembly.References
            CollectionAssert.Contains(instrImplType.Assembly.References.ToList(), instrType.Assembly);
            // Test of SearchAssembly.Load()
            Assert.AreEqual(Assembly.GetExecutingAssembly(),instrImplType.Assembly.Load());
            // Test of SearchType.Load()
            Assert.AreEqual(typeof(OpenTap.Engine.UnitTests.InstrumentTest), instrImplType.Load());
            
            // Test of nested class
            var stepType = searcher.PluginTypes.FirstOrDefault(st => st.Name == "OpenTap.Engine.UnitTests.TestPlanTestFixture1+TestPlanTestStep");
            Assert.IsNotNull(stepType);

            CollectionAssert.IsSubsetOf(searcher.PluginTypes.ToList(), searcher.AllTypes.Values);
        }

        [Test]
        public void SameAssemblyTwice()
        {
            PluginSearcher searcher = new PluginSearcher();
            try
            {
                Directory.CreateDirectory("SameAssemblyTwiceTestDir");
                File.Copy("OpenTap.UnitTests.dll", "SameAssemblyTwiceTestDir/OpenTap.UnitTests.dll",true);
                searcher.Search(new string[] { "OpenTap.UnitTests.dll", "OpenTap.dll", "SameAssemblyTwiceTestDir/OpenTap.UnitTests.dll" });
                Assert.AreEqual(2, searcher.Assemblies.Count());
            }
            finally
            {
                Directory.Delete("SameAssemblyTwiceTestDir",true);
            }
        }

        [Test]
        public void SearchTwice()
        {
            PluginSearcher searcher = new PluginSearcher();
            try
            {
                Directory.CreateDirectory("SameAssemblyTwiceTestDir");
                File.Copy("OpenTap.UnitTests.dll", "SameAssemblyTwiceTestDir/OpenTap.UnitTests.dll", true);
                searcher.Search(new string[] { "OpenTap.UnitTests.dll" });
                Assert.AreEqual(1, searcher.Assemblies.Count());
                searcher.Search(new string[] { "OpenTap.dll" });
                Assert.AreEqual(2, searcher.Assemblies.Count());
            }
            finally
            {
                Directory.Delete("SameAssemblyTwiceTestDir", true);
            }
        }
        
        [Test]
        public void LocateSystemNetIPAddressTest()
        {
            // the plugin searcher does not always locate .NET assemblies if they are part of the framework.
            // However if they are ever mentioned in respect to a Type, then we must make sure it is correctly loaded. 
            var type1 = TypeData.FromType(typeof(System.Net.IPAddress));
            var type2 = TypeData.GetTypeData("System.Net.IPAddress");
            var type3 = TypeData.GetTypeData(new System.Net.IPAddress(new byte[]{127, 0, 0, 1}));
            
            Assert.IsNotNull(type1);
            Assert.IsNotNull(type2);
            Assert.IsNotNull(type3);
            Assert.AreEqual(type1, type2);
            Assert.AreEqual(type3, type2);
        }
    }
}

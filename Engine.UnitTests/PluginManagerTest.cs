//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using OpenTap.EngineUnitTestUtils;
using System.Threading.Tasks;
using OpenTap.Package;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{


    /// <summary>
    ///This is a test class for PluginManagerTest and is intended
    ///to contain all PluginManagerTest Unit Tests
    ///</summary>
    [TestFixture]
    public class PluginManagerTest : EngineTestBase
    {
        /// <summary>
        ///A test for SearchAsync
        ///</summary>
        [Test]
        public void SearchAsyncTest()
        {
            PluginManager.SearchAsync().Wait();
            Assert.IsTrue(PluginManager.GetPlugins(typeof(IResultListener)).Any(), "Search did not find database interface.");
            Assert.IsTrue(PluginManager.GetPlugins(typeof(ITestStep)).Any(), "Search did not find any teststeps.");
        }

        private static void DeleteFile(string assemblyFileName)
        {
            int retries = 0;
            while (File.Exists(assemblyFileName))
            {
                try
                {
                    File.Delete(assemblyFileName);
                }
                catch
                {
                    Thread.Sleep(50);
                    if (retries++ > 20)
                        break;
                }
            }
        }
        private static void GeneratePluginAssembly(string assemblyFileName, string testStepName, string guid = "634896ca-7e6a-c66d-5ef7-2c2d2a5c3f30", string customcode = "")
        {
            string cs = "";
            cs += "using System.Reflection;";
            cs += "using System.Runtime.InteropServices;";
            cs += "using OpenTap;";
            cs += "[assembly: Guid(\"" + guid + "\")]";
            cs += customcode;
            cs += "public class " + testStepName + " : TestStep { public override void Run(){} }";

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("OpenTap.dll");
            parameters.GenerateInMemory = false;
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = Path.Combine(Path.GetTempPath(), Path.GetFileName(assemblyFileName));
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, cs);
            if (results.Errors.HasErrors)
            {
                var errors = results.Errors.Cast<CompilerError>().Select(err => err.ToString());
                Assert.Inconclusive(String.Join("\r\n", errors));
            }

            DeleteFile(assemblyFileName);
            File.Move(results.PathToAssembly, assemblyFileName);
            DeleteFile(results.PathToAssembly);
        }

        //[Test, Ignore("We dont know what this actually does")]
        //public void DirectoriesToSearchTest()
        //{
        //    //This Test requires TAP to be installed

        //    // Make sure we have the stuff we need in a temp dir:
        //    string baseDir = Path.Combine(Path.GetTempPath(), "DirectoriesToSearchTest");
        //    Directory.CreateDirectory(baseDir);
        //    File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(baseDir, Path.GetFileName(Assembly.GetExecutingAssembly().Location)), true);
        //    File.Copy("OpenTap.dll", Path.Combine(baseDir, "OpenTap.dll"), true);
        //    File.Copy("Keysight.Ccl.Licensing.Api.dll", Path.Combine(baseDir, "Keysight.Ccl.Licensing.Api.dll"), true);
        //    File.Copy("System.Reflection.Metadata.dll", Path.Combine(baseDir, "System.Reflection.Metadata.dll"), true);
        //    File.Copy("System.Collections.Immutable.dll", Path.Combine(baseDir, "System.Collections.Immutable.dll"), true);

        //    // Run PluginManager.Search from the temp dir while specifying TAP_PATH as an additional directory to search:
        //    AppDomainSetup setup = new AppDomainSetup { ApplicationBase = baseDir };
        //    AppDomain domain = AppDomain.CreateDomain("TestSearchDomain", null, setup);
        //    List<string> asms = new List<string>();
        //    domain.DoCallBack(() =>
        //    {
        //        TestTraceListener log = new TestTraceListener();
        //        Log.AddListener(log);
        //        // Ask to look for plugins in TAP installation dir as well as current dir 
        //        // (current dir should be different from TAP installation dir for the bug to show)
        //        PluginManager.DirectoriesToSearch.Add(Environment.GetEnvironmentVariable("TAP_PATH"));
        //        var d = AppDomain.CurrentDomain;
        //        var l = log.GetLog();
        //        d.SetData("log", l);
        //    });
        //    string logText = (string)domain.GetData("log");
        //    AppDomain.Unload(domain);

        //    // This can fail if a plugin in the other Dir to search (in this test TAP_PATH) 
        //    // cannot be loaded because a dependency cannot be found.
        //    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(logText, "Missing required assembly"));
        //}

        [Test]
        public void ExtensionTest()
        {
            int Min = -10;
            int Max = 20;
            IEnumerable<int> ints = Enumerable.Range(Min, Max - Min + 1).ToList();
            int min = ints.FindMin(i => i);
            int max = ints.FindMax(i => i);
            Debug.Assert(min == Min);
            Debug.Assert(max == Max);
            Debug.Assert(max == ints.FindMin(i => -i));
        }

        /// <summary>
        /// Same assembly two different locations.
        /// </summary>
        [Test, Ignore("?")]
        public void SameAssemblyTwiceLoaded()
        {
            OpenTap.SessionLogs.Initialize("StartupLog.txt");
            var trace = new TestTraceListener();
             OpenTap.Log.AddListener(trace);
            Directory.CreateDirectory("PluginManager.Test");
            string path1 = "twice.dll";
            GeneratePluginAssembly(path1, "Test1");
            string path2 = "PluginManager.Test\\twice.dll";
            GeneratePluginAssembly(path2, "Test1");
            try
            {
                var d = AppDomain.CurrentDomain;
                var assemblies = d.GetAssemblies().Where(asm => !asm.IsDynamic).Select(asm => Path.GetFileName(asm.Location)).ToList();
                Assert.IsTrue(assemblies.Contains("twice.dll"));
                Log.Flush();
                var all = trace.allLog.ToString();
                int times = 0;
                foreach (var line in all.Split('\n'))
                    if (line.Contains("Warning") && line.Contains("Not loading") && line.Contains("twice.dll"))
                        times++;

                Assert.AreEqual(times, 1);
            }
            finally
            {
                DeleteFile(path1); //loaded dlls cannot be deleted...
                DeleteFile(path2);
            }
        }

        static void GenerateAssemblyWithVersion(string assemblyFileName, string testStepName, string version)
        {
            string cs = "";
            cs += "using System.Reflection;\n";
            cs += "using System.Runtime.InteropServices;\n";
            cs += "[assembly: Guid(\"634896ca-7e6a-c66d-5ef7-2c2d2a5c3f31\")]\n";
            cs += "[assembly: AssemblyVersion(\"" + version + "\")]\n";
            cs += "public class " + testStepName + " { public void Run(){} }";

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            
            parameters.GenerateInMemory = false;
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = Path.Combine(Path.GetTempPath(), Path.GetFileName(assemblyFileName));
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, cs);
            if (results.Errors.HasErrors)
            {
                var errors = results.Errors.Cast<CompilerError>().Select(err => err.ToString());
                Assert.Inconclusive(String.Join("\r\n", errors));
            }

            DeleteFile(assemblyFileName);
            File.Move(results.PathToAssembly, assemblyFileName);
            DeleteFile(results.PathToAssembly);
        }

        [Test]
        public void SameAssemblyDifferentVersions()
        {
            Directory.CreateDirectory("Test1");
            Directory.CreateDirectory("Test2");
            Directory.CreateDirectory("Test3");
            GenerateAssemblyWithVersion("Test1\\Dual1.dll", "MyStep1", version: "1.0.0");
            GenerateAssemblyWithVersion("Test2\\Dual1.dll", "MyStep1", version: "1.2.0");
            GenerateAssemblyWithVersion("Test3\\Dual1.dll", "MyStep1", version: "1.1.0");
            PluginManager.SearchAsync().Wait();
            var asm1 = Assembly.Load("Dual1");
            Assert.IsTrue(asm1.GetName().Version.Minor == 2);

        }

        interface FancyInterface<T>
        {
            T X { get; set; }
        }
        class FancyType<T> : FancyInterface<int>
        {
            public int X { get; set; }
        }

        [Test]
        public void ReflectionTest()
        {
            Assert.IsTrue(typeof(ITestStep).DescendsTo(typeof(object)));
            Assert.IsTrue(typeof(ITestStep).DescendsTo(typeof(IValidatingObject)));
            Assert.IsTrue(typeof(ITestStep).DescendsTo(typeof(System.ComponentModel.INotifyPropertyChanged)));
            Assert.IsFalse(typeof(ITestStep).DescendsTo(typeof(IInstrument)));
            Assert.IsTrue(typeof(Instrument).DescendsTo(typeof(IResource)));
            Assert.IsTrue(typeof(FancyType<int>).DescendsTo(typeof(FancyType<>)));
            Assert.IsTrue(typeof(FancyType<int>).DescendsTo(typeof(FancyInterface<>)));
            Assert.IsTrue(typeof(FancyType<int>).DescendsTo(typeof(FancyInterface<int>)));
            Assert.IsFalse(typeof(FancyType<int>).DescendsTo(typeof(FancyInterface<bool>)));
        }
        
        /// <summary>
        /// Loading of dlls at runtime.
        /// </summary>
        [Test]
        public void DynamicLoading()
        {
            // test loading dlls at runtime, by the following procedure:
            // 1. call search() and get number of TestSteps
            // 2. Create a new dll with a TestStep in it. (by calling the compiler)
            // 3. call search() and get number of TestSteps.
            // 4. The number of steps should have increased by the number of steps in the new assembly.
            // some of this is done in an AppDomain to be able to unload and delete the DLL
            // guids are used to generate unique names.

            // the new assembly must have a GUID.
            var classname = "cls" + Guid.NewGuid().ToString().Replace("-", "");
            var cls =
                  "using OpenTap;\nusing System.Runtime.InteropServices;\nusing System.Reflection;\n"
                + "using System.Runtime.CompilerServices;\n[assembly: Guid(\"__GUID__\")]\n"
                + "namespace test{public class __NAME__ : TestStep\n{\npublic override void Run(){}\n}}\n";
            cls = cls.Replace("__NAME__", classname).Replace("__GUID__", Guid.NewGuid().ToString());

            var dll = string.Format("Keysight.Tap.Engine.TestModule_{0}.dll", Guid.NewGuid());
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("OpenTap.dll");
            parameters.ReferencedAssemblies.Add("netstandard.dll");
            parameters.GenerateInMemory = false;
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = dll;
            parameters.CompilerOptions = "/platform:AnyCPU";
            
            int prevcnt = PluginManager.GetAllPlugins().Count();
            try
            {
                CompilerResults results = provider.CompileAssemblyFromSource(parameters, cls);
                Assert.IsTrue(results.Errors.Count == 0);
                PluginManager.SearchAsync(); // start a new search to find the new assembly
                int postcnt = PluginManager.GetAllPlugins().Count();
                Assert.IsTrue(prevcnt == postcnt - 1);
            }
            finally
            {
                File.Delete(dll);
                PluginManager.SearchAsync(); // start a new search now that the dll is not there anymore.
            }
        }

        [Test]
        public void MemorizerThreadTest()
        {
            List<Task> tasks = new List<Task>();

            // Testing a very short operation in the memorizer
            // With a relatively short running tasks to test if we can break the internals of the memorizer.

            var numberThingTest = new Memorizer<int, int>(thing => thing % 25)
            { MaxNumberOfElements = 25, SoftSizeDecayTime = TimeSpan.MaxValue };
            var numberRevertThingTest = new Memorizer<int, int>(thing => -thing)
            { MaxNumberOfElements = 25, SoftSizeDecayTime = TimeSpan.MaxValue };
            for (int i = 0; i < 10; i++)
            {
                int _i = i;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    for (int j = 0; j < 100000; j++)
                    {
                        var value1 = numberThingTest.Invoke((_i + j) % 71); // ensure collision of keys.
                        var value2 = numberRevertThingTest.Invoke(value1);
                        Assert.IsTrue(value2 == -(((_i + j) % 71) % 25)); // check calculation.
                    }
                }));
            }

            Task.WhenAll(tasks).Wait();
            Task.WaitAll(tasks.ToArray());

        }

        double modf(double val, double mod)
        {
            return Math.Floor((val - Math.Floor(val / mod) * mod) * 1000.0) / 1000.0;
        }


        [Test]
        public void MemorizerTest()
        {
            var times = Enumerable.Range(1, 4).Select(i =>
            {
                Memorizer<double, double, double> mem = new Memorizer<double, double, double>(d => modf(d, Math.PI), Math.Sin);
                var timer = Stopwatch.StartNew();
                var output = Enumerable.Range(0, (int)Math.Pow(3, i))
                    .Select(i2 => i2 / 20.0)
                    .Select(mem.Invoke).ToList();
                return timer.Elapsed;
            }).ToList();


            var times2 = Enumerable.Range(1, 4).Select(i =>
            {
                //Memorizer<double, double, double> mem = new Memorizer<double, double, double>(d => modf(d, Math.PI), Math.Sin);
                var timer = Stopwatch.StartNew();
                var output = Enumerable.Range(0, (int)Math.Pow(3, i))
                    .Select(i2 => i2 / 20.0)
                    .Select(Math.Sin).ToList();
                return timer.Elapsed;
            }).ToList();
            Debug.WriteLine(times);
            Debug.WriteLine(times2);
        }

        public class Algorithm
        {

            public string Name { get; set; }
            public Algorithm()
            {
                Name = "";
            }
        }
        public class Sequence
        {
            [XmlAttribute("Name")]
            public string Name { get; set; }

            [XmlArray]
            public List<Algorithm> Items { get; set; }
            public Sequence()
            {
                Name = "test2";
                Items = new List<Algorithm> { new Algorithm { Name = "Test Algorithm" } };
            }
        }
        [Test]
        public void TestLoadSequence()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Sequence));
            string testData = "<Sequence><Algorithm Name=\"test\"/></Sequence>";
            MemoryStream memStream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(testData));
            //var output = serializer.Deserialize(memStream);
            memStream = new MemoryStream(10000);
            serializer.Serialize(memStream, new Sequence() { Items = new List<Algorithm> { new Algorithm { Name = "test" } } });
            //Console.WriteLine(output);
            System.Text.Encoding.ASCII.GetString(memStream.GetBuffer());
        }

        [Test]
        public void TestTapPluginTypeAttribute()
        {
            var pluginCategory = typeof(ScpiInstrument).GetPluginType();
            Assert.IsTrue(typeof(IInstrument) == pluginCategory[0]);
        }

        public class TestStep
        {
            // This is not a test step!
        }

        [Test]
        public void SameNamedTypes()
        {
            Assert.AreEqual(typeof(OpenTap.TestStep), PluginManager.LocateType("OpenTap.TestStep"));
            Assert.AreEqual(typeof(TestStep), PluginManager.LocateType(typeof(TestStep).FullName));
        }

        [Test]
        public void ResolveAssemblyVersions()
        {
            
            string asm1name = "../ResolveAssemblyVersions/ResolveAssemblyVersions.dll";
            string asm2name = "../ResolveAssemblyVersions/ResolveAssemblyVersions.2.dll";
            if(Directory.Exists("../ResolveAssemblyVersions/"))
                Directory.Delete("../ResolveAssemblyVersions/", true);
            
            Directory.CreateDirectory("../ResolveAssemblyVersions/");
            GenerateAssemblyWithVersion(asm1name, "test", "2.2.0.0");
            GenerateAssemblyWithVersion(asm2name, "test", "2.3.0.0");
            var asm = Assembly.LoadFrom(asm1name);
            var asm2 = Assembly.Load("ResolveAssemblyVersions, Version=2.1.0.0, Culture=neutral, PublicKeyToken=null");

            // this should give a warning
            var asm3 = Assembly.Load("ResolveAssemblyVersions, Version=2.3.0.0, Culture=neutral, PublicKeyToken=null");

            bool noexec = false;
            if (!noexec)
            {
                // This does not work. Once the assembly has failed to load once
                // we wont try again, even if DirectoriesToSearch has been changed.
                try
                {
                    Assembly.Load("ResolveAssemblyVersions.2, Version=2.3.0.0, Culture=neutral, PublicKeyToken=null");
                    Assert.Fail("Load ResolveAssemblyVersions.2 should throw an exception");
                }
                catch
                {

                }
            }

            PluginManager.DirectoriesToSearch.Add(Path.GetFullPath(@"..\ResolveAssemblyVersions\"));
            var asm4 = Assembly.Load("ResolveAssemblyVersions.2, Version=2.3.0.0, Culture=neutral, PublicKeyToken=null");
            var asm5 = Assembly.Load("ResolveAssemblyVersions.2, Version=2.1.0.0, Culture=neutral, PublicKeyToken=null");

            Assert.IsTrue(asm == asm2);
            Assert.IsTrue(asm3 == asm2);

            Assert.IsTrue(asm4 != asm2);
            Assert.IsTrue(asm4 == asm5);

        }
    }
    public class SimpleListTest : TestStep
    {
        public List<double> Doubles { get; set; }
        public List<float> Floats { get; set; }
        public List<decimal> Decimals { get; set; }
        public List<byte> Bytes { get; set; }
        public List<bool> Bools { get; set; }
        public double ADouble { get; set; }
        public int AInt { get; set; }

        public override void Run()
        {
            throw new NotImplementedException();
        }
    }
}



using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Engine.UnitTests;
using OpenTap.NetCoreAssemblyLoader;
using OpenTap.Package;

namespace OpenTap.UnitTests
{
    public class CsvUser
    {
        public CsvUser()
        {

        }

        public void UseCsv()
        {
            var ins = new PackageInstallAction() { Packages = new[] { "CSV" } };
            ins.Execute(CancellationToken.None);
            PluginManager.DirectoriesToSearch.Add(ExecutorClient.ExeDir);
            PluginManager.SearchAsync().Wait();
            var deriv = TypeData.GetDerivedTypes<ResultListener>();
            var td = deriv.FirstOrDefault(t => t.Name == "Keysight.OpenTap.Plugins.Csv.CsvResultListener");
            var writer = td.CreateInstance();
        }
    }
    public class SessionLoadCliAction : ICliAction
    {
        private static TraceSource log = Log.CreateSource(nameof(SessionLoadCliAction));
        public int Execute(CancellationToken cancellationToken)
        {
            var uninst1 = new PackageUninstallAction() { Packages = new[] { "CSV" } };
            uninst1.Execute(CancellationToken.None);

            var asms = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
            var asms2 = Array.Empty<string>();

            var installed1 = Array.Empty<string>();

            WeakReference wr = null;

            using (var s = AssemblyLoadSession.CreateSession())
            {
                void AsmUse()
                {
                    wr = (PluginManager.assemblyLoader.Value as SessionLoadContext).refCtx;
                    // var td = TypeData.GetDerivedTypes<ICliAction>()
                    //     .FirstOrDefault(t => t.Name.Contains("PackageShowAction"));
                    // var o = td.CreateInstance();
                    var csv = new CsvUser();
                    csv.UseCsv();
                    asms2 = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
                    installed1 = Installation.Current.GetPackages().Select(p => p.Name).ToArray();
                }

                AsmUse();
            }


            var asms3 = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();

            var newlyLoaded = asms2.Except(asms).ToArray();

            int i = 0;
            while (wr.IsAlive)
            {
                log.Info($"Waiting for load context to detach.... ({i})");
                TapThread.Sleep(1000);
                i++;
            }

            var uninst = new PackageUninstallAction() { Packages = new[] { "CSV" } };
            uninst.Execute(CancellationToken.None);

            var installed2 = Installation.Current.GetPackages().Select(p => p.Name).ToArray();

            return 0;
        }
    }

    [TestFixture]
    public class SessionLoadContextTests
    {
        [Test]
        public void TestAssemblyUnload()
        {
            PluginManager.Load();
            var asms = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
            var asms2 = Array.Empty<string>();

            WeakReference ctx = null;


            using (AssemblyLoadSession.CreateSession())
            {
                void AsmUse()
                {
                    PluginManager.assemblyLoader.Value.LoadAssembly(Path.Combine(ExecutorClient.ExeDir, "Dependencies/System.Text.Json.4.0.1.2/System.Text.Json.dll"));
                    asms2 = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
                    ctx = new WeakReference(PluginManager.assemblyLoader.Value);
                }

                AsmUse();
            }

            var asms3 = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();

            var newlyLoaded = asms2.Except(asms).ToArray();

            CollectionAssert.AreNotEqual(asms, asms2);
            // Verify that the loaded assembly was correctly unloaded
            CollectionAssert.AreEqual(asms, asms3);
            Assert.AreEqual(1, newlyLoaded.Length);
        }
    }
}
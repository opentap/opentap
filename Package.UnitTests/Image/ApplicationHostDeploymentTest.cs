using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Package;

namespace OpenTap.Image.Tests
{
    [TestFixture]
    public class ApplicationHostDeploymentTest
    {
        [Test, NonParallelizable]
        public void TwoDeployedImagesRunDistinctPluginsConcurrently()
        {
            var workspace = Path.Combine(Path.GetTempPath(), "OpenTapHostedImages-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                var engine = typeof(ApplicationContext).Assembly.Location;
                var runtime = Path.Combine(Path.GetDirectoryName(engine), "OpenTap.Runtime.dll");
                if (!File.Exists(runtime)) runtime = Path.Combine(Path.GetDirectoryName(engine), "Packages", "OpenTAP", "OpenTap.Runtime.dll");
                var version = "1.0.0+" + Guid.NewGuid().ToString("N");
                var core = CreatePackage(workspace, "OpenTAP", version, new Dictionary<string, byte[]>
                {
                    ["OpenTap.dll"] = File.ReadAllBytes(engine),
                    ["OpenTap.Package.dll"] = File.ReadAllBytes(typeof(Installation).Assembly.Location),
                    ["Packages/OpenTAP/OpenTap.Runtime.dll"] = File.ReadAllBytes(runtime)
                });
                var installations = new[] { Path.Combine(workspace, "image-a"), Path.Combine(workspace, "image-b") };
                for (int i = 0; i < 2; i++)
                {
                    string marker = i == 0 ? "ImageAOnly" : "ImageBOnly";
                    var build = CodeGen.BuildCode(
                        "using OpenTap; using System.Reflection; " +
                        $"[assembly: AssemblyVersion(\"{i + 1}.0.0.0\")] " +
                        $"public class {marker} : TestStep {{ public override void Run() {{ }} }}", "HostedImagePlugin");
                    Assert.IsTrue(build.Success, build.Log);
                    var plugin = CreatePackage(workspace, marker, version, new Dictionary<string, byte[]>
                    {
                        [$"Packages/{marker}/HostedImagePlugin.dll"] = build.Bytes
                    });
                    // Exercise real image caching, package extraction and installation metadata.
                    new ImageIdentifier(new[] { core, plugin }, Array.Empty<string>())
                        .Deploy(installations[i], CancellationToken.None);
                    CollectionAssert.AreEquivalent(new[] { "OpenTAP", marker },
                        new Installation(installations[i]).GetPackages().Select(p => p.Name));
                }

                using var rendezvous = new Barrier(2);
                Action synchronize = () =>
                {
                    if (!rendezvous.SignalAndWait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Other image did not reach the rendezvous.");
                };
                var results = new[] { new string[5], new string[5] };
                var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
                {
                    using (OpenTap.Hosting.ApplicationHost.Run(new ApplicationHostOptions.Builder().WithEntryPoint(typeof(HostedImageEntry), nameof(HostedImageEntry.Run)).WithArgument(new object[] { installations[i], synchronize, results[i] }).WithBaseDirectory(installations[i]).Build())) { }
                })).ToArray();
                Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(60)), "Hosted image execution timed out.");
                for (int i = 0; i < 2; i++)
                {
                    Assert.AreEqual(installations[i], results[i][0]);
                    Assert.AreEqual(i == 0 ? "ImageAOnly" : "ImageBOnly", results[i][1]);
                    Assert.AreEqual($"{i + 1}.0.0.0", results[i][2]);
                    Assert.AreEqual(Path.Combine(installations[i], "OpenTap.dll"), results[i][3]);
                    Assert.AreEqual(installations[i], results[i][4], "Installation.Current must not inherit the process installation.");
                }
            }
            finally
            {
                // Non-collectible contexts can lock loaded DLLs on Windows; leave only
                // those files behind rather than masking test failures with cleanup errors.
                foreach (var file in Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories))
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { Directory.Delete(workspace, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        static PackageDef CreatePackage(string directory, string name, string version, Dictionary<string, byte[]> files)
        {
            var definition = new PackageDef
            {
                Name = name, Version = SemanticVersion.Parse(version), Architecture = CpuArchitecture.AnyCPU,
                OS = "Windows,Linux,MacOS",
                Files = files.Keys.Select(path => new PackageFile { RelativeDestinationPath = path }).ToList()
            };
            var path = Path.Combine(directory, name + ".TapPackage");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var stream = zip.CreateEntry($"Packages/{name}/package.xml").Open()) definition.SaveTo(stream);
                foreach (var file in files)
                    using (var stream = zip.CreateEntry(file.Key).Open()) stream.Write(file.Value, 0, file.Value.Length);
            }
            return PackageDef.FromPackage(path);
        }
    }

    public static class HostedImageEntry
    {
        public static object Run(object argument)
        {
            var args = (object[])argument;
            var directory = (string)args[0];
            // Action is defined in shared CoreLib; Barrier's framework assembly
            // can itself be loaded privately, so pass synchronization as a callback.
            var synchronize = (Action)args[1];
            var results = (string[])args[2];
            if (ApplicationContext.BaseDirectory != directory ||
                AssemblyLoadContext.GetLoadContext(typeof(ApplicationContext).Assembly) == AssemblyLoadContext.Default)
                throw new Exception("Wrong instance environment.");
            synchronize();
            PluginManager.Search();
            var plugins = PluginManager.GetPlugins<TestStep>()
                .Where(t => t.Assembly.GetName().Name == "HostedImagePlugin").ToArray();
            if (plugins.Length != 1) throw new Exception("Expected exactly one image-specific plugin.");
            if (!plugins[0].Assembly.Location.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new Exception("Plugin loaded from another installation.");
            // Exercise the plugin while both contexts are alive.
            ((TestStep)Activator.CreateInstance(plugins[0])).Run();
            results[0] = new Installation(ApplicationContext.BaseDirectory).Directory;
            results[1] = plugins[0].Name;
            results[2] = plugins[0].Assembly.GetName().Version.ToString();
            results[3] = typeof(ApplicationContext).Assembly.Location;
            results[4] = Installation.Current.Directory;
            synchronize();
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Diagnostic;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class UninstallContextTest
{
    sealed class RetryOutputListener(TextWriter output, string testName) : ILogListener
    {
        public void EventsLogged(IEnumerable<OpenTap.Diagnostic.Event> events)
        {
            foreach (var evt in events)
            {
                if (evt.Message.StartsWith("[UNINSTALL-RETRY]", StringComparison.Ordinal))
                    output.WriteLine($"{testName}: {evt.Message}");
            }
        }

        public void Flush() => output.Flush();
    }

    IDisposable retryOutputSession;

    [SetUp]
    public void SetUpRetryOutput()
    {
        retryOutputSession = Session.Create(SessionOptions.RedirectLogging);
        Log.AddListener(new RetryOutputListener(TestContext.Out, TestContext.CurrentContext.Test.FullName));
    }

    [TearDown]
    public void TearDownRetryOutput()
    {
        try
        {
            Log.Flush();
        }
        finally
        {
            retryOutputSession.Dispose();
        }
    }

    [Test, Repeat(100)]
    public void TestDeleteFile()
    {
        var ll = new TestTraceListener();
        Log.AddListener(ll);
        var uninstallContext = UninstallContext.Create(Installation.Current);

        // initially verify that all the files exist.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }

        Assert.That(uninstallContext.Delete(Installation.Current.GetOpenTapPackage()), Is.True);

        // After deleting the files verify that all files are deleted.
        List<string> stillExistingFiles = new();
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            if (File.Exists(file.RelativeDestinationPath))
            {
                stillExistingFiles.Add(file.RelativeDestinationPath);
            }
        }
        
        uninstallContext.UndoAllDeletions();
        Log.Flush();

        Assert.That(ll.ErrorMessage, Is.Empty);
        Assert.That(ll.WarningMessage, Is.Empty);

        Assert.That(string.Join(",", stillExistingFiles), Is.Empty);
        
        // finally after undo, verify that all files are back.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }
        Assert.That(File.Exists(".uninstall/.OpenTapIgnore"), Is.True);
        
    }

    sealed class DeletePackageFixture : IDisposable
    {
        public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        public readonly PackageDef Package = new() { Name = "DeletePackageTest" };
        public readonly UninstallContext Context;
        public readonly string Target;

        public DeletePackageFixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            Context = UninstallContext.Create(new Installation(DirectoryPath));
            Target = Directory.GetDirectories(Path.Combine(DirectoryPath, ".uninstall")).Single();
            foreach (string name in new[] { "first.txt", "second.txt", "third.txt" })
            {
                File.WriteAllText(Path.Combine(DirectoryPath, name), name);
                Package.Files.Add(new PackageFile { FileName = name, RelativeDestinationPath = name });
            }
        }

        public string Original(string name) => Path.Combine(DirectoryPath, name);
        public string Moved(string name) => Path.Combine(Target, name);
        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }

    public void TestDeletePackageSuccessAndSharedFiles()
    {
        using var fixture = new DeletePackageFixture();
        var shared = new HashSet<string> { "second.txt" };
        Assert.That(fixture.Context.Delete(fixture.Package, shared), Is.True);
        Assert.That(File.Exists(fixture.Original("first.txt")), Is.False);
        Assert.That(File.Exists(fixture.Original("second.txt")), Is.True);
        Assert.That(File.Exists(fixture.Original("third.txt")), Is.False);
        fixture.Context.UndoAllDeletions();
        foreach (var file in fixture.Package.Files)
            Assert.That(File.ReadAllText(fixture.Original(file.FileName)), Is.EqualTo(file.FileName));
    }

    public void TestDeletePackageSweepsBeforeRetrying()
    {
        using var fixture = new DeletePackageFixture();
        // A directory at the move destination fails File.Move on every platform.
        Directory.CreateDirectory(fixture.Moved("first.txt"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var release = Task.Run(() =>
        {
            Assert.That(SpinWait.SpinUntil(() => File.Exists(fixture.Moved("third.txt")), 3000), Is.True,
                "The last file must be swept before retrying the first.");
            Directory.Delete(fixture.Moved("first.txt"));
        });
        try
        {
            Assert.That(fixture.Context.Delete(fixture.Package, null, cancellation.Token), Is.True);
        }
        finally
        {
            release.GetAwaiter().GetResult();
        }
        foreach (var file in fixture.Package.Files)
            Assert.That(File.Exists(fixture.Original(file.FileName)), Is.False);
        fixture.Context.UndoAllDeletions();
    }

    public void TestDeletePackageRetriesLockedFile()
    {
        using var fixture = new DeletePackageFixture();
        using var locked = new FileStream(fixture.Original("first.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var release = Task.Run(() =>
        {
            Assert.That(SpinWait.SpinUntil(() => File.Exists(fixture.Moved("third.txt")), 3000), Is.True);
            locked.Dispose();
        });
        try
        {
            Assert.That(fixture.Context.Delete(fixture.Package, null, cancellation.Token), Is.True);
        }
        finally
        {
            release.GetAwaiter().GetResult();
        }
        Assert.That(File.Exists(fixture.Original("first.txt")), Is.False);
        fixture.Context.UndoAllDeletions();
        Assert.That(File.ReadAllText(fixture.Original("first.txt")), Is.EqualTo("first.txt"));
    }

    public void TestDeletePackageAlreadyCancelled()
    {
        using var fixture = new DeletePackageFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => fixture.Context.Delete(fixture.Package, null, cancellation.Token));
        foreach (var file in fixture.Package.Files)
            Assert.That(File.Exists(fixture.Original(file.FileName)), Is.True);
    }

    public void TestDeletePackageRetryBudgetAndRollback()
    {
        using var fixture = new DeletePackageFixture();
        Directory.CreateDirectory(fixture.Moved("first.txt"));
        var timer = Stopwatch.StartNew();
        Assert.That(fixture.Context.Delete(fixture.Package), Is.False);
        Assert.That(timer.Elapsed.TotalSeconds, Is.InRange(9.5, 15), "Retries must be bounded.");
        foreach (var file in fixture.Package.Files)
            Assert.That(File.ReadAllText(fixture.Original(file.FileName)), Is.EqualTo(file.FileName));
    }

    public void TestDeletePackageCancellationRollsBack()
    {
        using var fixture = new DeletePackageFixture();
        Directory.CreateDirectory(fixture.Moved("first.txt"));
        using var cancellation = new CancellationTokenSource();
        var cancel = Task.Run(() =>
        {
            Assert.That(SpinWait.SpinUntil(() => File.Exists(fixture.Moved("third.txt")), 3000), Is.True);
            cancellation.Cancel();
        });
        var timer = Stopwatch.StartNew();
        try
        {
            Assert.Throws<OperationCanceledException>(() => fixture.Context.Delete(fixture.Package, null, cancellation.Token));
        }
        finally
        {
            cancel.GetAwaiter().GetResult();
        }
        Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(3));
        foreach (var file in fixture.Package.Files)
            Assert.That(File.ReadAllText(fixture.Original(file.FileName)), Is.EqualTo(file.FileName));
    }

    public void TestRenamePackageFilesFromArchive()
    {
        using var fixture = new DeletePackageFixture();
        string archive = Path.GetFullPath("TapPackages/MyPlugin1.TapPackage");
        var package = PackageDef.FromPackage(archive);
        PluginInstaller.UnpackPackage(archive, fixture.DirectoryPath);
        Assert.That(package.Files, Is.Not.Empty);
        foreach (var file in package.Files)
            Assert.That(File.Exists(fixture.Original(file.RelativeDestinationPath)), Is.True);
        Installer.RenamePackageFiles(fixture.DirectoryPath, new List<string> { archive });
        foreach (var file in package.Files)
            Assert.That(File.Exists(fixture.Original(file.RelativeDestinationPath)), Is.False);
    }

    public void TestRenamePackageFilesFromXml(bool invalidSecondPackage)
    {
        using var fixture = new DeletePackageFixture();
        string xml = Path.Combine(fixture.DirectoryPath, "input.xml");
        File.WriteAllText(xml, "<Package Name=\"DeletePackageTest\" Version=\"1.0.0\" xmlns=\"http://opentap.io/schemas/package\"><Files>" +
            string.Join("", fixture.Package.Files.Select(f => $"<File Path=\"{f.FileName}\" />")) + "</Files></Package>");
        var paths = new List<string> { xml };
        if (invalidSecondPackage)
        {
            paths.Add(Path.Combine(fixture.DirectoryPath, "missing.TapPackage"));
            Assert.Catch(() => Installer.RenamePackageFiles(fixture.DirectoryPath, paths));
        }
        else
            Installer.RenamePackageFiles(fixture.DirectoryPath, paths);
        foreach (var file in fixture.Package.Files)
            Assert.That(File.Exists(fixture.Original(file.FileName)), Is.EqualTo(invalidSecondPackage));
    }

    public void TestUndoAllDeletionsContinuesAfterFailure()
    {
        using var fixture = new DeletePackageFixture();
        Assert.That(fixture.Context.Delete(fixture.Package), Is.True);
        File.WriteAllText(fixture.Original("second.txt"), "replacement");
        Assert.DoesNotThrow(() => fixture.Context.UndoAllDeletions());
        Assert.That(File.ReadAllText(fixture.Original("first.txt")), Is.EqualTo("first.txt"));
        Assert.That(File.ReadAllText(fixture.Original("third.txt")), Is.EqualTo("third.txt"));
        Assert.That(File.ReadAllText(fixture.Original("second.txt")), Is.EqualTo("replacement"));
        Assert.That(File.Exists(fixture.Moved("second.txt")), Is.True);
        File.Delete(fixture.Original("second.txt"));
        fixture.Context.UndoAllDeletions();
        Assert.That(File.ReadAllText(fixture.Original("second.txt")), Is.EqualTo("second.txt"));
    }

    class TestAsmDef(string assemblyName, string @namespace, string className, string displayName, int majorVersion, string helpLink = null)
    {
        public readonly string assemblyName = assemblyName;
        public readonly string className = className;
        public readonly string @namespace = @namespace;
        public readonly string displayName = displayName;
        public int majorVersion = majorVersion;
        public readonly string helpLink = helpLink;
    }

    static string CreateNewAssemblyWithTestStep(TestAsmDef def)
    {
        var asmName = new AssemblyNameDefinition(def.assemblyName, new Version(def.majorVersion, 0));
        string moduleName = "TestModule";

        var asm = AssemblyDefinition.CreateAssembly(asmName, moduleName, ModuleKind.Dll);

        var teststep = asm.MainModule.ImportReference(typeof(TestStep));
        var runMethod = asm.MainModule.ImportReference(typeof(TestStep).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)).Resolve();
        var ctorMethod = asm.MainModule.ImportReference(typeof(TestStep).GetConstructor([])).Resolve();

        // create dummy plugin
        { 
            // Create new test step
            var t = new TypeDefinition(def.@namespace, def.className, TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, teststep);
            asm.MainModule.Types.Add(t);

            // Create default constructor
            {
                var ctor = new MethodDefinition(".ctor", ctorMethod.Attributes, asm.MainModule.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, t.Module.ImportReference(ctorMethod));
                il.Emit(OpCodes.Ret);
                t.Methods.Add(ctor);
            }

            // build Run() method
            {
                var newRunMethod = new MethodDefinition("Run", MethodAttributes.Public, runMethod.ReturnType) { IsHideBySig = true, IsVirtual = true };
                var il = newRunMethod.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                t.Methods.Add(newRunMethod);
            }

            // Add display attribute
            {
                Type attrType = typeof(DisplayAttribute);
                object[] arguments = [def.displayName,    "Runtime Generated Step", "Cecil",        1.0,              false,        Array.Empty<string>()];
                Type[] signature = [ .. arguments.Select(x => x.GetType()) ];
                var tCtor = t.Module.ImportReference(attrType.GetConstructor(signature));
                var attr = new CustomAttribute(tCtor);
                var attrArguments = arguments.Select(x => new CustomAttributeArgument(t.Module.ImportReference(x.GetType()), x));
                foreach (var arg in attrArguments) attr.ConstructorArguments.Add(arg);
                t.CustomAttributes.Add(attr);
            }
            // Add HelpLink attribute
            if (def.helpLink != null)
            {
                Type attrType = typeof(HelpLinkAttribute);
                object[] arguments = [def.helpLink];
                Type[] signature = [ .. arguments.Select(x => x.GetType()) ];
                var tCtor = t.Module.ImportReference(attrType.GetConstructor(signature));
                var attr = new CustomAttribute(tCtor);
                var attrArguments = arguments.Select(x => new CustomAttributeArgument(t.Module.ImportReference(x.GetType()), x));
                foreach (var arg in attrArguments) attr.ConstructorArguments.Add(arg);
                t.CustomAttributes.Add(attr);
            }
        }

        var fp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
        asm.Write(fp);
        return fp;
    }

    [Test]
    public void TestLoadDifferentPluginVersions()
    {
        const string testAssemblyName = nameof(TestLoadDifferentPluginVersions);
        var asmLocation = Path.Combine(Installation.Current.Directory, testAssemblyName + ".dll");
        if (File.Exists(asmLocation)) File.Delete(asmLocation);
        using var _ = Utils.WithDisposable(() => File.Delete(asmLocation));

        var uninstall = UninstallContext.Create(Installation.Current);
        var def1 = new TestAsmDef(testAssemblyName, "ns1", "stepclass", "First Name", 1);
        var def2 = new TestAsmDef(testAssemblyName, "ns1", "stepclass", "Second Name", 2);
        // Create two different versions of the same assembly with minor changes.
        var asm1 = CreateNewAssemblyWithTestStep(def1);
        var asm2 = CreateNewAssemblyWithTestStep(def2);

        TypeData initialTd = null;
        {
            // Copy the assembly into the installation
            File.Copy(asm1, asmLocation);
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            var disp1 = td.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ First Name"));
            // load the plugin. By loading the plugin, we prevent the Searcher from invalidating it.
            td.AsTypeData().Load();
            initialTd = td.AsTypeData();
        }


        // Verify the type still exists after deletion
        {
            uninstall.Delete(new PackageFile() { FileName = testAssemblyName + ".dll", RelativeDestinationPath = testAssemblyName + ".dll" });
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            Assert.That(td.AsTypeData().Type, Is.Not.Null);
        }

        // Uninstall the type
        uninstall.Delete(new PackageFile() { FileName = testAssemblyName + ".dll", RelativeDestinationPath = testAssemblyName + ".dll" });

        // Verify that the type now remains even if it was deleted
        {
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            Type tp = td.AsTypeData().Type;
            Assert.That(tp, Is.Not.Null);
            Assert.That(tp.Assembly.Location, Does.Not.Exist);
        }

        // Verify that the old type name is still used after updating the dll
        {
            File.Copy(asm2, asmLocation);
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            var disp1 = td.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ First Name"));
        }

        // Verify that the first loaded typedata is still valid
        {
            Assert.That(initialTd, Is.Not.Null);
            var disp1 = initialTd.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ First Name"));
            var step = (TestStep)initialTd.CreateInstance();
        }

        // Verify that the actual file on disk is the 2nd variant
        {
            var newSearcher = new PluginSearcher();
            newSearcher.Search([asmLocation]);
            Dictionary<string, TypeData> allTypes = (Dictionary<string, TypeData>)newSearcher.GetType()
                .GetField("AllTypes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(newSearcher);
            Assert.That(allTypes, Is.Not.Null);
            Assert.That(allTypes.Count, Is.EqualTo(1));
            Assert.That(allTypes[$"{def1.@namespace}.{def1.className}"].GetDisplayAttribute().GetFullName(), Is.EqualTo("Cecil \\ Second Name"));
        }
    }

    [Test]
    public void TestUpgradeUnloadedPluginTypes()
    {
        const string testAssemblyName = nameof(TestUpgradeUnloadedPluginTypes);
        // This test is very similar to the first variant.
        // The difference is that this variant does not load the plugin initially.
        // This verifies the functionality that we should be able to remove / update a plugin if it has not been loaded previously.
        var asmLocation = Path.Combine(Installation.Current.Directory, testAssemblyName + ".dll");
        if (File.Exists(asmLocation)) File.Delete(asmLocation);
        using var _ = Utils.WithDisposable(() => File.Delete(asmLocation));

        var uninstall = UninstallContext.Create(Installation.Current);
        var def1 = new TestAsmDef(testAssemblyName, "ns2", "stepclass", "First Name", 1);
        var def2 = new TestAsmDef(testAssemblyName, "ns2", "stepclass", "Second Name", 2);
        // Create two different versions of the same assembly with minor changes.
        var asm1 = CreateNewAssemblyWithTestStep(def1);
        var asm2 = CreateNewAssemblyWithTestStep(def2);

        {
            // Copy the assembly into the installation
            File.Copy(asm1, asmLocation);
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            var disp1 = td.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ First Name"));
            bool loaded = td.AsTypeData().IsAssemblyLoaded();
        }


        // Verify that the type is gone after uninstalling it
        {
            uninstall.Delete(new PackageFile() { FileName = testAssemblyName + ".dll", RelativeDestinationPath = testAssemblyName + ".dll" });
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Null);
        }

        // Verify that we get the new version by updating the dll
        {
            File.Copy(asm2, asmLocation);
            PluginManager.Search();
            var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}");
            Assert.That(td, Is.Not.Null);
            var disp1 = td.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ Second Name"));
        }
    }

    [Test]
    public void TestAttributeScanning()
    {
        const string testAssemblyName = nameof(TestAttributeScanning);
        var asmLocation = Path.Combine(Installation.Current.Directory, testAssemblyName + ".dll");
        if (File.Exists(asmLocation)) File.Delete(asmLocation);
        var def1 = new TestAsmDef(testAssemblyName, "AttributeTest", "MyStepClass", "My Type", 1,
                helpLink: "example helplink string");
        var asm1 = CreateNewAssemblyWithTestStep(def1);
        File.Copy(asm1, asmLocation);
        using var _ = Utils.WithDisposable(() => File.Delete(asmLocation));
        PluginManager.Search();

        var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{def1.@namespace}.{def1.className}").AsTypeData();
        {
            Assert.That(td, Is.Not.Null);
            Assert.That(td.IsAssemblyLoaded(), Is.False);
        }
        {
            var disp1 = td.GetDisplayAttribute();
            Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ My Type"));
            Assert.That(td.IsAssemblyLoaded(), Is.False);
        }
        {
            var disp2 = td.GetAttribute<DisplayAttribute>();
            Assert.That(disp2.GetFullName(), Is.EqualTo("Cecil \\ My Type"));
            Assert.That(td.IsAssemblyLoaded(), Is.False);
        }
        {
            Assert.That(td.IsBrowsable, Is.True);
            Assert.That(td.IsAssemblyLoaded(), Is.False);
        }
        {
            var help = td.GetAttribute<HelpLinkAttribute>();
            Assert.That(help, Is.Not.Null);
            Assert.That(help.HelpLink, Is.EqualTo("example helplink string"));
            Assert.That(td.IsAssemblyLoaded(), Is.False);
        }
    }

    [SupportedOSPlatform("ios")]
    public class UnsupportedStep : TestStep
    {
        public override void Run()
        {
        }
    }

    [SupportedOSPlatform("osx")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    public class SupportedStep : TestStep
    {
        public override void Run()
        {
        }
    }

    [TestCase(typeof(UnsupportedStep), false)]
    [TestCase(typeof(SupportedStep), true)]
    public void TestSupportedOSPlatformScanned(Type t, bool supported)
    {
        var td = TypeData.FromType(t);
        var scanned = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(x => x.Name == td.Name);
        TypeData expected = supported ? td : null;
        Assert.That(scanned, Is.EqualTo(expected));
    }
}

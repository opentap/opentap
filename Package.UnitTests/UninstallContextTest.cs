using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class UninstallContextTest
{

    [Test]
    public void TestDeleteFile()
    {
        var uninstallContext = UninstallContext.Create(Installation.Current);

        // initially verify that all the files exist.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }

        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            uninstallContext.Delete(file);
        }

        // After deleting the files verify that all files are deleted.
        bool verifyAllFilesDeleted = true;
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            if (File.Exists(file.RelativeDestinationPath))
            {
                verifyAllFilesDeleted = false;
            }
        }
        
        uninstallContext.UndoAllDeletions();

        Assert.That(verifyAllFilesDeleted, Is.True);
        
        // finally after undo, verify that all files are back.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }
        Assert.That(File.Exists(".uninstall/.OpenTapIgnore"), Is.True);
        
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

    [Test]
    public void TestUnsupportedPluginsNotScanned()
    {
        var typeName = TypeData.FromType(typeof(UnsupportedStep)).Name;
        var steps = TypeData.GetDerivedTypes<ITestStep>();
        var unsupported = steps.FirstOrDefault(x => x.Name == typeName);
        Assert.That(unsupported, Is.Null);
    }
}

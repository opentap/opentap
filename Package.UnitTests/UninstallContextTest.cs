using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    [Test]
    public void TestLoadDifferentPluginVersions()
    {
        const string testAssemblyName = nameof(TestLoadDifferentPluginVersions);
        const string testNamespace = "TestNamespace";
        const string testTestStep = "MytestStep";

        static string CreateNewAssemblyWithTestStep(string displayName, int majorVersion)
        {
            var asmName = new AssemblyNameDefinition(testAssemblyName, new Version(majorVersion, 0));
            string moduleName = "TestModule";

            var asm = AssemblyDefinition.CreateAssembly(asmName, moduleName, ModuleKind.Dll);

            var teststep = asm.MainModule.ImportReference(typeof(TestStep));
            var runMethod = asm.MainModule.ImportReference(typeof(TestStep).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)).Resolve();
            var ctorMethod = asm.MainModule.ImportReference(typeof(TestStep).GetConstructor([])).Resolve();

            // create dummy plugin
            { 
                // Create new test step
                var t = new TypeDefinition(testNamespace, testTestStep, TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, teststep);
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
                    object[] arguments = [displayName,    "Runtime Generated Step", "Cecil",        1.0,              false,        Array.Empty<string>()];
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

        var asmLocation = Path.Combine(Installation.Current.Directory, testAssemblyName + ".dll");
        if (File.Exists(asmLocation)) File.Delete(asmLocation);

        var uninstall = UninstallContext.Create(Installation.Current);
        try 
        {
            // Create two different versions of the same assembly with minor changes.
            var asm1 = CreateNewAssemblyWithTestStep("First Name", 1);
            var asm2 = CreateNewAssemblyWithTestStep("Second Name", 2);

            TypeData initialTd = null;
            {
                // Copy the assembly into the installation
                File.Copy(asm1, asmLocation);
                PluginManager.Search();
                var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{testNamespace}.{testTestStep}");
                Assert.That(td, Is.Not.Null);
                var disp1 = td.GetDisplayAttribute();
                Assert.That(disp1.GetFullName(), Is.EqualTo("Cecil \\ First Name"));
                // load the plugin
                td.AsTypeData().Load();
                initialTd = td.AsTypeData();
            }


            // Verify the type still exists after deletion
            {
                uninstall.Delete(new PackageFile() { FileName = testAssemblyName + ".dll", RelativeDestinationPath = testAssemblyName + ".dll" });
                PluginManager.Search();
                var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{testNamespace}.{testTestStep}");
                Assert.That(td, Is.Not.Null);
                Assert.That(td.AsTypeData().Type, Is.Not.Null);
            }

            // Uninstall the type
            uninstall.Delete(new PackageFile() { FileName = testAssemblyName + ".dll", RelativeDestinationPath = testAssemblyName + ".dll" });

            // Verify that the type now remains even if it was deleted
            {
                PluginManager.Search();
                var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{testNamespace}.{testTestStep}");
                Assert.That(td, Is.Not.Null);
                Type tp = td.AsTypeData().Type;
                Assert.That(tp, Is.Not.Null);
                Assert.That(tp.Assembly.Location, Does.Not.Exist);
            }

            // Verify that the old type name is still used after updating the dll
            {
                File.Copy(asm2, asmLocation);
                PluginManager.Search();
                var td = TypeData.GetDerivedTypes<ITestStep>().FirstOrDefault(s => s.Name == $"{testNamespace}.{testTestStep}");
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
                Assert.That(allTypes["TestNamespace.MytestStep"].GetDisplayAttribute().GetFullName(), Is.EqualTo("Cecil \\ Second Name"));
            }
        }
        finally
        {
            File.Delete(asmLocation);
        }
    }
}

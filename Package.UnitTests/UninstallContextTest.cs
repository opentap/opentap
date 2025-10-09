using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class UninstallContextTest
{

    public class SimpleAttribute : Attribute 
    {
    }

    public class LessSimpleAttribute : Attribute 
    {
        public LessSimpleAttribute(string str)
        {
        }
    }

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
                var t = new TypeDefinition("ns", "MyTestStep", TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, teststep);
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
                    // We want the constructor with this signature:
                    // public DisplayAttribute(string Name, string Description = null, string Group = null, double Order = DefaultOrder, bool Collapsed = false, string[] Groups = null)
                    Type  [] signature = [typeof(string), typeof(string),           typeof(string), typeof(double), typeof(bool), typeof(string[])];
                    object[] arguments = [displayName,    "Runtime Generated Step", "Cecil",        1,              false,        Array.Empty<string>()];

                    var tDisplay = t.Module.ImportReference(typeof(DisplayAttribute)).Resolve();
                    var tCtor = t.Module.ImportReference(typeof(DisplayAttribute).GetConstructor(signature));
                    
                    var attr = new CustomAttribute(tCtor);
                    // for (int i = 0; i < arguments.Length; i++)
                    // {
                    //     attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.MainModule.ImportReference(signature[i]), arguments[i]));
                    // }
                    t.CustomAttributes.Add(attr);
                }
            }

            var fp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            asm.Write(fp);
            return fp;
        }

        var initialSteps = TypeData.GetDerivedTypes<ITestStep>().ToArray();
        string[] names = ["First Name", "Second Name"];
        var asm1 = CreateNewAssemblyWithTestStep("First Name", 1);
        var asm2 = CreateNewAssemblyWithTestStep("Second Name", 2);

        var asm = System.Reflection.Assembly.LoadFrom(asm1);
        var t1 = asm.GetType("ns.MyTestStep");

        var step = t1.CreateInstance();

        PluginManager.Search();
        var steps2 = TypeData.GetDerivedTypes<ITestStep>().ToArray();

        var attributes = step.GetType().GetCustomAttributes(false);
        foreach (var attr in attributes)
        {
            TestContext.WriteLine(attr);
        }
    }
}

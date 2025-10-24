using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using OpenTap;
using OpenTap.Package;

namespace Package.UnitTests;

[TestFixture]
public class TestConcurrentTypeDataLoad
{
    static System.Reflection.Assembly CreateNewAssemblyWithTestStep(string stepName)
    {
        var asmName = new AssemblyNameDefinition(Guid.NewGuid().ToString(), new Version(1, 0));
        string moduleName = Guid.NewGuid().ToString();

        using var asm = AssemblyDefinition.CreateAssembly(asmName, moduleName, ModuleKind.Dll);

        var teststep = asm.MainModule.ImportReference(typeof(TestStep));
        var runMethod = asm.MainModule.ImportReference(typeof(TestStep).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)).Resolve();
        var ctorMethod = asm.MainModule.ImportReference(typeof(TestStep).GetConstructor([])).Resolve();

        // create dummy plugin
        {
            // Create new test step
            var t = new TypeDefinition(Guid.NewGuid().ToString(), stepName, TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, teststep);
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
        }

        var path = Path.Combine(Installation.Current.Directory, "TestTypeDataLoadAssemblies", asm.Name.Name + ".dll");
        asm.Write(path);
        return System.Reflection.Assembly.LoadFrom(path);
    }

    [TestCase(1)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCase(500)]
    public void TestTypeDataLoad(int count)
    {
        static void VerifyAssemblyData(int index, System.Reflection.Assembly asm)
        {
            var step = asm.GetExportedTypes().First();
            var td = TypeData.FromType(step);
            TestStep instance = (TestStep)td.CreateInstance();
            Assert.That(instance, Is.Not.Null);
            Assert.That(td.GetDisplayAttribute().Name, Is.EqualTo($"Step_{index}"));
        }

        var ctx = UninstallContext.Create(Installation.Current);
        var directory = Path.Combine(Installation.Current.Directory, "TestTypeDataLoadAssemblies");
        Directory.CreateDirectory(directory);

        // Ensure searching is done before we start creating assemblies
        PluginManager.GetSearcher();

        // Create a ton of distinct assemblies and load them
        var assemblies = Enumerable.Range(0, count).Select(i => CreateNewAssemblyWithTestStep($"Step_{i}")).ToArray();
        // Simultaneously attempt to load typedata from each plugin
        Task[] tasks = [.. Enumerable.Range(0, count).Select(i => StartAwaitable(() => VerifyAssemblyData(i, assemblies[i])))];
        Task.WaitAll(tasks);
        // Delete all assemblies and try again -- it should still work
        foreach (var asm in assemblies)
        {
            var rel = Path.GetRelativePath(Installation.Current.Directory, asm.Location);
            ctx.Delete(rel);
        }
        PluginManager.Search();
        tasks = [.. Enumerable.Range(0, count).Select(i => StartAwaitable(() => VerifyAssemblyData(i, assemblies[i])))];
        Task.WaitAll(tasks);
        // This should succeed since all the dlls were uninstalled
        Directory.Delete(directory, false);
    }

    internal static Task StartAwaitable(Action action)
    {
        var result = new TaskCompletionSource<bool>();
        TapThread.Start(() =>
        {
            try
            {
                action();
                result.SetResult(true);
            }
            catch (Exception inner)
            {
                result.SetException(inner);
            }
        });
        return result.Task;
    }
}

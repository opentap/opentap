using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace OpenTap
{
    internal interface IAssemblyLoader
    {
        string GetAssemblyLocation(Assembly asm);
        Assembly LoadAssembly(string assemblyPath);
    }

    internal class InMemoryAssemblyLoader : IAssemblyLoader
    {
        private ConcurrentDictionary<Assembly, string> locationLookup = new ConcurrentDictionary<Assembly, string>();
        private ConcurrentDictionary<string, Assembly> asmLookup = new ConcurrentDictionary<string, Assembly>();

        public string GetAssemblyLocation(Assembly asm)
        {
            var loc = asm.IsDynamic ? null : asm.Location;
            if (string.IsNullOrWhiteSpace(loc) == false) return asm.Location;
            return locationLookup.TryGetValue(asm, out loc) ? loc : "";
        }

        public Assembly LoadAssembly(string assemblyPath)
        {
            Assembly load(string p)
            {

                var asmBytes = File.ReadAllBytes(assemblyPath);
                var pdb = Path.ChangeExtension(assemblyPath, "pdb");
                if (File.Exists(pdb))
                {
                    var symbolBytes = File.ReadAllBytes(pdb);
                    return Assembly.Load(asmBytes, symbolBytes);
                }

                return Assembly.Load(asmBytes);
            }

            var asm = asmLookup.GetOrAdd(assemblyPath, load);
            locationLookup.TryAdd(asm, assemblyPath);
            return asm;
        }
    }

    internal class DefaultAssemblyLoader : IAssemblyLoader
    {
        public string GetAssemblyLocation(Assembly asm)
        {
            return asm.IsDynamic ? null : asm.Location;
        }

        public Assembly LoadAssembly(string assemblyPath)
        {
            return Assembly.LoadFrom(assemblyPath);
        }
    }
}
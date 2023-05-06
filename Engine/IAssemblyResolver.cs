using System.Collections.Generic;
using System.Reflection;

namespace OpenTap
{
    internal interface IAssemblyResolver
    {
        void Invalidate(IEnumerable<string> directoriesToSearch);
        string[] GetAssembliesToSearch();
        void AddAssembly(Assembly asm);
        void AddAssembly(string name, string path);
        Assembly Resolve(string name, bool reflectionOnly);
    }
}
using System.Collections.Generic;
using System.Reflection;

namespace OpenTap;

public interface IAssemblyResolver : ITapPlugin
{
    /// <summary>
    /// Should be called before each search. Flushes the files found. Also sets up the directories to search.
    /// </summary>
    void Invalidate(IEnumerable<string> directoriesToSearch);
    void AddAssembly(string name, string path);
    void AddAssembly(Assembly asm);
    string[] GetAssembliesToSearch();
}

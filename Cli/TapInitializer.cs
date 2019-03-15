//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenTap
{
    internal static class TapInitializer
    {
        private static readonly SimpleTapAssemblyResolver tapAssemblyResolver = new SimpleTapAssemblyResolver();
        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += tapAssemblyResolver.Resolve;
            ContinueInitialization();
        }

        internal static void ContinueInitialization()
        {
            // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
            // Remove so we avoid race condition with OpenTap AssemblyResolver.
            AppDomain.CurrentDomain.AssemblyResolve -= tapAssemblyResolver.Resolve;
            PluginManager.SearchAsync();
        }
    }

    /// <summary>
    /// We need this SimpleTapAssemblyResolver to resolve netstandard.dll. We need to resolve netstandard.dll to be able to load OpenTAP, which is a .netstandard project
    /// After we load OpenTAP, we can safely remove this simple resolver and let TapAssemblyResolver in OpenTAP resolve dependencies.
    /// </summary>
    internal class SimpleTapAssemblyResolver
    {
        private List<string> assemblies { get; set; }

        public SimpleTapAssemblyResolver()
        {
            string curAssemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            string currentDir = Path.GetDirectoryName(curAssemblyFolder);
            assemblies = Directory.EnumerateFiles(currentDir, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }

        internal Assembly Resolve(object sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;

            string filename = args.Name.Split(',')[0].ToLower();
            string assembly = assemblies.FirstOrDefault(s => Path.GetFileNameWithoutExtension(s).ToLower() == filename);

            // check for assemblies already loaded
            Assembly loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (loadedAssembly != null)
                return loadedAssembly;

            if (!string.IsNullOrWhiteSpace(assembly))
                return Assembly.LoadFrom(assembly);

            Console.Error.WriteLine($"Asked to resolve {filename}, but couldn't");
            return null;
        }
    }

}

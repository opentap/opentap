//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenTap.Diagnostic;

namespace OpenTap
{
    internal static class TapInitializer
    {
        private static SimpleTapAssemblyResolver tapAssemblyResolver;

        public class InitTraceListener : ILogListener {
            public List<Event> AllEvents = new List<Event>();
            public void EventsLogged(IEnumerable<Event> events)
            {
                lock(AllEvents)
                    AllEvents.AddRange(events);
            }
            public void Flush(){

            }
            public static InitTraceListener Instance = new InitTraceListener();  
        }

        internal static void Initialize(bool isolated)
        {
            if (tapAssemblyResolver == null) tapAssemblyResolver = new SimpleTapAssemblyResolver(isolated);
            AppDomain.CurrentDomain.AssemblyResolve += tapAssemblyResolver.Resolve;
            ContinueInitialization();
        }

        internal static void ContinueInitialization()
        {
            // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
            // Remove so we avoid race condition with OpenTap AssemblyResolver.
            OpenTap.Log.AddListener(InitTraceListener.Instance);
            PluginManager.Search();
            OpenTap.Log.RemoveListener(InitTraceListener.Instance);
        }
    }

    /// <summary>
    /// We need this SimpleTapAssemblyResolver to resolve netstandard.dll. We need to resolve netstandard.dll to be able to load OpenTAP, which is a .netstandard project
    /// After we load OpenTAP, we can safely remove this simple resolver and let TapAssemblyResolver in OpenTAP resolve dependencies.
    /// </summary>
    internal class SimpleTapAssemblyResolver
    {
        Dictionary<string, string> asmlookup = new Dictionary<string, string>();
        internal bool Isolated { get; set; }

        public SimpleTapAssemblyResolver(bool isolated)
        {
            Isolated = isolated;
            string currentDir = Environment.GetEnvironmentVariable(ExecutorClient.OpenTapInitDirectory);
            if (currentDir == null)
            {
                currentDir = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                currentDir = Path.GetDirectoryName(currentDir);
            }
            var assemblies = Tap.Shared.PathUtils.IterateDirectories(currentDir, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            foreach(var assembly in assemblies){
                var name = Path.GetFileNameWithoutExtension(assembly).ToLower();
                asmlookup[name] = assembly;
            }
        }

        private void OnResolveOpenTap(string path, Assembly asm)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        }

        private ConcurrentDictionary<string, Assembly> lookup = new ConcurrentDictionary<string, Assembly>();
        internal Assembly Resolve(object sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;
            string filename = args.Name.Split(',')[0].ToLower();

            if (asmlookup.TryGetValue(filename, out string assembly))
            {
                // If it was already resolved, return it.
                if (lookup.TryGetValue(assembly, out var asm)) return asm;
                // Otherwise, resolve it.
                asm = _resolve(assembly);
                // It is important that we cache the initial assembly resolution
                // because it is possible under some circumstances to get a StackOverflowException
                // during 'OnResolveOpenTap' otherwise. This caching ensures that 'OnResolveOpenTap' is only called once.
                lookup.TryAdd(assembly, asm);
                if (Path.GetFileName(assembly).Equals("opentap.dll", StringComparison.OrdinalIgnoreCase))
                    OnResolveOpenTap(assembly, asm);
                return asm;
            }

            Console.Error.WriteLine($"Asked to resolve {filename}, but couldn't");
            return null;
        }

        private Assembly _resolve(string assembly)
        {
            byte[] TryReadBytes(string file)
            {
                try
                {
                    if (File.Exists(file))
                        return File.ReadAllBytes(file);
                }
                catch
                {
                    // Could be in use, could be unauthorized for some reason. Just return empty.
                }
                return Array.Empty<byte>();
            }

            Assembly asm;
            if (!Isolated)
                asm = Assembly.LoadFrom(assembly);
            else
            {

                // Load the assembly from memory to avoid locking the file when running isolated
                var rawAssembly = File.ReadAllBytes(assembly);
                var symbolsFile = Path.ChangeExtension(assembly, "pdb");
                byte[] rawSymbols = TryReadBytes(symbolsFile);
                asm = Assembly.Load(rawAssembly, rawSymbols);
            }

            return asm;
        }
    }

}

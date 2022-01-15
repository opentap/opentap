//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenTap.Diagnostic;

namespace OpenTap
{
    internal static class TapInitializer
    {
        public static Stopwatch watch = Stopwatch.StartNew();
        private static readonly SimpleTapAssemblyResolver tapAssemblyResolver = new SimpleTapAssemblyResolver();

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

        internal static void Initialize()
        {
            
            Console.WriteLine($"init- {TapInitializer.watch.ElapsedMilliseconds}");
            AppDomain.CurrentDomain.AssemblyResolve += tapAssemblyResolver.Resolve;
            ContinueInitialization();
        }

        internal static void ContinueInitialization()
        {
            Console.WriteLine($"cinit- {TapInitializer.watch.ElapsedMilliseconds}");
            // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
            // Remove so we avoid race condition with OpenTap AssemblyResolver.
            AppDomain.CurrentDomain.AssemblyResolve -= tapAssemblyResolver.Resolve;
            OpenTap.Log.AddListener(InitTraceListener.Instance);
            //swhile (true)
            {
                PluginManager.Search();
            }
            Console.Out.Flush();

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
        public SimpleTapAssemblyResolver()
        {
            // Console.WriteLine(Assembly.GetExecutingAssembly().CodeBase);
            Console.WriteLine($"filesearch- {TapInitializer.watch.ElapsedMilliseconds}");

            // string currentDir = Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.OpenTapInitDirectory);
            // if (currentDir == null)
            // {
                // currentDir = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                // currentDir = Path.GetDirectoryName(currentDir);
            // }
            // var assemblies = Tap.Shared.PathUtils.IterateDirectories(currentDir, "*.*", SearchOption.AllDirectories)
            //     .Where(s => s.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) || s.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
            //     .ToList();
            // foreach(var assembly in assemblies){
            //     var name = Path.GetFileNameWithoutExtension(assembly).ToLower();
            //     asmlookup[name] = assembly;
            // }
            
            Console.WriteLine($"filesearch after- {TapInitializer.watch.ElapsedMilliseconds}");
        }

        internal Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("OpenTap,") && File.Exists("OpenTap.dll"))
                return Assembly.LoadFrom("OpenTap.dll");
            
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;
            string filename = args.Name.Split(',')[0].ToLower();
                        
            Console.WriteLine($"resolve- {TapInitializer.watch.ElapsedMilliseconds} {args.Name}");
            if (asmlookup.TryGetValue(filename, out string assembly)) 
                return Assembly.LoadFrom(assembly);

            Console.Error.WriteLine($"Asked to resolve {filename}, but couldn't");
            return null;
        }
    }

}

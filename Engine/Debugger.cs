//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Reflection;

namespace OpenTap
{
    /// <summary> Debugger plugin interface for attaching custom debuggers. </summary>
    public interface IDebugger
    {
        /// <summary> Invoked when programs starts. </summary>
        void AttachDebugger();
    }

    /// <summary> Class for managing debuggers. Set the OPENTAP_DEBUGGER_ASSEMBLY environment variable to make it possible to attach debuggers. 
    /// The environment variable should contain paths to assemblies with IDebugger implementations. </summary>
    /// <remarks>
    /// To use this feature with Visual Studio, first install the Visual Studio SDK:
    ///    C:\git\opentap\bin\Debug\tap package install "Visual Studio SDK"
    /// Then set the environment variable to the location of the debugger dll in that package:
    ///    $env:OPENTAP_DEBUGGER_ASSEMBLY="C:\git\opentap\bin\Debug\Packages\Visual Studio SDK\OpenTap.VSSdk.Debugger.dll"
    /// </remarks>
    public static class DebuggerAttacher
    {
        const string DebuggerAssemblyEnv = "OPENTAP_DEBUGGER_ASSEMBLY";

        /// <summary> Attaches the debugger. </summary>
        public static void TryAttach()
        {
            var assembly = Environment.GetEnvironmentVariable(DebuggerAssemblyEnv);
            if (string.IsNullOrEmpty(assembly))
                return;

            Console.WriteLine("Attaching debugger " + assembly);
            try
            {
                var asm = PluginManager.LoadAssembly(assembly);
                var types = asm.GetExportedTypes();
                foreach (Type t in types)
                {
                    // This is done to avoid loading plugins in the tap.exe application - we cannot reference the IDebugger interface directly.
                    if (t.GetInterface("OpenTap.IDebugger") != null)
                    {
                        object dbg = Activator.CreateInstance(t);
                        var m = t.GetMethod("AttachDebugger");
                        m.Invoke(dbg, Array.Empty<object>());
                    }
                }
            }
            catch(TargetInvocationException tie)
            {
                Console.WriteLine("Exception when running IDebugger.AttachDebugger: {0}", tie.InnerException.Message);
            }
            catch(Exception e)
            {
                Console.WriteLine("Caught exception {0}", e.Message);
            }
        }
    }
}

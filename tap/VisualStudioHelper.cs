//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace tap
{
#if DEBUG && !NETCOREAPP
    /// <summary>
    /// Helper class for interacting with the visual studio debugger. Does not build on .NET Core.
    /// </summary>
    public static class VisualStudioHelper
    {
        /// <summary>
        /// Attach to the debugger.
        /// </summary>
        public static void AttemptDebugAttach()
        {
            bool requiresAttach = !Debugger.IsAttached;

            // If I don't have a debugger attached, try to attach 
            if (requiresAttach)
            {
                Stopwatch timer = Stopwatch.StartNew();
                //log.Debug("Attaching debugger.");
                int tries = 4;
                EnvDTE.DTE dte = null;
                while (tries-- > 0)
                {
                    try
                    {
                        dte = VisualStudioHelper.GetRunningInstance().FirstOrDefault();
                        if (dte == null)
                        {
                            //log.Debug("Could not attach Visual Studio debugger. No instances found or missing user privileges.");
                            return;
                        }
                        EnvDTE.Debugger debugger = dte.Debugger;
                        foreach (EnvDTE.Process program in debugger.LocalProcesses)
                        {
                            if (program.ProcessID == Process.GetCurrentProcess().Id)
                            {
                                program.Attach();
                                //log.Debug(timer, "Debugger attached.");
                                return;
                            }
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        if (ex.ErrorCode == unchecked((int)0x8001010A)) // RPC_E_SERVERCALL_RETRYLATER
                        {
                            //log.Debug("Visual Studio was busy while trying to attach. Retrying shortly.");
                            System.Threading.Thread.Sleep(500);
                        }
                        else
                        {
                            //log.Debug(ex);
                            // this probably means someone else was launching at the same time, so you blew up. try again after a brief sleep
                            System.Threading.Thread.Sleep(50);
                        }
                    }
                    catch
                    {
                        //log.Debug(ex);
                        // this probably means someone else was launching at the same time, so you blew up. try again after a brief sleep
                        System.Threading.Thread.Sleep(50);
                    }
                    finally
                    {
                        //Need to release teh com object so other processes get a chance to use it
                        VisualStudioHelper.ReleaseInstance(dte);
                    }
                }
            }
            else
            {
                //log.Debug("Debugger already attached.");
            }
        }

        /// <summary>
        /// Create binding context.
        /// </summary>
        /// <param name="reserved"></param>
        /// <param name="ppbc"></param>
        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        /// <summary>
        /// Get running objects.
        /// </summary>
        /// <param name="reserved"></param>
        /// <param name="prot"></param>
        /// <returns></returns>
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        /// <summary>
        /// Get interfaces to control currently running Visual Studio instances
        /// </summary>
        public static IEnumerable<EnvDTE.DTE> GetRunningInstance()
        {
            IRunningObjectTable rot;
            IEnumMoniker enumMoniker;
            int retVal = GetRunningObjectTable(0, out rot);

            if (retVal == 0)
            {
                rot.EnumRunning(out enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == 0)
                {
                    IBindCtx bindCtx;
                    CreateBindCtx(0, out bindCtx);
                    string displayName;
                    moniker[0].GetDisplayName(bindCtx, null, out displayName);
                    bool isVisualStudio = displayName.StartsWith("!VisualStudio");
                    if (isVisualStudio)
                    {
                        object obj;
                        rot.GetObject(moniker[0], out obj);
                        var dte = obj as EnvDTE.DTE;
                        yield return dte;
                    }
                }
            }
        }
        /// <summary>
        /// Releases the devenv instance.
        /// </summary>
        /// <param name="instance"></param>
        public static void ReleaseInstance(EnvDTE.DTE instance)
        {
            try
            {
                if (instance != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(instance);
            }
            catch (Exception ex)
            {
                //TraceSource log = Log.CreateSource("VSHelper");
                Console.WriteLine(ex);
            }
        }
    }
#endif
}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTap 
{
    /// <summary>
    /// Attempt to load VISA libraries from the system
    /// </summary>
    internal class VisaLibraryLoader : IVisaFunctionLoader
    {
        double IVisaFunctionLoader.Order => 10000;

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string filename);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procname);

        static TraceSource staticLog = Log.CreateSource("VisaLibraryLoader");

        private unsafe delegate int ViReadDelegate(int vi, byte* buffer, int count, out int retCount);
        private ViReadDelegate viReadRef;
        private unsafe delegate int ViWriteDelegate(int vi, byte* buffer, int count, out int retCount);
        private ViWriteDelegate viWriteRef;

        public VisaLibraryLoader()
        {
            Functions = Load();
        }
        
        public unsafe int viRead(int vi, ArraySegment<byte> buffer, int count, out int retCount)
        {
            if (buffer.Count < count)
                throw new ArgumentException("Amount of bytes requested is larger than the space available in buffer.");
            
            fixed (byte* p = &buffer.Array[buffer.Offset])
            {
                return viReadRef(vi, p, count, out retCount);
            }
        }
        public unsafe int viWrite(int vi, ArraySegment<byte> buffer, int count, out int retCount)
        {
            if (buffer.Count < count)
                throw new ArgumentException("Amount of bytes requested to be written is larger than the space present in buffer.");

            fixed (byte* p = &buffer.Array[buffer.Offset])
            {
                return viWriteRef(vi, p, count, out retCount);
            }
        }

        public VisaFunctions? Functions { get; }

        // Required by .NET to catch AccessViolationException.
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        public VisaFunctions? Load()
        {
            try
            {
                Func<string, IntPtr> loadSym;
                if (OperatingSystem.Current == OperatingSystem.Windows)
                {
                    var libHandle = LoadLibrary("visa32.dll");
                    loadSym = name => GetProcAddress(libHandle, name);
                }
                else if (OperatingSystem.Current == OperatingSystem.Linux)
                {
                    string[] paths =
                    {
                        "./libvisa32.so", "./libvisa.so", "libvisa32.so", "libvisa.so", "libiovisa.so", "./libiovisa.so"
                    };
                    var libHandle = paths.Select(LibDl.Load).FirstOrDefault(x => x != IntPtr.Zero);
                    loadSym = (name) => LibDl.Sym(libHandle, name);
                }
                else if (OperatingSystem.Current == OperatingSystem.MacOS)
                {
                    string[] paths = { "./VISA", "/Library/Frameworks/VISA.framework/VISA" };
                    var libHandle = paths.Select(LibDl.Load).FirstOrDefault(x => x != IntPtr.Zero);
                    loadSym = (name) => LibDl.Sym(libHandle, name);
                }
                else
                {
                    staticLog.Debug("Unknown OS: {0}", OpenTap.OperatingSystem.Current);
                    return null;
                }

                var functions = new VisaFunctions();
                functions.ViOpenDefaultRmRef = GetSymbol<VisaFunctions.ViOpenDefaultRmDelegate>("viOpenDefaultRM");
                functions.ViFindRsrcRef = GetSymbol<VisaFunctions.ViFindRsrcDelegate>("viFindRsrc");
                functions.ViFindNextRef = GetSymbol<VisaFunctions.ViFindNextDelegate>("viFindNext");
                functions.ViParseRsrcRef = GetSymbol<VisaFunctions.ViParseRsrcDelegate>("viParseRsrc");
                functions.ViParseRsrcExRef = GetSymbol<VisaFunctions.ViParseRsrcExDelegate>("viParseRsrcEx");
                functions.ViOpenRef = GetSymbol<VisaFunctions.ViOpenDelegate>("viOpen");
                functions.ViCloseRef = GetSymbol<VisaFunctions.ViCloseDelegate>("viClose");
                functions.ViReadRef = viRead;
                viReadRef = GetSymbol<ViReadDelegate>("viRead");
                functions.ViWriteRef = viWrite;
                viWriteRef = GetSymbol<ViWriteDelegate>("viWrite");
                functions.ViReadStbRef = GetSymbol<VisaFunctions.ViReadStbDelegate>("viReadSTB");
                functions.ViClearRef = GetSymbol<VisaFunctions.ViClearDelegate>("viClear");
                functions.ViLockRef = GetSymbol<VisaFunctions.ViLockDelegate>("viLock");
                functions.ViUnlockRef = GetSymbol<VisaFunctions.ViUnlockDelegate>("viUnlock");
                functions.ViGetAttribute1Ref = GetSymbol<VisaFunctions.ViGetAttributeBDelegate>("viGetAttribute");
                functions.ViGetAttribute2Ref = GetSymbol<VisaFunctions.ViGetAttributeSbDelegate>("viGetAttribute");
                functions.ViGetAttribute3Ref = GetSymbol<VisaFunctions.ViGetAttributeIDelegate>("viGetAttribute");
                functions.ViSetAttribute1Ref = GetSymbol<VisaFunctions.ViSetAttributeBDelegate>("viSetAttribute");
                functions.ViSetAttribute2Ref = GetSymbol<VisaFunctions.ViSetAttributeIDelegate>("viSetAttribute");
                functions.ViStatusDescRef = GetSymbol<VisaFunctions.ViStatusDescDelegate>("viStatusDesc");
                functions.ViEnableEventRef = GetSymbol<VisaFunctions.ViEnableEventDelegate>("viEnableEvent");
                functions.ViDisableEventRef = GetSymbol<VisaFunctions.ViDisableEventDelegate>("viDisableEvent");
                functions.ViInstallHandlerRef = GetSymbol<VisaFunctions.ViInstallHandlerDelegate>("viInstallHandler");
                functions.ViUninstallHandlerRef = GetSymbol<VisaFunctions.ViUninstallHandlerDelegate>("viUninstallHandler");
                functions.ViWaitOnEventRef = GetSymbol<VisaFunctions.ViWaitOnEventDelegate>("viWaitOnEvent");

                return functions;


                T GetSymbol<T>(string str)
                {
                    return Marshal.GetDelegateForFunctionPointer<T>(loadSym(str));
                }
            }
            catch (Exception ex)
            {
                staticLog.Debug("External VISA library not loaded: {0}", ex);
                return null;
            }
        }
    }
}
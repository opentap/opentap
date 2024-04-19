//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace OpenTap 
{
    /// <summary>
    /// Attempt to load VISA libraries from the system
    /// </summary>
    internal class VisaLibraryLoader : IVisa, IVisaProvider
    {
        private bool _loaded = false;
        #region IVisaProvider
        double IVisaProvider.Order => 10000;
        IVisa IVisaProvider.Visa => _loaded ? this : null;
        #endregion

        /// <summary>Event handler prototype</summary>
        public delegate int viEventHandler(int vi, int eventType, int context, int userHandle);

        #region Delegates
        private delegate int viOpenDefaultRMDelegate(out int sesn);
        private delegate int viFindRsrcDelegate(int sesn, string expr, out int vi, out int retCount, StringBuilder desc);
        private delegate int viFindNextDelegate(int vi, StringBuilder desc);
        private delegate int viParseRsrcDelegate(int sesn, string desc, ref short intfType, ref short intfNum);
        private delegate int viParseRsrcExDelegate(int sesn, string desc, ref short intfType, ref short intfNum, StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists);
        private delegate int viOpenDelegate(int sesn, string viDesc, int mode, int timeout, out int vi);
        private delegate int viCloseDelegate(int vi);
        private delegate int viGetAttributeDelegate1(int vi, int attrName, out byte attrValue);
        private delegate int viGetAttributeDelegate2(int vi, int attrName, StringBuilder attrValue);
        private delegate int viGetAttributeDelegate3(int vi, int attrName, out int attrValue);
        private delegate int viSetAttributeDelegate1(int vi, int attrName, byte attrValue);
        private delegate int viSetAttributeDelegate2(int vi, int attrName, int attrValue);
        private delegate int viStatusDescDelegate(int vi, int status, StringBuilder desc);
        private delegate int viEnableEventDelegate(int vi, int eventType, short mechanism, int context);
        private delegate int viDisableEventDelegate(int vi, int eventType, short mechanism);
        private delegate int viInstallHandlerDelegate(int vi, int eventType, IVisa.viEventHandler handler, int UserHandle);
        private delegate int viUninstallHandlerDelegate(int vi, int eventType, IVisa.viEventHandler handler, int userHandle);
        private delegate int viInstallHandlerDelegate2(int vi, int eventType, IVisa.viEventHandler handler, int UserHandle);
        private delegate int viUninstallHandlerDelegate2(int vi, int eventType, IVisa.viEventHandler handler, int userHandle);
        private unsafe delegate int viReadDelegate(int vi, byte* buffer, int count, out int retCount);
        private unsafe delegate int viWriteDelegate(int vi, byte* buffer, int count, out int retCount);
        private unsafe delegate int viReadDelegate2(int vi, ArraySegment<byte> buffer, int count, out int retCount);
        private unsafe delegate int viWriteDelegate2(int vi, ArraySegment<byte> buffer, int count, out int retCount);
        private delegate int viReadSTBDelegate(int vi, ref short status);
        private delegate int viClearDelegate(int vi);
        private delegate int viLockDelegate(int vi, int lockType, int timeout, string requestedKey, StringBuilder accessKey);
        private delegate int viUnlockDelegate(int vi);

        private viOpenDefaultRMDelegate viOpenDefaultRMRef;
        private viFindRsrcDelegate viFindRsrcRef;
        private viFindNextDelegate viFindNextRef;
        private viParseRsrcDelegate viParseRsrcRef;
        private viParseRsrcExDelegate viParseRsrcExRef;
        private viOpenDelegate viOpenRef;
        private viCloseDelegate viCloseRef;
        private viGetAttributeDelegate1 viGetAttribute1Ref;
        private viGetAttributeDelegate2 viGetAttribute2Ref;
        private viGetAttributeDelegate3 viGetAttribute3Ref;
        private viSetAttributeDelegate1 viSetAttribute1Ref;
        private viSetAttributeDelegate2 viSetAttribute2Ref;
        private viStatusDescDelegate viStatusDescRef;
        private viEnableEventDelegate viEnableEventRef;
        private viDisableEventDelegate viDisableEventRef;
        private viInstallHandlerDelegate viInstallHandlerRef;
        private viUninstallHandlerDelegate viUninstallHandlerRef;
        private viReadDelegate viReadRef;
        private viWriteDelegate viWriteRef;
        private viReadSTBDelegate viReadSTBRef;
        private viClearDelegate viClearRef;
        private viLockDelegate viLockRef;
        private viUnlockDelegate viUnlockRef;

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string filename);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procname);
        static IntPtr NullPtr()
        {
            return IntPtr.Zero;
        }

        static T GetSymbol<T>(IntPtr s)
        {
            return Marshal.GetDelegateForFunctionPointer<T>(s);
        }
        static TraceSource staticLog = OpenTap.Log.CreateSource("VisaLibraryLoader");
        private void Load()
        {
            try
            {
                bool IsWin32 = OpenTap.OperatingSystem.Current == OpenTap.OperatingSystem.Windows; 
                bool IsLinux = OpenTap.OperatingSystem.Current == OpenTap.OperatingSystem.Linux;
                bool IsMacOS = OpenTap.OperatingSystem.Current == OpenTap.OperatingSystem.MacOS;

                Func<IntPtr> LoadLib;
                Func<IntPtr, string, int, IntPtr> LoadSym;
                
                if (IsWin32)
                {
                    LoadLib = () => LoadLibrary("visa32.dll");
                    LoadSym = (lib_handle, name, ord) => GetProcAddress(lib_handle, name);
                }
                else if (IsLinux)
                {
                    LoadLib = () =>
                    {
                        string[] paths = {"./libvisa32.so", "./libvisa.so", "libvisa32.so", "libvisa.so", "libiovisa.so", "./libiovisa.so"};
                        return paths.Select(LibDl.Load) .FirstOrDefault(x => x != IntPtr.Zero);
                    };
                    LoadSym = (lib_handle, name, ord) => LibDl.Sym(lib_handle, name);
                } 
                else if (IsMacOS)
                {
                    LoadLib = () =>
                    {
                        string[] paths = {"./VISA", "/Library/Frameworks/VISA.framework/VISA"};
                        return paths.Select(LibDl.Load) .FirstOrDefault(x => x != IntPtr.Zero);
                    };
                    LoadSym = (lib_handle, name, ord) => LibDl.Sym(lib_handle, name);
                }
                else 
                {
                    staticLog.Debug("Unknown OS: {0}", OpenTap.OperatingSystem.Current);
                    _loaded = false;
                    LoadLib = () => NullPtr();
                    return;
                }

                var lib = LoadLib();

                viOpenDefaultRMRef = GetSymbol<viOpenDefaultRMDelegate>(LoadSym(lib, "viOpenDefaultRM", 141));

                viFindRsrcRef = GetSymbol<viFindRsrcDelegate>(LoadSym(lib, "viFindRsrc", 129));
                viFindNextRef = GetSymbol<viFindNextDelegate>(LoadSym(lib, "viFindNext", 130));
                viParseRsrcRef = GetSymbol<viParseRsrcDelegate>(LoadSym(lib, "viParseRsrc", 146));
                viParseRsrcExRef = GetSymbol<viParseRsrcExDelegate>(LoadSym(lib, "viParseRsrcEx", 147));

                viOpenRef = GetSymbol<viOpenDelegate>(LoadSym(lib, "viOpen", 131));
                viCloseRef = GetSymbol<viCloseDelegate>(LoadSym(lib, "viClose", 132));

                viReadRef = GetSymbol<viReadDelegate>(LoadSym(lib, "viRead", 256));
                viWriteRef = GetSymbol<viWriteDelegate>(LoadSym(lib, "viWrite", 257));
                viReadSTBRef = GetSymbol<viReadSTBDelegate>(LoadSym(lib, "viReadSTB", 259));
                viClearRef = GetSymbol<viClearDelegate>(LoadSym(lib, "viClear", 260));

                viLockRef = GetSymbol<viLockDelegate>(LoadSym(lib, "viLock", 144));
                viUnlockRef = GetSymbol<viUnlockDelegate>(LoadSym(lib, "viUnlock", 145));

                viGetAttribute1Ref = GetSymbol<viGetAttributeDelegate1>(LoadSym(lib, "viGetAttribute", 133));
                viGetAttribute2Ref = GetSymbol<viGetAttributeDelegate2>(LoadSym(lib, "viGetAttribute", 133));
                viGetAttribute3Ref = GetSymbol<viGetAttributeDelegate3>(LoadSym(lib, "viGetAttribute", 133));
                viSetAttribute1Ref = GetSymbol<viSetAttributeDelegate1>(LoadSym(lib, "viSetAttribute", 134));
                viSetAttribute2Ref = GetSymbol<viSetAttributeDelegate2>(LoadSym(lib, "viSetAttribute", 134));

                viStatusDescRef = GetSymbol<viStatusDescDelegate>(LoadSym(lib, "viStatusDesc", 142));

                viEnableEventRef = GetSymbol<viEnableEventDelegate>(LoadSym(lib, "viEnableEvent", 135));
                viDisableEventRef = GetSymbol<viDisableEventDelegate>(LoadSym(lib, "viDisableEvent", 136));
                viInstallHandlerRef = GetSymbol<viInstallHandlerDelegate>(LoadSym(lib, "viInstallHandler", 139));
                viUninstallHandlerRef = GetSymbol<viUninstallHandlerDelegate>(LoadSym(lib, "viUninstallHandler", 140));
                _loaded = true;
            }
            catch (Exception ex)
            {
                staticLog.Debug("External VISA library not loaded: {0}", ex);
                this._loaded = false;
            }
        }
        /// <summary>
        /// Attempt to load VISA libraries from the system
        /// </summary>
        public VisaLibraryLoader()
        {
            // load in method so that we can handle AccessViolationException
            this.Load();            
        }
        #endregion

        /// <summary>Open default RM session</summary>
        public int viOpenDefaultRM(out int sesn) { return viOpenDefaultRMRef(out sesn); }
        /// <summary>Find device</summary>
        public int viFindRsrc(int sesn, string expr, out int vi, out int retCount, StringBuilder desc) { return viFindRsrcRef(sesn, expr, out vi, out retCount, desc); }
        /// <summary>Find next device</summary>
        public int viFindNext(int vi, StringBuilder desc) { return viFindNextRef(vi, desc); }
        /// <summary>Parse resource string to get interface information</summary>
        public int viParseRsrc(int sesn, string desc, ref short intfType, ref short intfNum) { return viParseRsrcRef(sesn, desc, ref intfType, ref intfNum); }
        /// <summary>Parse resource string to get extended interface information</summary>
        public int viParseRsrcEx(int sesn, string desc, ref short intfType, ref short intfNum, StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists) { return viParseRsrcExRef(sesn, desc, ref intfType, ref intfNum, rsrcClass, expandedUnaliasedName, aliasIfExists); }
        /// <summary>Open session</summary>
        public int viOpen(int sesn, string viDesc, int mode, int timeout, out int vi) { return viOpenRef(sesn, viDesc, mode, timeout, out vi); }
        /// <summary>Close session</summary>
        public int viClose(int vi) { return viCloseRef(vi); }
        
        /// <summary>Get attribute, returning a byte</summary>
        public int viGetAttribute1(int vi, int attrName, out byte attrValue) { return viGetAttribute1Ref(vi, attrName, out attrValue); }
        /// <summary>Get attribute, filling a pre-allocated StringBuilder buffer</summary>
        public int viGetAttribute2(int vi, int attrName, StringBuilder attrValue) { return viGetAttribute2Ref(vi, attrName, attrValue); }
        /// <summary>Get attribute, returning an int</summary>
        public int viGetAttribute3(int vi, int attrName, out int attrValue) { return viGetAttribute3Ref(vi, attrName, out attrValue); }
        /// <summary>Set attribute, providing a byte value</summary>
        public int viSetAttribute1(int vi, int attrName, byte attrValue) { return viSetAttribute1Ref(vi, attrName, attrValue); }
        /// <summary>Set attribute, providing an int value</summary>
        public int viSetAttribute2(int vi, int attrName, int attrValue) { return viSetAttribute2Ref(vi, attrName, attrValue); }
        /// <summary>Get status code description</summary>
        public int viStatusDesc(int vi, int status, StringBuilder desc) { return viStatusDescRef(vi, status, desc); }

        /// <summary>Enable event</summary>
        public int viEnableEvent(int vi, int eventType, short mechanism, int context) { return viEnableEventRef(vi, eventType, mechanism, context); }
        /// <summary>Disable event</summary>
        public int viDisableEvent(int vi, int eventType, short mechanism) { return viDisableEventRef(vi, eventType, mechanism); }
        /// <summary>Install handler</summary>
        public int viInstallHandler(int vi, int eventType, IVisa.viEventHandler handler, int UserHandle) { return viInstallHandlerRef(vi, eventType, handler, UserHandle); }
        /// <summary>Uninstall handler</summary>
        public int viUninstallHandler(int vi, int eventType, IVisa.viEventHandler handler, int userHandle) { return viUninstallHandlerRef(vi, eventType, handler, userHandle); }
        /// <summary>Read data from device</summary>
        public unsafe int viRead(int vi, ArraySegment<byte> buffer, int count, out int retCount)
        {
            if (buffer.Count < count)
                throw new ArgumentException("Amount of bytes requested is larger than the space available in buffer.");
            
            fixed (byte* p = &buffer.Array[buffer.Offset])
            {
                return viReadRef(vi, p, count, out retCount);
            }
        }
        /// <summary>Write data to device</summary>
        public unsafe int viWrite(int vi, ArraySegment<byte> buffer, int count, out int retCount)
        {
            if (buffer.Count < count)
                throw new ArgumentException("Amount of bytes requested to be written is larger than the space present in buffer.");

            fixed (byte* p = &buffer.Array[buffer.Offset])
            {
                return viWriteRef(vi, p, count, out retCount);
            }
        }
        /// <summary>Read status byte</summary>
        public int viReadSTB(int vi, ref short status) { return viReadSTBRef(vi, ref status); }
        /// <summary>Clear a device</summary>
        public int viClear(int vi) { return viClearRef(vi); }
        /// <summary>Lock resource</summary>
        public int viLock(int vi, int lockType, int timeout, string requestedKey, StringBuilder accessKey) { return viLockRef(vi, lockType, timeout, requestedKey, accessKey); }
        /// <summary>Unlock resource</summary>
        public int viUnlock(int vi) { return viUnlockRef(vi); }
    }
}
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTap;

/// <summary>
///  This class uses and resolves libdl, which dependning on the OS comes in a libdl.so or libdl.so.2 flavor.
/// </summary>
static class LibDl
{
    static IntPtr load(string name) => libDl.dlopen(name, rtld_now);
    static void close(IntPtr ptr) => libDl.dlclose(ptr);

    /// <summary> Gets most recent load error. </summary>
    public static string GetError()
    {
        var error = libDl.dlerror();
        if (IntPtr.Zero == error) return null;
        return Marshal.PtrToStringAuto(error);
    }
    
    static void clearError() => libDl.dlerror();

    const int rtld_now = 2;

    interface ILibDL
    {
        IntPtr dlopen(string filename, int flags);
        int dlclose(IntPtr handle);
        IntPtr dlerror();
        IntPtr dlsym(IntPtr handle, string symbol);
    }

    /// <summary>
    /// libc5 (shipped with Ubuntu 20.04 and older) and older contains this libdl version, but it is not shipped with libc6
    /// </summary>
    class libdl1 : ILibDL
    {
        private const string libName = "libdl.so";
        [DllImport(libName)]
        static extern IntPtr dlopen(string fileName, int flags);
        [DllImport(libName)]
        static extern int dlclose(IntPtr handle);
        [DllImport(libName)]
        static extern IntPtr dlerror();
        [DllImport(libName)]
        static extern IntPtr dlsym(IntPtr handle, string symbol);


        IntPtr ILibDL.dlopen(string fileName, int flags) => dlopen(fileName, flags);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
    }

    /// <summary>
    /// libc6 (shipped with Ubuntu 22.04) only ships libdl.so.2. This seems to be the only way to resolve it.
    /// Trying "libdl" or just "dl" also does not seem to resolve to libdl.so.2. 
    /// </summary>
    class libdl2 : ILibDL
    {
        private const string libName = "libdl.so.2";
        [DllImport(libName)]
        static extern IntPtr dlopen(string fileName, int flags);
        [DllImport(libName)]
        static extern int dlclose(IntPtr handle);
        [DllImport(libName)]
        static extern IntPtr dlerror();
        [DllImport(libName)]
        static extern IntPtr dlsym(IntPtr handle, string symbol);
        
        IntPtr ILibDL.dlopen(string fileName, int flags) => dlopen(fileName, flags);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
    }
    
    /// <summary>
    /// MacOS calls it libdl
    /// </summary>
    class libdlMac : ILibDL
    {
        private const string libName = "libdl";
        [DllImport(libName)]
        static extern IntPtr dlopen(string fileName, int flags);
        [DllImport(libName)]
        static extern int dlclose(IntPtr handle);
        [DllImport(libName)]
        static extern IntPtr dlerror();
        [DllImport(libName)]
        static extern IntPtr dlsym(IntPtr handle, string symbol);
        
        IntPtr ILibDL.dlopen(string fileName, int flags) => dlopen(fileName, flags);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
    }
    
    static readonly ILibDL libDl;

    static LibDl()
    {
        if (OpenTap.OperatingSystem.Current == OpenTap.OperatingSystem.MacOS)
        {
            libDl = new libdlMac();
        }
        else 
        {
            try
            {
                libDl = new libdl2();
                // call dlerror to ensure library is resolved
                libDl.dlerror();
            }
            catch (DllNotFoundException)
            {
                libDl = new libdl1();
            }
        }
    }

    public static IntPtr Sym(IntPtr lib, string name) => libDl.dlsym(lib, name);

    public static IntPtr Load(string name)
    {
        clearError();
        return load(name);
    }

    public static void Unload(IntPtr lib)
    {
        if (lib == IntPtr.Zero)
            throw new NullReferenceException();
        close(lib);
    }
}
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

static class LibDl
{
    static IntPtr load(string name) => libdl.dlopen(name, rtld_now);
    static void close(IntPtr ptr) => libdl.dlclose(ptr);

    public static string GetError()
    {
        var error = libdl.dlerror();
        if (IntPtr.Zero == error) return null;
        return Marshal.PtrToStringAuto(error);

    }
    
    static void checkError()
    {
        var error = libdl.dlerror();
        if (error != IntPtr.Zero)
            throw new Exception("Unable to load python: " + error.ToString());
    }

    static void clearError() => libdl.dlerror();

    const int rtld_now = 2;

    interface ILibDL
    {
        IntPtr dlopen(string filename, int flags);
        int dlclose(IntPtr handle);
        IntPtr dlerror();
        IntPtr dlsym(IntPtr handle, string symbol);
    }

    class libdl1 : ILibDL
    {
        [DllImport("libdl.so")]
        static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libdl.so")]
        static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so")]
        static extern IntPtr dlerror();
        
        [DllImport("libdl.so.2")]
        static extern IntPtr dlsym(IntPtr handle, string symbol);


        IntPtr ILibDL.dlopen(string fileName, int flags) => dlopen(fileName, flags);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);

    }

    class libdl2 : ILibDL
    {
        [DllImport("libdl.so.2")]
        static extern IntPtr dlopen(string fileName, int flags);
        
        [DllImport("libdl.so.2")]
        static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so.2")]
        static extern IntPtr dlerror();
        
        [DllImport("libdl.so.2")]
        static extern IntPtr dlsym(IntPtr handle, string symbol);
        
        
        IntPtr ILibDL.dlopen(string fileName, int flags) => dlopen(fileName, flags);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();

        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
    }

    static readonly ILibDL libdl;

    static LibDl()
    {
        try
        {
            libdl = new libdl2();
            // call dlerror to ensure library is resolved
            libdl.dlerror();
        }
        catch (DllNotFoundException)
        {
            libdl = new libdl1();
        }
    }

    public static IntPtr Sym(IntPtr lib, string name) => libdl.dlsym(lib, name);

    public static IntPtr Load(string name)
    {
        clearError();
        IntPtr p = load(name);
        if (p == IntPtr.Zero)
            return IntPtr.Zero;
        return p;
    }

    public static void Unload(IntPtr lib)
    {
        if (lib == IntPtr.Zero)
            throw new NullReferenceException();
        close(lib);
    }
}
using System;
using System.Reflection;
using System.Runtime.Loader;

namespace tap
{
    class DisposableLoadContext : AssemblyLoadContext, IDisposable
    {

        public DisposableLoadContext(string pluginPath) : base(true)
        {
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return base.Load(assemblyName);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            return base.LoadUnmanagedDll(unmanagedDllName);
        }

        public void Dispose()
        {
            if (IsCollectible) 
                Unload();
        }
    }
}
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    internal static class GitVersionNativeLibrary
    {
        private const string LibGit2Name = "libgit2-b7bad55";
        private const string LibGit2Directory = "Dependencies/LibGit2Sharp.0.27.0.0";
        private static readonly object loadLock = new object();
        private static IntPtr nativeLibraryHandle;

        internal static string GetPath(string taskDirectory)
        {
            var architecture = GetArchitecture();
            string packageRuntime;
            string packageFileName;
            string nativeRuntime;
            string nativeFileName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                packageRuntime = nativeRuntime = "win-" + architecture;
                packageFileName = $"git2-b7bad55.dll.{architecture}";
                nativeFileName = "git2-b7bad55.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                packageRuntime = "macos-" + architecture;
                packageFileName = $"{LibGit2Name}.dylib.{architecture}";
                nativeRuntime = "osx-" + architecture;
                nativeFileName = $"{LibGit2Name}.dylib";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var musl = IsMusl();
                packageRuntime = "linux-" + architecture;
                packageFileName = musl
                    ? $"{LibGit2Name}.so.musl.{architecture}"
                    : $"{LibGit2Name}.so.{architecture}";
                nativeRuntime = (musl ? "linux-musl-" : "linux-") + architecture;
                nativeFileName = $"{LibGit2Name}.so";
            }
            else
            {
                throw new PlatformNotSupportedException("Git version calculation is not supported on this operating system.");
            }

            // In the OpenTAP NuGet package, libgit2 is part of each OpenTAP runtime payload.
            var packagePath = Path.Combine(taskDirectory, "runtimes", packageRuntime, LibGit2Directory, packageFileName);
            if (File.Exists(packagePath))
                return packagePath;

            // The project build output uses the original NativeBinaries NuGet layout.
            return Path.Combine(taskDirectory, "runtimes", nativeRuntime, "native", nativeFileName);
        }

        internal static void Load(string path)
        {
            lock (loadLock)
            {
                if (nativeLibraryHandle != IntPtr.Zero)
                    return;

                // MSBuild loads tasks into a plugin AssemblyLoadContext. Registering its unmanaged
                // resolver makes the RID-specific asset visible to LibGit2Sharp in that context.
                var loadContextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
                var getLoadContext = loadContextType?.GetMethod("GetLoadContext", new[] { typeof(Assembly) });
                var resolvingEvent = loadContextType?.GetEvent("ResolvingUnmanagedDll");
                if (getLoadContext != null && resolvingEvent != null)
                {
                    var context = getLoadContext.Invoke(null, new object[] { typeof(LibGit2Sharp.Repository).Assembly });
                    var resolver = Delegate.CreateDelegate(resolvingEvent.EventHandlerType,
                        typeof(GitVersionNativeLibrary).GetMethod(nameof(ResolveUnmanagedDll), BindingFlags.NonPublic | BindingFlags.Static));
                    resolvingEvent.AddEventHandler(context, resolver);
                }

                nativeLibraryHandle = LoadNativeLibrary(path);
                if (nativeLibraryHandle == IntPtr.Zero)
                    throw new DllNotFoundException($"Unable to load native libgit2 library '{path}'.");
            }
        }

        private static IntPtr ResolveUnmanagedDll(Assembly assembly, string libraryName)
        {
            return libraryName.IndexOf("git2", StringComparison.OrdinalIgnoreCase) >= 0
                ? nativeLibraryHandle
                : IntPtr.Zero;
        }

        private static IntPtr LoadNativeLibrary(string path)
        {
            // NativeLibrary is supplied by the build host rather than netstandard2.0, so access it
            // through reflection while retaining compatibility with desktop MSBuild.
            var nativeLibraryType = Type.GetType("System.Runtime.InteropServices.NativeLibrary");
            var load = nativeLibraryType?.GetMethod("Load", new[] { typeof(string) });
            if (load != null)
                return (IntPtr)load.Invoke(null, new object[] { path });

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? LoadLibrary(path)
                : dlopen(path, RTLD_NOW | RTLD_GLOBAL);
        }

        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string fileName, int flags);

        private static string GetArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86: return "x86";
                case Architecture.X64: return "x64";
                case Architecture.Arm: return "arm";
                case Architecture.Arm64: return "arm64";
                default: throw new PlatformNotSupportedException($"Git version calculation is not supported for {RuntimeInformation.ProcessArchitecture} processes.");
            }
        }

        private static class GlibC
        {
            [DllImport("libc")]
            internal static extern IntPtr gnu_get_libc_version();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsMusl()
        {
            try
            {
                return GlibC.gnu_get_libc_version() == IntPtr.Zero;
            }
            catch
            {
                return true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

namespace OpenTap.Package.FilesInUse;

using DWORD = uint;
using UINT = uint;
using ULONG = uint; // yes this is real
using WCHAR = ushort;
using BOOL = int;
/// <summary>
///  Implementation based on this blog post: https://devblogs.microsoft.com/oldnewthing/20120217-00/?p=8283
/// </summary>
internal class RestartManager : IDisposable
{ 
    static IntPtr CreateCString(string path)
    {
        IntPtr ptr = Marshal.AllocHGlobal((path.Length + 1) * sizeof(char));
        Marshal.Copy(path.ToCharArray(), 0, ptr, path.Length);
        Marshal.WriteInt16(ptr + path.Length * sizeof(char), 0);
        return ptr;
    } 

    static IntPtr CreateCArray(IEnumerable<IntPtr> pointers)
    {
        var ptrs = pointers.ToArray();
        IntPtr ptr = Marshal.AllocHGlobal(ptrs.Length * IntPtr.Size);
        for (int i = 0; i < ptrs.Length; i++)
        {
            var offset = i * IntPtr.Size;
            Marshal.WriteIntPtr(ptr + offset, ptrs[i]);
        }

        return ptr;
    } 
    public delegate void RmWriteStatusCallback(UINT nPercentComplete);
    private static class Interop
    {
        private const DWORD ERROR_SUCCESS = 0;
        private const DWORD ERROR_MORE_DATA = 234;
        [DllImport("Rstrtmgr")]
        private static extern DWORD RmEndSession(DWORD pSessionHandle);
        [DllImport("Rstrtmgr")]
        private static extern DWORD RmStartSession(ref DWORD pSessionHandle, DWORD dwSessionFlags, WCHAR[] strSessionKey);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmRegisterResources(DWORD dwSessionHandle, 
            UINT nFiles, IntPtr rgsFileNames,
            UINT nApplications, RM_UNIQUE_PROCESS[] rgApplications, 
            UINT nServices, string[] rgsServiceNames);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmGetList(DWORD dwSessionHandle, ref UINT nProcInfoNeeded, ref UINT nProcInfo,
            IntPtr rgAffectedApps, ref DWORD dwRebootReasons);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmShutdown(DWORD dwSesssionHandle, ULONG lActionFlags, RmWriteStatusCallback fnStatus);
        [DllImport("Rstrtmgr")]
        private static extern DWORD RmRestart(DWORD dwSesssionHandle, DWORD dwRestartFlags, RmWriteStatusCallback fnStatus);
        public static bool EndSession(DWORD sessionHandle)
        {
            return RmEndSession(sessionHandle) == ERROR_SUCCESS;
        }

        public static bool Shutdown(DWORD sessionHandle, bool closeAll, RmWriteStatusCallback statusCallback)
        {
            const ULONG CloseAll = 0x1;
            const ULONG CloseOnlyRestartable = 0x10;
            return RmShutdown(sessionHandle, closeAll ? CloseAll : CloseOnlyRestartable, statusCallback) == ERROR_SUCCESS;
        }
        public static bool Restart(DWORD sessionHandle, RmWriteStatusCallback statusCallback)
        {
            return RmRestart(sessionHandle, 0, statusCallback) == ERROR_SUCCESS;
        }
        public static bool StartSession(ref DWORD sessionHandle)
        {
            WCHAR[] szSessionKey = new WCHAR[CCH_RM_SESSION_KEY + 1];
            return RmStartSession(ref sessionHandle, 0, szSessionKey) == ERROR_SUCCESS;
        }

        public static bool RegisterResources(DWORD sessionHandle, IEnumerable<string> paths)
        {
            var files = paths.Select(CreateCString).ToArray();
            var fileptr = CreateCArray(files); 
            var status = RmRegisterResources(sessionHandle, (UINT)files.Length, fileptr, 0, null, 0, null) == ERROR_SUCCESS;
            foreach (var f in files.Append(fileptr))
            { 
                Marshal.FreeHGlobal(f);
            }
            return status;
        } 
        
        public static bool TryGetProcesses(DWORD sessionHandle, out List<ProcessObject> processes)
        {
            processes = new();
            
            UINT nProcInfoNeeded = 0;
            UINT nProcInfo = 0;
            DWORD rebootReason = 0;
            // First check how much space we need to allocate
            var dwError = RmGetList(sessionHandle, ref nProcInfoNeeded, ref nProcInfo, IntPtr.Zero, ref rebootReason);
            if (dwError != ERROR_SUCCESS && dwError != ERROR_MORE_DATA) return false; 
            
            // Now allocate that space and query again
            var rgpiPtr = Marshal.AllocHGlobal((int)nProcInfoNeeded * Marshal.SizeOf<RM_PROCESS_INFO>());
            nProcInfo = nProcInfoNeeded;
            dwError = RmGetList(sessionHandle, ref nProcInfoNeeded, ref nProcInfo, rgpiPtr, ref rebootReason);
            if (dwError == ERROR_SUCCESS)
            {
                for (int i = 0; i < nProcInfo; i++)
                {
                    var offset = i * Marshal.SizeOf<RM_PROCESS_INFO>();
                    var rgpi = Marshal.PtrToStructure<RM_PROCESS_INFO>(rgpiPtr + offset);
                    var pid = (int)rgpi.Process.dwProcessId;
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        processes.Add(new ProcessObject(proc, rgpi.bRestartable != 0));
                    }
                    catch
                    {
                        // Process may have closed before we queried it.
                    }
                }
            }


            Marshal.FreeHGlobal(rgpiPtr);
            return dwError == ERROR_SUCCESS;
        }
        
    }
    
    const int CCH_RM_SESSION_KEY = 32;
    private DWORD sessionId;

    public RestartManager()
    {
        if (!Interop.StartSession(ref sessionId))
            throw new Exception($"Unable to create restart manager.");
    }
    private static readonly TraceSource log = Log.CreateSource("RestartManager");

    public void RegisterFiles(IEnumerable<string> paths)
    {
        Interop.RegisterResources(sessionId, paths);
    }

    public struct ProcessObject
    {
        public ProcessObject(Process process, bool restartable)
        {
            Process = process;
            Restartable = restartable;
        }

        public Process Process { get; }
        public bool Restartable { get; }
        public override string ToString()
        {
            var prettyName = Process.ProcessName;
            try
            {
                if (!string.IsNullOrWhiteSpace(Process.MainWindowTitle))
                    prettyName = Process.MainWindowTitle;
            }
            catch
            {
                // ignore
            }

            var prefix = Restartable ? "* " : "";
            var postfix = $" (PID: {Process.Id})";

            return prefix + prettyName + postfix;
        }
    }
    public List<ProcessObject> GetProcessesUsingFiles()
    {
        if (OperatingSystem.Current != OperatingSystem.Windows) return [];
        Interop.TryGetProcesses(sessionId, out var procs);
        return procs;
    }

    public void Shutdown(RmWriteStatusCallback callback, bool force)
    {
        Interop.Shutdown(sessionId, force, callback);
    }

    public void Restart(RmWriteStatusCallback callback)
    {
        Interop.Restart(sessionId, callback);
    }

    public void Dispose()
    {
        Interop.EndSession(sessionId);
    }
}

// data structs -- do not change!!

[StructLayout(LayoutKind.Sequential)]
struct FILETIME
{
     public DWORD dwLowDateTime;
     public DWORD dwHighDateTime; 
}

[StructLayout(LayoutKind.Sequential)]
struct RM_UNIQUE_PROCESS
{
    public DWORD dwProcessId;
    public FILETIME ProcessStartTime;
}

enum RM_APP_TYPE
{
    RmUnknownApp = 0,
    RmMainWindow = 1,
    RmOtherWindow = 2,
    RmService = 3,
    RmExplorer = 4,
    RmConsole = 5,
    RmCritical = 1000
}

// https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/ns-restartmanager-rm_process_info
[StructLayout(LayoutKind.Sequential)]
struct RM_PROCESS_INFO
{
    const int CCH_RM_MAX_APP_NAME = 255;
    const int CCH_RM_MAX_SVC_NAME = 63;
    public RM_UNIQUE_PROCESS Process;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCH_RM_MAX_APP_NAME+1)]
    public WCHAR[,] strAppName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCH_RM_MAX_SVC_NAME+1)]
    public WCHAR[,] strServiceShortName;

    public RM_APP_TYPE ApplicationType;
    public ULONG AppStatus;
    public DWORD TSessionId;
    public BOOL bRestartable;

    public string StrAppName => Marshal.PtrToStringUni(Marshal.UnsafeAddrOfPinnedArrayElement(strAppName, 0));
    public string StrServiceName => Marshal.PtrToStringUni(Marshal.UnsafeAddrOfPinnedArrayElement(strServiceShortName, 0));
}

[Browsable(false)]
[Display("who", Groups: ["files", "used", "by"])]
internal class TestFilesInUseCliAction : ICliAction
{
    private static readonly TraceSource log = Log.CreateSource("Files In Use");
    [UnnamedCommandLineArgument("Filenames")]
    public string[] Filenames { get; set; }
    public int Execute(CancellationToken cancellationToken)
    {
        var path2 = @"C:\git\tap\bin\Debug\Editor.exe";
        using var rm = new RestartManager();
        rm.RegisterFiles([path2]);
        var procs = rm.GetProcessesUsingFiles();
        var evt = new ManualResetEventSlim(false);
        rm.Shutdown(prog =>
        {
            log.Info($"Shutdown Progress: {prog}");
            if (prog >= 100)
                evt.Set();
        }, false);

        for (int i = 0; i < 3; i++)
        {
            var ct = new CancellationTokenSource();
            ct.CancelAfter(TimeSpan.FromSeconds(2));
            WaitHandle.WaitAny([evt.WaitHandle, ct.Token.WaitHandle]);
            if (evt.Wait(0))
                break;
            var procs2 = rm.GetProcessesUsingFiles();
            var procString = string.Join("\n\t", procs2.Select(p => p.ToString()));
            log.Info($"Waiting for processes to shut down:\n{procString}");
        }
        rm.Shutdown(prog =>
        {
            log.Info($"Shutdown Progress: {prog}");
            if (prog >= 100)
                evt.Set();
        }, false);
        
        evt.Reset();

        rm.Restart(prog =>
        {
            log.Info($"Restart Progress: {prog}");
            if (prog >= 100)
                evt.Set();
        });
        for (int i = 0; i < 3; i++)
        {
            var ct = new CancellationTokenSource();
            ct.CancelAfter(TimeSpan.FromSeconds(2));
            WaitHandle.WaitAny([evt.WaitHandle, ct.Token.WaitHandle]);
            if (evt.Wait(0))
                break;
            log.Info($"Waiting for processes to restart");
        }
        return 0;
    }
}
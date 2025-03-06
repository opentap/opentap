using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTap.Package;

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

    internal class RestartManagerException : Exception
    {
        public RestartManagerException(DWORD dwError) : base(RmErrors.ErrorStrings.TryGetValue(dwError, out var err)
            ? err
            : "Unknown error.")
        {
        } 
    }

    private static class RmErrors
    {
        public static void ThrowIfError(DWORD error)
        {
            if (error != ERROR_SUCCESS) throw new RestartManagerException(error);
        }
        
        // Error codes from:
        // https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmgetlist#return-value
        public const DWORD ERROR_SUCCESS = 0; // The function completed successfully. 

        public const DWORD
            ERROR_MORE_DATA =
                234; // This error value is returned by the RmGetList function if the rgAffectedApps buffer is too small to hold all application information in the list. 

        public const DWORD ERROR_CANCELLED = 1223; // The current operation is canceled by user. 

        public const DWORD
            ERROR_SEM_TIMEOUT =
                121; // A Restart Manager function could not obtain a Registry write mutex in the allotted time. A system restart is recommended because further use of the Restart Manager is likely to fail. 

        public const DWORD
            ERROR_BAD_ARGUMENTS =
                160; // One or more arguments are not correct. This error value is returned by the Restart Manager function if a NULL pointer or 0 is passed in a parameter that requires a non-null and non-zero value. 

        public const DWORD ERROR_WRITE_FAULT = 29; // An operation was unable to read or write to the registry. 

        public const DWORD
            ERROR_OUTOFMEMORY =
                14; // A Restart Manager operation could not complete because not enough memory was available. 

        public const DWORD ERROR_INVALID_HANDLE = 6; // No Restart Manager session exists for the handle supplied. 
        public const DWORD ERROR_ACCESS_DENIED = 5; // A path registered to the Restart Manager session is a directory. 

        public static readonly Dictionary<DWORD, string> ErrorStrings = new()
        {
            [ERROR_SUCCESS] = "The function completed successfully.",
            [ERROR_MORE_DATA] =
                "This error value is returned by the RmGetList function if the rgAffectedApps buffer is too small to hold all application information in the list.",
            [ERROR_CANCELLED] = "The current operation is canceled by user.",
            [ERROR_SEM_TIMEOUT] =
                "A Restart Manager function could not obtain a Registry write mutex in the allotted time. A system restart is recommended because further use of the Restart Manager is likely to fail.",
            [ERROR_BAD_ARGUMENTS] =
                "One or more arguments are not correct. This error value is returned by the Restart Manager function if a NULL pointer or 0 is passed in a parameter that requires a non-null and non-zero value.",
            [ERROR_WRITE_FAULT] = "An operation was unable to read or write to the registry.",
            [ERROR_OUTOFMEMORY] =
                "A Restart Manager operation could not complete because not enough memory was available.",
            [ERROR_INVALID_HANDLE] = "No Restart Manager session exists for the handle supplied.",
            [ERROR_ACCESS_DENIED] = "A path registered to the Restart Manager session is a directory.",
        };
    }

    public delegate void RmWriteStatusCallback(UINT nPercentComplete);

    private static class Interop
    {
        [DllImport("Rstrtmgr")]
        private static extern DWORD RmEndSession(DWORD pSessionHandle);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmStartSession(ref DWORD pSessionHandle, DWORD dwSessionFlags,
            WCHAR[] strSessionKey);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmRegisterResources(DWORD dwSessionHandle,
            UINT nFiles, IntPtr rgsFileNames,
            UINT nApplications, RM_UNIQUE_PROCESS[] rgApplications,
            UINT nServices, string[] rgsServiceNames);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmGetList(DWORD dwSessionHandle, ref UINT nProcInfoNeeded, ref UINT nProcInfo,
            IntPtr rgAffectedApps, ref DWORD dwRebootReasons);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmShutdown(DWORD dwSesssionHandle, ULONG lActionFlags,
            RmWriteStatusCallback fnStatus);

        [DllImport("Rstrtmgr")]
        private static extern DWORD RmRestart(DWORD dwSesssionHandle, DWORD dwRestartFlags,
            RmWriteStatusCallback fnStatus);

        public static DWORD EndSession(DWORD sessionHandle)
        {
            return RmEndSession(sessionHandle);
        }

        public static DWORD Shutdown(DWORD sessionHandle, bool closeAll, RmWriteStatusCallback statusCallback)
        {
            const ULONG CloseAll = 0x1;
            const ULONG CloseOnlyRestartable = 0x10;
            return RmShutdown(sessionHandle, closeAll ? CloseAll : CloseOnlyRestartable, statusCallback);
        }

        public static DWORD Restart(DWORD sessionHandle, RmWriteStatusCallback statusCallback)
        {
            return RmRestart(sessionHandle, 0, statusCallback);
        }

        public static DWORD StartSession(ref DWORD sessionHandle)
        {
            // We don't need to hold on to the session key, so we can just write it on the stack and discard it.
            WCHAR[] szSessionKey = new WCHAR[CCH_RM_SESSION_KEY + 1];
            return RmStartSession(ref sessionHandle, 0, szSessionKey);
        }

        public static DWORD RegisterResources(DWORD sessionHandle, IEnumerable<string> paths)
        {
            var files = paths.Select(CreateCString).ToArray();
            var fileptr = CreateCArray(files);
            var status = RmRegisterResources(sessionHandle, (UINT)files.Length, fileptr, 0, null, 0, null);
            foreach (var f in files.Append(fileptr))
            {
                Marshal.FreeHGlobal(f);
            } 
            return status;
        }

        public static DWORD GetList(DWORD sessionHandle, out RM_PROCESS_INFO[] processes)
        {
            processes = [];
            UINT nProcInfoNeeded = 0;
            UINT nProcInfo = 0;
            DWORD rebootReason = 0;
            // First check how much space we need to allocate
            var dwError = RmGetList(sessionHandle, ref nProcInfoNeeded, ref nProcInfo, IntPtr.Zero, ref rebootReason);
            if (dwError != RmErrors.ERROR_SUCCESS && dwError != RmErrors.ERROR_MORE_DATA)
                return dwError;

            // Now allocate that space and query again
            var rgpiPtr = Marshal.AllocHGlobal((int)nProcInfoNeeded * Marshal.SizeOf<RM_PROCESS_INFO>());
            try
            {
                nProcInfo = nProcInfoNeeded;
                dwError = RmGetList(sessionHandle, ref nProcInfoNeeded, ref nProcInfo, rgpiPtr, ref rebootReason);
                if (dwError != RmErrors.ERROR_SUCCESS)
                {
                    return dwError;
                }

                processes = new RM_PROCESS_INFO[nProcInfo];
                for (int i = 0; i < nProcInfo; i++)
                {
                    var offset = i * Marshal.SizeOf<RM_PROCESS_INFO>();
                    var rgpi = Marshal.PtrToStructure<RM_PROCESS_INFO>(rgpiPtr + offset);
                    processes[i] = rgpi;
                }

                return dwError;
            }
            finally
            {
                Marshal.FreeHGlobal(rgpiPtr);
            }
        }
    }

    const int CCH_RM_SESSION_KEY = 32;
    private DWORD sessionId;

    public RestartManager()
    {
        StartSession();
    }

    private void StartSession()
    {
        RmErrors.ThrowIfError(Interop.StartSession(ref sessionId));
    }

    public void RegisterFiles(IEnumerable<string> paths)
    {
        RmErrors.ThrowIfError(Interop.RegisterResources(sessionId, paths));
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

            var postfix = $" (Process ID: {Process.Id})";

            return prettyName + postfix;
        }
    }

    public List<ProcessObject> GetProcessesUsingFiles()
    {
        if (OperatingSystem.Current != OperatingSystem.Windows) return [];
        RmErrors.ThrowIfError(Interop.GetList(sessionId, out var procs));
        return procs.Select(x =>
                new ProcessObject(Process.GetProcessById((int)x.Process.dwProcessId), x.bRestartable != 0))
            .ToList();
    }

    public static List<ProcessObject> GetProcessesUsingFiles(string[] files)
    {
        // Reusing the same restart manager session for multiple requests 
        // sounds like it would be the intended use case, but calling RmGetList
        // for the same session after e.g. a file handle has been free'd does
        // not seem to remove that process from the list.
        // This is not an issue when a new RestartManager is created.
        using var rm = new RestartManager();
        rm.RegisterFiles(files);
        return rm.GetProcessesUsingFiles();
    }

    public void Shutdown(RmWriteStatusCallback callback, bool force)
    {
        RmErrors.ThrowIfError(Interop.Shutdown(sessionId, force, callback));
    }

    public void Restart(RmWriteStatusCallback callback)
    { 
        RmErrors.ThrowIfError(Interop.Restart(sessionId, callback));
    }

    public void Dispose()
    {
        RmErrors.ThrowIfError(Interop.EndSession(sessionId));
    }

    #region Data structs
    
    /// <summary>
    /// These structs are being marshalled as back and forth as raw pointers.
    /// It is very important that their size does not change.
    /// </summary>
    
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

    /// <summary>
    /// See:
    /// https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/ns-restartmanager-rm_process_info
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct RM_PROCESS_INFO
    {
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public WCHAR[,] strAppName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public WCHAR[,] strServiceShortName;

        public RM_APP_TYPE ApplicationType;
        public ULONG AppStatus;
        public DWORD TSessionId;
        public BOOL bRestartable;

        public string StrAppName => Marshal.PtrToStringUni(Marshal.UnsafeAddrOfPinnedArrayElement(strAppName, 0));

        public string StrServiceName =>
            Marshal.PtrToStringUni(Marshal.UnsafeAddrOfPinnedArrayElement(strServiceShortName, 0));
    }
    #endregion
}

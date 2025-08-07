using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTap
{
    
    /// <summary> Implements Visa SCPI IO. </summary>
    class ScpiIO : IScpiIO3
    {
        private bool sendEnd = true;
        private int lockTimeout = 5000;
        private int ioTimeout = 2000;
        private byte termChar = 10;
        private bool useTermChar = false;

        private int rm = Visa.VI_NULL;
        private int instrument = Visa.VI_NULL;

        private bool IsConnected { get { return instrument != Visa.VI_NULL; } }
        public int ID => instrument;
        public event ScpiIOSrqDelegate SRQ
        {
            add
            {
                lock (srqLock)
                {
                    try
                    {
                        srqListeners.Add(value);
                        if ((srqListeners.Count == 1) && IsConnected)
                            EnableSRQ();
                    }
                    catch
                    {
                        srqListeners.Remove(value);
                        if (srqListeners.Count == 0)
                            DisableSRQ();
                        throw;
                    }
                }
            }

            remove
            {
                lock (srqLock)
                {
                    srqListeners.Remove(value);
                    if ((srqListeners.Count == 0) && IsConnected)
                        DisableSRQ();
                }
            }
        }
        private void RaiseError(int error)
        {
            if (error < 0)
                throw new VISAException(ID, error);
        }
        private void invokeSrqListeners()
        {
            var listeners = srqListeners.ToList();
            foreach (var l in listeners)
            {
                try
                {
                    l.Invoke(this);
                }
                catch (Exception ex)
                {
                    log.Error($"Unhandled exception in SRQ listener '{l.GetType().FullName}': {ex.Message}");
                    log.Debug(ex);
                }
            }
        }

        private GCHandle srqDelegateHandle; 
        private static readonly TraceSource log = Log.CreateSource("SCPI");
        void EnableSRQ()
        {
            // We pass this delegate into unmanaged code, so we need to ensure it will not be garbage collected.
            // This use of GCAlloc is in line with Microsoft recommendations when passing references to unmanaged libraries.
            // See the description of `GCHandleType.Normal' here:
            // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandletype?view=net-9.0
            // ... This enumeration member is useful when an unmanaged client holds the only reference,
            // which is undetectable from the garbage collector, to a managed object.

            if (!srqDelegateHandle.IsAllocated)
            {
                srqDelegateHandle = GCHandle.Alloc(new VisaFunctions.ViEventHandler((_, _, _, _) =>
                {
                    try
                    {
                        invokeSrqListeners();
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Unhandled exception during SRQ event: {ex.Message}");
                        log.Debug(ex);
                    }
                    return Visa.VI_SUCCESS;
                }), GCHandleType.Normal);
            }
            RaiseError(Visa.viInstallHandler(instrument, Visa.VI_EVENT_SERVICE_REQ, (VisaFunctions.ViEventHandler)srqDelegateHandle.Target, 0));
            RaiseError(Visa.viEnableEvent(instrument, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_HNDLR, Visa.VI_NULL));
        }

        void DisableSRQ()
        { 
            if (srqDelegateHandle.IsAllocated)
            {
                RaiseError(Visa.viDisableEvent(instrument, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_ALL_MECH));
                RaiseError(Visa.viUninstallHandler(instrument, Visa.VI_EVENT_SERVICE_REQ, null, 0));
                
                srqDelegateHandle.Free();
                srqDelegateHandle = default;
            }
        }

        public void OpenSRQ()
        {
            lock (srqLock)
                if (srqListeners.Count > 0)
                    EnableSRQ();
        }

        public void CloseSRQ()
        {
            lock (srqLock)
                DisableSRQ();
        }
            
        private List<ScpiIOSrqDelegate> srqListeners = new List<ScpiIOSrqDelegate>();
        private object srqLock = new object();

        private ScpiIOResult MakeError(int result)
        {
            switch (result)
            {
                case Visa.VI_SUCCESS: return ScpiIOResult.Success;
                case Visa.VI_SUCCESS_MAX_CNT: return ScpiIOResult.Success_MaxCount;
                case Visa.VI_SUCCESS_TERM_CHAR: return ScpiIOResult.Success_TermChar;

                case Visa.VI_ERROR_TMO: return ScpiIOResult.Error_Timeout;
                case Visa.VI_ERROR_RSRC_LOCKED: return ScpiIOResult.Error_ResourceLocked;
                case Visa.VI_ERROR_CONN_LOST: return ScpiIOResult.Error_ConnectionLost;
                case Visa.VI_ERROR_RSRC_NFOUND: return ScpiIOResult.Error_ResourceNotFound;
            }

            if (result >= 0)
                return ScpiIOResult.Success;
            else
                return ScpiIOResult.Error_General;
        }

        private void SyncSettings()
        {
            Visa.viSetAttribute(instrument, Visa.VI_ATTR_SEND_END_EN, (byte)(sendEnd ? 1 : 0));
            Visa.viSetAttribute(instrument, Visa.VI_ATTR_TMO_VALUE, ioTimeout);
            Visa.viSetAttribute(instrument, Visa.VI_ATTR_TERMCHAR, termChar);
            Visa.viSetAttribute(instrument, Visa.VI_ATTR_TERMCHAR_EN, (byte)(useTermChar ? 1 : 0));
        }

        public ScpiIOResult Open(string visaAddress, bool exclusiveLock)
        {
            if (instrument != Visa.VI_NULL)
                throw new Exception("IO is already open");
            var res2 = Visa.viOpenDefaultRM(out rm);
            if (res2 < 0) return MakeError(res2);

            var res = Visa.viOpen(ScpiInstrument.GetResourceManager(), visaAddress, exclusiveLock ? Visa.VI_EXCLUSIVE_LOCK : Visa.VI_NO_LOCK, IOTimeoutMS, out instrument);

            // Use sensible defaults
            sendEnd = true;
            useTermChar = false;

            if (res >= 0)
                SyncSettings();
            else
                instrument = Visa.VI_NULL;

            return MakeError(res);
        }

        public ScpiIOResult Close()
        {
            try
            {
                return MakeError(Visa.viClose(instrument));
            }
            finally
            {
                Visa.viClose(rm);
                instrument = Visa.VI_NULL;
            }
        }

        public bool SendEnd
        {
            get => sendEnd;
            set
            {
                if (sendEnd != value)
                {
                    sendEnd = value;
                    if (IsConnected) Visa.viSetAttribute(instrument, Visa.VI_ATTR_SEND_END_EN, (byte)(value ? 1 : 0));
                }
            }
        }
        public int IOTimeoutMS
        {
            get => ioTimeout;
            set
            {
                if (ioTimeout != value)
                {
                    ioTimeout = value;
                    if (IsConnected) Visa.viSetAttribute(instrument, Visa.VI_ATTR_TMO_VALUE, ioTimeout);
                }
            }
        }
        public int LockTimeoutMS
        {
            get { return lockTimeout; }
            set { lockTimeout = value; }
        }
        public byte TerminationCharacter
        {
            get => termChar;
            set
            {
                if (termChar != value)
                {
                    termChar = value;
                    if (IsConnected) Visa.viSetAttribute(instrument, Visa.VI_ATTR_TERMCHAR, value);
                }
            }
        }
        public bool UseTerminationCharacter
        {
            get => useTermChar;
            set
            {
                if (useTermChar != value)
                {
                    useTermChar = value;
                    if (IsConnected) Visa.viSetAttribute(instrument, Visa.VI_ATTR_TERMCHAR_EN, (byte)(value ? 1 : 0));
                }
            }
        }

        public string ResourceClass
        {
            get
            {
                var sb = new StringBuilder();
                Visa.viGetAttribute(instrument, Visa.VI_ATTR_RSRC_CLASS, sb);
                return sb.ToString();
            }
        }

        public ScpiIOResult DeviceClear()
        {
            return MakeError(Visa.viClear(instrument));
        }

        public ScpiIOResult Lock(ScpiLockType lockType, string sharedKey = null)
        {
            var sb = new StringBuilder();
            return MakeError(Visa.viLock(instrument, lockType == ScpiLockType.Exclusive ? Visa.VI_EXCLUSIVE_LOCK : Visa.VI_SHARED_LOCK, LockTimeoutMS, sharedKey, sb));
        }

        public ScpiIOResult Read(ArraySegment<byte> buffer, int count, ref bool eoi, ref int read)
        {
            var res = Visa.viRead(instrument, buffer, count, out read);
            eoi = (res == Visa.VI_SUCCESS);
            return MakeError(res);
        }

        public ScpiIOResult ReadSTB(ref byte stb)
        {
            short statusByte = 0;
            var res = Visa.viReadSTB(instrument, ref statusByte);
            stb = (byte)statusByte;
            return MakeError(res);
        }

        public ScpiIOResult Unlock()
        {
            return MakeError(Visa.viUnlock(instrument));
        }

        public ScpiIOResult Write(ArraySegment<byte> buffer, int count, ref int written)
        {
            return MakeError(Visa.viWrite(instrument, buffer, count, out written));
        }
        
        public ScpiIOResult EnableEvent(ScpiEvent eventType, ScpiEventMechanism mechanism)
        {
            return MakeError(Visa.viEnableEvent(instrument, (int)eventType, (short)mechanism, Visa.VI_NULL));
        }

        public ScpiIOResult DisableEvent(ScpiEvent eventType, ScpiEventMechanism mechanism)
        {
            return MakeError(Visa.viDisableEvent(instrument, (int)eventType, (short)mechanism));
        }
        
        public ScpiIOResult WaitOnEvent(ScpiEvent eventType, int timeout, out ScpiEvent outEventType)
        {
            var result = Visa.viWaitOnEvent(instrument, (int)eventType, timeout, out int outEvent, IntPtr.Zero);
            outEventType = (ScpiEvent)outEvent;
            return MakeError(result);
        }
    }
}

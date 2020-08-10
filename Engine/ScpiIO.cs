using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    
    /// <summary> Implements Visa SCPI IO. </summary>
    class ScpiIO : IScpiIO2
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
                        srqListenerCount++;
                        if ((srqListenerCount == 1) && IsConnected)
                            EnableSRQ();
                    }
                    catch
                    {
                        srqListeners.Remove(value);
                        srqListenerCount--;
                        RaiseError(Visa.viUninstallHandler(instrument, Visa.VI_EVENT_SERVICE_REQ, null, 0));
                        throw;
                    }
                }
            }

            remove
            {
                lock (srqLock)
                {
                    srqListeners.Add(value);
                    srqListenerCount--;
                    if ((srqListenerCount == 0) && IsConnected)
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
            var listeners = srqListeners;
            foreach(var l in listeners)
                l.Invoke(this);
        }

        void EnableSRQ()
        {
            RaiseError(Visa.viInstallHandler(instrument, Visa.VI_EVENT_SERVICE_REQ, new Visa.viEventHandler((vi, evt, context, handle) => { invokeSrqListeners(); return Visa.VI_SUCCESS; }), 0));
            RaiseError(Visa.viEnableEvent(instrument, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_HNDLR, Visa.VI_NULL));
        }

        void DisableSRQ()
        {
            RaiseError(Visa.viDisableEvent(instrument, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_ALL_MECH));
            RaiseError(Visa.viUninstallHandler(instrument, Visa.VI_EVENT_SERVICE_REQ, null, 0));
        }

        public void OpenSRQ()
        {
            lock (srqLock)
                if (srqListenerCount > 0)
                    EnableSRQ();
        }

        public void CloseSRQ()
        {
            lock (srqLock)
                if (srqListenerCount > 0)
                    DisableSRQ();
        }
            
        private List<ScpiIOSrqDelegate> srqListeners = new List<ScpiIOSrqDelegate>();
        private int srqListenerCount = 0;
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
    }
}
//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.ComponentModel;
using System.Net;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTap
{
    internal enum IEEEBinaryType
    {
        BinaryType_UI1,
        BinaryType_I2,
        BinaryType_I4,
        BinaryType_R4,
        BinaryType_R8,
    }

    internal class VISAException : Exception
    {
        public int ErrorCode { get; private set; }

        private static string GetMessage(int vi, int error)
        {
            StringBuilder sb = new StringBuilder(1024);
            if (Visa.viStatusDesc(vi, error, sb) == Visa.VI_SUCCESS)
                return sb.ToString();
            else
                return "Unknown error";
        }

        public VISAException(int vi, int error) : base(GetMessage(vi, error))
        {
            ErrorCode = error;
        }

        public VISAException(string message, int error) : base(message)
        {
            ErrorCode = error;
        }
    }

    /// <summary> 
    /// Implements a connection to talk to any SCPI-enabled instrument.
    /// </summary>
    public abstract class ScpiInstrument : Instrument, IScpiInstrument
    {
        private ScpiIO scpiIO;

        /// <summary>
        /// Implements a 
        /// </summary>
        private class ScpiIO : IScpiIO
        {
            private bool sendEnd = true;
            private int lockTimeout = 5000;
            private int ioTimeout = 2000;
            private byte termChar = 10;
            private bool useTermChar = false;

            private int rm = Visa.VI_NULL;
            private int instrument = Visa.VI_NULL;

            private bool IsConnected { get { return instrument != Visa.VI_NULL; } }

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
                }
            }

            public bool SendEnd
            {
                get
                {
                    return sendEnd;
                }
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
                get
                {
                    return ioTimeout;
                }
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
                get
                {
                    return termChar;
                }
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
                get
                {
                    return useTermChar;
                }
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

            public int InstrumentHandle { get { return instrument; } }

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

        IScpiIO IScpiInstrument.IO { get { return scpiIO; } }

        #region Settings
        /// <summary>
        /// The VISA address of the instrument that this class represents a connection to.
        /// </summary>
        [Display("VISA Address", Group: "Common", Order: 1, Description: "The VISA address of the instrument e.g. 'TCPIP::1.2.3.4::INSTR' or 'GPIB::14::INSTR'")]
        [VisaAddress]
        public string VisaAddress { get; set; }

        static TraceSource staticLog = OpenTap.Log.CreateSource("SCPI");

        static int visa_resource = Visa.VI_NULL;
        static bool visa_tried_load = false;
        static bool visa_failed = false;

        private void RaiseError(ScpiIOResult error)
        {
            switch (error)
            {
                case ScpiIOResult.Error_ConnectionLost: throw new VISAException("Connection lost", Visa.VI_ERROR_CONN_LOST);
                case ScpiIOResult.Error_General: throw new VISAException("General error", Visa.VI_ERROR_IO);
                case ScpiIOResult.Error_ResourceLocked: throw new VISAException("Resource locked", Visa.VI_ERROR_RSRC_LOCKED);
                case ScpiIOResult.Error_Timeout: throw new VISAException("IO Timeout", Visa.VI_ERROR_TMO);
                case ScpiIOResult.Error_ResourceNotFound: throw new VISAException("Resource not found", Visa.VI_ERROR_RSRC_NFOUND);
            }
        }

        private void RaiseError(int error)
        {
            if (error < 0)
                throw new VISAException(scpiIO.InstrumentHandle, error);
        }

        private static void RaiseError2(int error)
        {
            if (error < 0)
                throw new VISAException(0, error);
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions] // Required by .NET to catch AccessViolationException.
        internal static int GetResourceManager()
        {
            try
            {
                if (visa_resource == Visa.VI_NULL && !visa_tried_load)
                    RaiseError2(Visa.viOpenDefaultRM(out visa_resource));
            }
            catch (Exception e)
            {
                visa_failed = true;
                visa_tried_load = true;
                staticLog.Warning("Could not create VISA resource manager. This could be because of a missing VISA provider. Please install/reinstall Keysight IO Libraries.");
                staticLog.Debug(e);
            }
            return visa_resource;
        }

        static bool checkVisaAddress(string str)
        {
            if (visa_failed) return true;

            try
            {
                short ifType = 0;
                short partNumber = 0;
                RaiseError2(Visa.viParseRsrc(visa_resource, str, ref ifType, ref partNumber));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        static Memorizer<string, bool> validator = new Memorizer<string, bool>(checkVisaAddress)
        {
            SoftSizeDecayTime = TimeSpan.FromSeconds(10)
        };

        private bool visaAddrValid()
        {
            if (string.IsNullOrWhiteSpace(VisaAddress)) return false;
            
            // Ignore this validation if the visa address is a valid ip address
            if (Regex.IsMatch(VisaAddress, IpPattern)) return true;
            validator.CheckConstraints();
            return validator.Invoke(VisaAddress);
        }

        /// <summary>
        /// The timeout used by the underlying VISA driver when communicating with the instrument [ms].
        /// </summary>
        [Display("VISA I/O Timeout", Group: "Common", Order: 1, Description: "The timeout used in the VISA driver when communicating with the instrument. (Default is 2 s and resolutions is 1 ms)")]
        [Unit("s", PreScaling: 1000, UseEngineeringPrefix: true)]
        public int IoTimeout
        {
            get { return scpiIO.IOTimeoutMS; }
            set { scpiIO.IOTimeoutMS = value; }
        }

        private uint lockRetries = 5;
        private double lockHoldoff = 0.1;

        /// <summary>
        /// If enabled ScpiInstrument acquires an exclusive lock when opening the instrument.
        /// </summary>
        [Display("Lock Queries", Group: "Locking", Order: 2.6, Collapsed: true, Description: "If enabled the instrument will acquire an exclusive lock when performing SCPI queries. This might degrade performance")]
        [EnabledIf("Lock", false)]
        public bool FinegrainedLock { get; set; }

        /// <summary>
        /// If enabled ScpiInstrument acquires an exclusive lock when opening the instrument.
        /// </summary>
        [Display("Lock Instrument", Group: "Locking", Order: 3, Collapsed: true, Description: "If enabled the instrument will be opened with exclusive access. This will disallow other clients from accessing the instrument.")]
        public bool Lock { get; set; }

        /// <summary>
        /// Specifies how many times the SCPI instrument should retry an operation, if it was canceled by another host locking the device.
        /// </summary>
        [Display("Lock Retries", Group: "Locking", Order: 3, Collapsed: true, Description: "Specifies how many times the SCPI instrument should retry an operation, if it was canceled by another host locking the device.")]
        public uint LockRetries
        {
            get { return lockRetries; }
            set { lockRetries = value; }
        }

        /// <summary>
        /// Specifies how long the SCPI instrument should wait before it retries an operation, if it was canceled by another host locking the device.
        /// </summary>
        [Unit("s", true)]
        [Display("Lock Holdoff", Group: "Locking", Order: 2.8, Collapsed: true, Description: "Specifies how long the SCPI instrument should wait before it retries an operation, if it was canceled by another host locking the device.")]
        public double LockHoldoff
        {
            get { return lockHoldoff; }
            set { lockHoldoff = value; }
        }

        /// <summary>
        /// When enabled, causes the instrument driver to ask the instrument SYST:ERR? after every command. Useful when debugging.
        /// </summary>
        [Display("Error Checking", Group: "Debug", Order: 4, Collapsed: true, Description: "When enabled, the instrument driver will ask the instrument SYST:ERR? after every command.")]
        public bool QueryErrorAfterCommand { get; set; }

        /// <summary>
        /// When true, <see cref="Open"/> will send VIClear() right after establishing a connection.
        /// </summary>
        [Display("Send VIClear On Connect", Group: "Debug", Order: 4.1, Collapsed: true, Description: "Send VIClear() when opening the connection to the instrument.")]
        public bool SendClearOnConnect { get; set; }

        /// <summary> Gets or sets whether Verbose SCPI logging is enabled. </summary>
        [Display("Verbose SCPI Logging", Group: "Debug", Order:4.2, Collapsed:true, Description: "Enables verbose logging of SCPI communication.")]
        public bool VerboseLoggingEnabled { get; set; } = true;

        #endregion

        /// <summary>
        /// Overrides ToString() to give more meaningful names.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{0} ({1})", Name, VisaAddress);
        }

        /// <summary>
        /// Gets the instrument identity string. (As returned by the SCPI command *IDN?).
        /// </summary>
        [XmlIgnore]
        [MetaData]
        public string IdnString { get; private set; }


        private string IpPattern =
            "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        /// <summary>
        /// Initializes a new instance of the InstrumentBase class.
        /// </summary>
        public ScpiInstrument()
        {
            scpiIO = new ScpiIO();

            IoTimeout = 2000;

            // Just trigger the use of the resource manager to test if VISA libraries is installed.
            GetResourceManager();

            // default value for settings:
            SendClearOnConnect = true;
            Rules.Add(() => LockHoldoff >= 0, "Lock holdoff must be positive.", "LockHoldoff");
            Rules.Add(() => IoTimeout >= 0, "I/O timeout must be positive.", "IoTimeout");
            Rules.Add(visaAddrValid, "Invalid visa address format.", "VisaAddress");
            Rules.Add(() => !Regex.IsMatch(VisaAddress ?? "", IpPattern), () => "Invalid VISA address, did you mean 'TCPIP::" + VisaAddress + "::INSTR'?", "VisaAddress");
        }

        private byte TerminationCharacter { get { return scpiIO.TerminationCharacter; } set { scpiIO.TerminationCharacter = value; } }

        private bool TerminationCharacterEnabled { get { return scpiIO.UseTerminationCharacter; } set { scpiIO.UseTerminationCharacter = value; } }

        private bool SendEndEnabled { get { return scpiIO.SendEnd; } set { scpiIO.SendEnd = value; } }

        /// <summary>
        /// Called by InstrumentBase.Open() before the newly opened connection is used for anything.
        /// Allows specializations of InstrumentBase to customize connection parameters such as
        /// TerminationCharacter.
        /// </summary>
        protected virtual void SetTerminationCharacter(int inst)
        {
            // inst.ResourceName expands eventual aliases.
            if (scpiIO.ResourceClass.Contains("SOCKET"))
            {
                TerminationCharacterEnabled = true;
            }
        }

        private void LockRetry(Action action)
        {
            int Retry = 0;

            do
            {
                try
                {
                    action();
                    return;
                }
                catch (VISAException ex)
                {
                    if (ex.ErrorCode != Visa.VI_ERROR_RSRC_LOCKED)
                        throw;
                }

                Thread.Sleep(TimeSpan.FromSeconds(lockHoldoff));
            }
            while (Retry++ < lockRetries);

            throw new IOException("Instrument locked");
        }

        private T LockRetry<T>(Func<T> action)
        {
            int Retry = 0;

            do
            {
                try
                {
                    var res = action();
                    return res;
                }
                catch (VISAException ex)
                {
                    if (ex.ErrorCode != Visa.VI_ERROR_RSRC_LOCKED)
                        throw;
                }

                Thread.Sleep(TimeSpan.FromSeconds(lockHoldoff));
            }
            while (Retry++ < lockRetries);

            throw new IOException("Instrument locked");
        }

        /// <summary>
        /// Opens the connection to the Instrument. 
        /// Assumes Visa Address property is specified.
        /// </summary>
        public override void Open()
        {
            if (visa_failed)
            {
                Log.Error("No VISA provider installed. Please install/reinstall Keysight IO Libraries, or similar VISA provider.");
                throw new Exception("Could not create VISA connection.");
            }

            int retry = 0;

            do
            {
                Log.Debug("Connecting to '{0}'", VisaAddress);
                try
                {
                    RaiseError(scpiIO.Open(VisaAddress, Lock));

                    if (SendClearOnConnect)
                    {
                        LockRetry(() => DoClear());
                    }

                    {
                        base.Open();
                        SetTerminationCharacter(scpiIO.InstrumentHandle);

                        IdnString = ScpiQuery("*IDN?");
                        IdnString = IdnString.Trim();
                        Log.Info("Now connected to: " + IdnString);

                        ScpiCommand("*CLS");// Empty error log

                        OpenSRQ();
                    }

                    return; // We are done opening the device
                }
                catch (VISAException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case Visa.VI_ERROR_RSRC_LOCKED:
                            if (retry < lockRetries) Thread.Sleep(TimeSpan.FromSeconds(lockHoldoff));
                            break;
                        default:
                            string errorMsg = String.Format("Cannot connect to instrument on address '{0}'.", VisaAddress);
                            throw new IOException(errorMsg, ex);
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = String.Format("Cannot connect to instrument on address '{0}'.", VisaAddress);
                    throw new IOException(errorMsg, ex);
                }
            }
            while (retry++ < lockRetries);

            string errorMsg2 = String.Format("Cannot connect to instrument on address '{0}'. Device already locked by another connection.", VisaAddress);
            throw new IOException(errorMsg2);
        }

        /// <summary>
        /// Closes the connection to the instrument. Assumes connection is open.
        /// </summary>
        public override void Close()
        {
            try
            {
                try
                {
                    CloseSRQ();
                }
                catch (Exception ex)
                {
                    Log.Error("Caught error while closing SRQ.");
                    Log.Debug(ex);
                }

                try
                {
                    RaiseError(scpiIO.Close());
                }
                catch (Exception ex)
                {
                    Log.Error("Caught error while closing instrument.");
                    Log.Debug(ex);
                }
            }
            finally
            {
                base.Close();
            }

        }

        /// <summary>
        /// Clears the device SCPI buffers.
        /// </summary>
        protected virtual void DoClear()
        {
            RaiseError(scpiIO.DeviceClear());
        }

        /// <summary>
        /// Reads the status byte.
        /// </summary>
        /// <returns></returns>
        protected virtual short DoReadSTB()
        {
            byte status = 0;
            RaiseError(scpiIO.ReadSTB(ref status));
            return status;
        }

        private ScpiIOResult Read(ArraySegment<byte> data, int count, out int read)
        {
            bool eoi = false;
            read = 0;
            var res = scpiIO.Read(data, count, ref eoi, ref read);
            RaiseError(res);
            return res;
        }

        private byte[] Read(int count)
        {
            byte[] buffer = new byte[count];
            int read;

            Read(new ArraySegment<byte>(buffer), count, out read);

            if (read != count)
                Array.Resize(ref buffer, read);

            return buffer;
        }

        private string ReadString(int count)
        {
            return System.Text.Encoding.ASCII.GetString(Read(count));
        }

        const int BufferSize = 16 * 1024;

        [ThreadStatic] static byte[] readBuffer; //[ThreadStatic] field cannot have an initializer.

        private string ReadString()
        {
            // copy readBuffer to avoid reading/writing the thread static variable more than necessesary.
            // this is known to be a relatively slow operation.
            byte[] buffer = readBuffer ?? new byte[BufferSize];

            int total = 0;

            var res = Read(new ArraySegment<byte>(buffer), buffer.Length, out total);

            while (res == ScpiIOResult.Success_MaxCount)
            {
                Array.Resize(ref buffer, (buffer.Length * 4) / 3); // Geometric resizing

                int subRead;
                res = Read(new ArraySegment<byte>(buffer, total, buffer.Length - total), buffer.Length - total, out subRead);

                if (subRead > 0)
                    total += subRead;
            }

            readBuffer = buffer;

            return Encoding.ASCII.GetString(readBuffer, 0, total);
        }

        private int Write(byte[] data, int count)
        {
            int sent = 0;

            var res = scpiIO.Write(new ArraySegment<byte>(data), count, ref sent);
            RaiseError(res);

            return sent;
        }

        private void WriteString(string data)
        {
            if (SendEndEnabled && !data.EndsWith("\n"))
                data = data + "\n";

            byte[] buf = System.Text.Encoding.ASCII.GetBytes(data);

            Write(buf, buf.Length);
        }

        private void WriteIEEEBlock(string command, byte[] data)
        {
            var origEndEnabled = SendEndEnabled;
            try
            {
                SendEndEnabled = false;

                string header = data.Length.ToString();
                header = '#' + header.Length.ToString() + header;

                WriteString(command + header);

                Write(data, data.Length);

                SendEndEnabled = origEndEnabled;
                WriteString("\n");
            }
            finally
            {
                SendEndEnabled = origEndEnabled;
            }
        }

        private string ReadStringOrBlock()
        {
            if (!TerminationCharacterEnabled)
            {
                return LockRetry(() => ReadString());
            }
            else
            {
                // To make sure we get the same behavior when TerminationCharacter is enabled.
                // we need to manually check and handle IEEE488 formatted blocks.
                char firstChar = LockRetry(() => (char)Read(1).First());
                if (firstChar == '#') // is this a IEEE488 formatted block.
                {
                    int lengthFieldLength = int.Parse((LockRetry(() => (char)Read(1).First())).ToString());
                    string lengthFieldText = LockRetry(() => ReadString(lengthFieldLength));
                    int length = int.Parse(lengthFieldText);
                    TerminationCharacterEnabled = false;
                    string text = LockRetry(() => ReadString(length)) + TerminationCharacter;
                    TerminationCharacterEnabled = true;

                    // Read the terminating character to get it of the output buffer
                    LockRetry(() => ReadString());

                    return text;
                }
                else
                {
                    string theRest = LockRetry(() => ReadString());
                    return firstChar + theRest;
                }
            }
        }

        private void DoLock()
        {
            RaiseError(scpiIO.Lock(ScpiLockType.Exclusive));
        }

        private void DoUnlock()
        {
            RaiseError(scpiIO.Unlock());
        }

        /// <summary>
        /// Sends a SCPI query to the instrument and waits for a response.
        /// </summary>
        /// <param name="query">The SCPI query to send.</param>
        /// <param name="isSilent">True to suppress log messages.</param>
        /// <returns>The response from the instrument.</returns>
        public virtual string ScpiQuery(string query, bool isSilent = false)
        {
            if (query == null)
                throw new ArgumentNullException("query");
            if (!IsConnected)
                throw new IOException("Not connected.");
            OnActivity();
            string result = String.Empty;

            try
            {
                Stopwatch timer = Stopwatch.StartNew();

                if (FinegrainedLock && !Lock)
                {
                    LockRetry(() => DoLock());
                    try
                    {
                        LockRetry(() => WriteString(query));
                        LockRetry(() => result = ReadStringOrBlock());
                    }
                    finally
                    {
                        DoUnlock();
                    }
                }
                else
                {
                    LockRetry(() => WriteString(query));
                    LockRetry(() => result = ReadStringOrBlock());
                }

                if (!isSilent && VerboseLoggingEnabled)
                    Log.Debug(timer, "SCPI >> {0}", query);

            }
            catch (VISAException ex)
            {
                if (ex.ErrorCode == Visa.VI_ERROR_CONN_LOST)
                {
                    Log.Error("Connection lost");
                    IsConnected = false;
                    throw new IOException("Connection lost");
                }
                if (ex.ErrorCode == Visa.VI_ERROR_TMO)
                {
                    Log.Error("Not responding (query '{0}' timed out)", query);
                    throw new TimeoutException("Not responding");
                }
                var status = DoReadSTB();
                Log.Error("SCPI query failed ({0})", query);
                Log.Error(ex);
                return null;
            }

            IsConnected = true;
            if (!isSilent && VerboseLoggingEnabled)
                Log.Debug("SCPI << {0}", result);
            return result;
        }

        /// <summary>
        /// As ScpiQuery except it will try to parse the returned string to T. See <see cref="Scpi.Parse"/> for details on parsing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="isSilent">True to suppress log messages.</param>
        /// <returns></returns>
        public T ScpiQuery<T>(string query, bool isSilent = false)
        {
            string response = ScpiQuery(query, isSilent);
            return Scpi.Parse<T>(response);
        }

        /// <summary>
        /// Sends a IEEE Block SCPI query to the instrument and waits for a response. The response is assumed to be IEEE block data.
        /// </summary>
        /// <param name="query">The SCPI query to send.</param>
        /// <returns>The response from the instrument.</returns>
        public byte[] ScpiQueryBlock(string query)
        {
            return ScpiQueryBlock<byte>(query);
        }

        private Array ReadIEEEBlock(IEEEBinaryType type)
        {
            return LockRetry<Array>(() => DoReadIEEEBlock(type));
        }

        //Work around for bug found IVI Shared Components. #2487
        private Array DoReadIEEEBlock(IEEEBinaryType type, bool seekToBlock = true, bool flushToEND = true)
        {
            //Variable Declarations
            int dataSize;
            int dataSizeLen;
            byte[] dataBytes;

            //Variables to proces bytes coming back from the instrument
            int dataTypeLen;
            Array data;

            //save the termchar for later use
            byte termChar = TerminationCharacter;
            bool termCharEn = TerminationCharacterEnabled;

            //
            //Part 1) Parse the header and grab the data
            //

            //If seekToBlock = True, scan for "#". Will timeout and throw an Exception if "#" is not found.
            if (seekToBlock)
            {
                // Do an early test by just reading one character. This will most likely be the common case
                var buff = Read(1);

                if ((buff.Length != 1) || (buff[0] != (byte)'#'))
                {
                    TerminationCharacter = (byte)'#';
                    TerminationCharacterEnabled = true;

                    ReadString(); // if it can't find a # it will timeout

                    //If we get to this point, we know that the # was found. Disable the Termination Character.
                    TerminationCharacter = termChar;
                    TerminationCharacterEnabled = false;
                }
            }
            else
            {
                //If the first char in the buffer is not a "#", throw an Exception.
                var buff = Read(1);

                if (buff[0] != (byte)'#')
                {
                    throw new VISAException("The Binary Block could not be parsed.", -2147221439);
                }
            }

            //Get the size of the "binary block data length field"
            var digits = ReadString(1);
            dataSizeLen = int.Parse(digits);

            if (dataSizeLen == 0)
            {
                // Indefinite length arbitrary block. Terminated by newline
                TerminationCharacterEnabled = true;
                TerminationCharacter = (byte)'\n';

                dataBytes = System.Text.Encoding.ASCII.GetBytes(ReadString());
                dataSize = dataBytes.Length;
            }
            else
            {
                //Get the size of the "binary block data"
                var length = ReadString(dataSizeLen);
                dataSize = int.Parse(length);

                TerminationCharacterEnabled = false;

                //Grab the data
                dataBytes = Read(dataSize);
            }

            if (flushToEND)
            {
                int originalTimeout = IoTimeout;
                IoTimeout = 100;

                TerminationCharacter = termChar;
                TerminationCharacterEnabled = true;

                try
                {
                    ReadString(); //throw everything away until we get a trailing \n, but fail in 100 ms if its not there
                }
                finally
                {
                    IoTimeout = originalTimeout;
                    TerminationCharacterEnabled = termCharEn;
                }
            }
            else
            {
                //Reset I/O to state before call
                TerminationCharacter = termChar;
                TerminationCharacterEnabled = termCharEn;
            }

            //
            //Part 2) Format the bytes that come back from the read.
            //Note, if the instrument returns big endian data, we have to correct for it.
            //
            switch (type)
            {
                case IEEEBinaryType.BinaryType_UI1:
                    return dataBytes; // Return early
                case IEEEBinaryType.BinaryType_I2:
                    dataTypeLen = 2;
                    data = new Int16[dataSize / dataTypeLen];
                    break;
                case IEEEBinaryType.BinaryType_I4:
                    dataTypeLen = 4;
                    data = new Int32[dataSize / dataTypeLen];
                    break;
                case IEEEBinaryType.BinaryType_R4:
                    dataTypeLen = 4;
                    data = new Single[dataSize / dataTypeLen];
                    break;
                case IEEEBinaryType.BinaryType_R8:
                    dataTypeLen = 8;
                    data = new double[dataSize / dataTypeLen];
                    break;
                default:
                    return null;
            }

            // Emulate VISA-COM behavior.
            if (dataSize != data.Length * dataTypeLen)
                throw new VISAException("The Safearray cannot be converted to the desired type.", -2147221439);

            Buffer.BlockCopy(dataBytes, 0, data, 0, dataBytes.Length);

            return data;
        }

        /// <summary>
        /// Sends a IEEE Block SCPI query to the instrument and waits for a response. The response is assumed to be IEEE block data.
        /// </summary>
        /// <param name="query">The SCPI query to send.</param>
        /// <returns>The response from the instrument.</returns>
        /// <example>
        /// The format for data returned by the query must be configured to a type matching the type of <typeparamref name="T"/>.
        /// Example 1:
        /// <code>
        /// ScpiCommand("FORMat:TRACe:DATA REAL,32", true);
        /// var data = ScpiQueryBlock&lt;float&gt;(":TRACe:DATA? TRACE1");
        /// </code>
        /// Example 2:
        /// <code>
        /// ScpiCommand("FORMat:TRACe:DATA REAL,64", true);
        /// var data = ScpiQueryBlock&lt;double&gt;(":TRACe:DATA? TRACE1");
        /// </code>
        /// </example>
        public virtual T[] ScpiQueryBlock<T>(string query) where T : struct
        {
            if (query == null)
                throw new ArgumentNullException("query");

            if (FinegrainedLock && !Lock)
                LockRetry(() => DoLock());

            try
            {
                scpiCommand(query, false);
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.Byte:
                        return (T[])ReadIEEEBlock(IEEEBinaryType.BinaryType_UI1);
                    case TypeCode.SByte:
                        return (T[])(Array)ReadIEEEBlock(IEEEBinaryType.BinaryType_UI1);

                    case TypeCode.Int16:
                        return (T[])ReadIEEEBlock(IEEEBinaryType.BinaryType_I2);
                    case TypeCode.UInt16:
                        return (T[])(Array)ReadIEEEBlock(IEEEBinaryType.BinaryType_I2);

                    case TypeCode.Int32:
                        return (T[])ReadIEEEBlock(IEEEBinaryType.BinaryType_I4);
                    case TypeCode.UInt32:
                        return (T[])(Array)ReadIEEEBlock(IEEEBinaryType.BinaryType_I4);

                    // Do binary conversion for 64 bit integers
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        {
                            byte[] result = (byte[])ReadIEEEBlock(IEEEBinaryType.BinaryType_UI1);

                            if (result.Length < 8)
                                return new T[0];

                            T[] arr = new T[result.Length / 8];
                            Buffer.BlockCopy(result, 0, arr, 0, arr.Length * 8);
                            return arr;
                        }

                    case TypeCode.Single:
                        return (T[])ReadIEEEBlock(IEEEBinaryType.BinaryType_R4);
                    case TypeCode.Double:
                        return (T[])ReadIEEEBlock(IEEEBinaryType.BinaryType_R8);
                    default:
                        throw new Exception("Unsupported IEEE488.2 block type: " + typeof(T).Name);
                }
            }
            finally
            {
                if (FinegrainedLock && !Lock)
                    DoUnlock();
            }
        }

        /// <summary>
        /// Sends a SCPI command to the instrument, but with special handling for parameters. See <see cref="Scpi.Format"/> for more information.
        /// </summary>
        /// <param name="command">The command to send, including <see cref="Scpi.Format"/> placement arguments.</param>
        /// <param name="parameters"><see cref="Scpi.Format"/> arguments.</param>
        /// <remarks>Non-blocking.</remarks>
        public void ScpiCommand(string command, params object[] parameters)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            ScpiCommand(Scpi.Format(command, parameters));
        }

        /// <summary>
        /// Sends a SCPI command to the instrument.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <remarks>Non-blocking.</remarks>
        public virtual void ScpiCommand(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            scpiCommand(command, QueryErrorAfterCommand);
        }

        void scpiCommand(string command, bool checkErrors)
        {

            if (!IsConnected)
                throw new IOException("Not connected.");

            OnActivity();
            //TestPlan.Sleep(); // Just giving the TestPlan a chance to abort if it has been requested to do so
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                LockRetry(() => WriteString(command));
                timer.Stop();
                if(VerboseLoggingEnabled)
                    Log.Debug(timer, "SCPI >> {0}", command);

                if (checkErrors)
                {
                    WaitForOperationComplete();
                    QueryErrors();
                }
            }
            catch (VISAException ex)
            {
                if (ex.ErrorCode == Visa.VI_ERROR_CONN_LOST)
                {
                    Log.Error("Connection lost");
                    IsConnected = false;
                    throw new IOException("Connection lost");
                }
                if (ex.ErrorCode == Visa.VI_ERROR_TMO)
                {
                    Log.Error("Not responding (command '{0}' timed out)", command);
                    throw new IOException("Not responding");
                }
                Log.Error(ex);
            }
        }

        /// <summary>
        /// Sends a IEEE Block SCPI command to the instrument.
        /// </summary>
        public virtual void ScpiIEEEBlockCommand(string command, byte[] data)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            if (command.Contains("?"))
            {
                throw new ArgumentException("command is a query: " + command);
            }
            if (!IsConnected)
                throw new IOException("Not connected.");
            OnActivity();
            //TestPlan.Sleep(); // Just giving the TestPlan a chance to abort if it has been requested to do so
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                LockRetry(() => WriteIEEEBlock(command, data));
                timer.Stop();
                if (VerboseLoggingEnabled)
                    Log.Debug(timer, "SCPI >> {0}", command);
                if (QueryErrorAfterCommand)
                {
                    WaitForOperationComplete();
                    QueryErrors();
                }
            }
            catch (VISAException ex)
            {
                if (ex.ErrorCode == Visa.VI_ERROR_CONN_LOST)
                {
                    Log.Error("Connection lost");
                    IsConnected = false;
                    throw new IOException("Connection lost");
                }
                if (ex.ErrorCode == Visa.VI_ERROR_TMO)
                {
                    Log.Error("Not responding (command '{0}' timed out)", command);
                    throw new IOException("Not responding");
                }
                Log.Error(ex);
            }
        }

        /// <summary>
        /// Sends a IEEE Block SCPI command to the instrument with a Streaming interface for large data size.  Uses DirectIO.
        /// </summary>
        public virtual void ScpiIEEEBlockCommand(string command, Stream data, long maxSize = 0)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            if (data == null)
                throw new ArgumentNullException("data");
            if (command.Contains("?"))
            {
                throw new ArgumentException("command is a query: " + command);
            }
            if (!IsConnected)
                throw new IOException("Not connected.");
            OnActivity();
            //TestPlan.Sleep(); // Just giving the TestPlan a chance to abort if it has been requested to do so
            try
            {

                //send SCPI ASCII header
                long sendLength = data.Length - data.Position; //available data to send
                if (!((sendLength > 0) && (maxSize > 0)))
                {
                    Log.Debug("SCPI Write Command Not Sent:  available length={0} max={1}", sendLength, maxSize);
                }
                else
                {

                    Stopwatch timer = Stopwatch.StartNew();

                    if (TerminationCharacterEnabled == false)
                    {
                        SendEndEnabled = false;
                    }

                    if (sendLength > maxSize)
                    {
                        Log.Debug("SCPI Write Command truncated to maximum:  available length={0} max={1}",
                            sendLength, maxSize);
                        sendLength = maxSize; //limit data to send to max
                    }
                    var sendLengthStr = string.Format("{0}", sendLength);
                    var commandBlk = string.Format("{0}#{1}{2}", command, sendLengthStr.Length, sendLengthStr);
                    byte[] commandB = Encoding.ASCII.GetBytes(commandBlk);
                    int retSentCnt = LockRetry(() => Write(commandB, commandB.Length));
                    if (retSentCnt != commandB.Length)
                    {
                        Log.Error("SCPI transmission incomplete, IO.Write return={0}, expect={1}", retSentCnt,
                            commandB.Length);
                        throw new IOException("SCPI block transmission incomplete");
                    }


                    //send binary data
                    int bufferSize = 0x10000;
                    byte[] buffer = new byte[bufferSize];
                    int readSize = 0;
                    int sendingSize = 0;
                    while (0 != (readSize = data.Read(buffer, 0, bufferSize)))
                    {
                        sendingSize = sendingSize + readSize;
                        if (sendingSize >= sendLength)
                        {
                            //send last segment before exceeding limit
                            readSize = readSize - (int)(sendingSize - sendLength); //remove overshoot
                            retSentCnt = LockRetry(() => Write(buffer, readSize));
                            if (retSentCnt != readSize)
                            {
                                Log.Error("SCPI transmission incomplete, IO.Write return={0}, expect={1}", retSentCnt,
                                    readSize);
                                throw new IOException("SCPI block transmission incomplete");
                            }
                            break;
                        }
                        retSentCnt = LockRetry(() => Write(buffer, readSize));
                        if (retSentCnt != readSize)
                        {
                            Log.Error("SCPI transmission incomplete, IO.Write return={0}, expect={1}", retSentCnt,
                                readSize);
                            throw new IOException("SCPI block transmission incomplete");
                        }
                    }
                    if (TerminationCharacterEnabled == false)
                    {
                        SendEndEnabled = true;
                    }
                    LockRetry(() => Write(Encoding.ASCII.GetBytes("\n"), 1));

                    timer.Stop();
                    if (VerboseLoggingEnabled)
                        Log.Debug(timer, String.Format("SCPI >> {0}", command));
                    if (QueryErrorAfterCommand)
                    {
                        WaitForOperationComplete();
                        QueryErrors();
                    }
                }
            }
            catch (VISAException ex)
            {
                if (ex.ErrorCode == Visa.VI_ERROR_CONN_LOST)
                {
                    Log.Error("Connection lost");
                    IsConnected = false;
                    throw new IOException("Connection lost");
                }
                if (ex.ErrorCode == Visa.VI_ERROR_TMO)
                {
                    Log.Error("Not responding (command '{0}' timed out)", command);
                    throw new IOException("Not responding");
                }
                if (ex.Message.StartsWith("SCPI block transmission incomplete"))
                {
                    throw;
                }
                Log.Error(ex);
            }
            finally
            {
                if (TerminationCharacterEnabled == false)
                {
                    SendEndEnabled = true;
                }
            }
        }

        /// <summary>
        /// Polls the instrument for an event.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Usually used for error handling.
        /// </remarks>
        public bool PollStatusEvent()
        {
            return (LockRetry(() => DoReadSTB()) & 0x20) == 0x20;
        }

        /// <summary>
        /// Specifies a dictionary that will map specific error codes to log levels other than Error (the level to which <see cref="QueryErrors(bool, int)"/> typically logs error messages).
        /// </summary>
        protected readonly Dictionary<int, LogEventType> ScpiErrorsLogLevelOverrides = new Dictionary<int, LogEventType>();

        /// <summary>
        /// Returns all the errors on the instrument error stack. Clears the list in the same call.
        /// </summary>
        /// <param name="suppressLogMessages">if true the errors will not be logged.</param>
        /// <param name="maxErrors">The max number of errors to retrieve. Useful if instrument generates errors faster than they can be read.</param>
        /// <returns></returns>
        public List<ScpiError> QueryErrors(bool suppressLogMessages = false, int maxErrors = 1000)
        {
            List<ScpiError> errors = new List<ScpiError>();
            while ((LockRetry(() => DoReadSTB()) & 0x4) != 0x00 && errors.Count < maxErrors)
            {
                ScpiError error = queryErrorParse();
                LogEventType logLevel = LogEventType.Error;
                if (ScpiErrorsLogLevelOverrides.ContainsKey(error.Code))
                {
                    logLevel = ScpiErrorsLogLevelOverrides[error.Code];
                }
                if (!suppressLogMessages)
                {
                    Log.Debug("SCPI >> SYSTem:ERRor?");
                    Log.TraceEvent(logLevel, 0, String.Format("SCPI << {0}", error));
                }

                errors.Add(error);
            }
            return errors;
        }

        /// <summary>
        /// A SCPI error.
        /// </summary>
        public struct ScpiError
        {
            /// <summary>
            /// Error code.
            /// </summary>
            public int Code;

            /// <summary>
            /// Error message.
            /// </summary>
            public string Message;

            /// <summary>
            /// Returns a string formatted the same way as the output of SYST:ERR?
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("{0},\"{1}\"", Code, Message);
            }
        }

        private ScpiError queryErrorParse()
        {
            int errorCode = 0;
            string error = String.Empty;
            string errorStr = ScpiQuery("SYST:ERR?", true).Trim();
            Match regexMatch = Regex.Match(errorStr, "(?<code>[\\-\\+0-9]+),\"(?<msg>.+)\"");
            if (regexMatch.Success)
            {
                error = regexMatch.Groups["msg"].Value;
                errorCode = int.Parse(regexMatch.Groups["code"].Value);
            }
            else
            {
                error = errorStr;
                errorCode = 0;
            }
            return new ScpiError { Code = errorCode, Message = error };
        }

        /// <summary>
        /// Waits for a all previously executed SCPI commands to complete. 
        /// </summary>
        /// <param name="timeoutMs">Maximum time to wait, default is 2 seconds. If value is less than IoTimeout, IoTimeout will be used. </param>
        public void WaitForOperationComplete(int timeoutMs = 2000)
        {
            int orgTimeout = IoTimeout;
            if (orgTimeout < timeoutMs) // Don't decrease timeout
            {
                IoTimeout = timeoutMs;
            }
            try
            {
                ScpiQuery("*OPC?");
            }
            finally
            {
                if (orgTimeout < timeoutMs) // Don't decrease timeout
                {
                    IoTimeout = orgTimeout;
                }
            }
        }

        /// <summary>
        ///  Aborts the currently running measurement and makes the default measurement active. 
        ///  This gets the mode to a consistent state with all of the default couplings set.  
        /// </summary>
        public void Reset()
        {
            ScpiCommand("*RST");
        }

        #region Service Request routines
        /// <summary>
        /// A delegate that is used by the <see cref="SRQ"/> event.
        /// </summary>
        /// <param name="sender">A reference to the <see cref="ScpiInstrument"/> that the SRQ originated from.</param>
        public delegate void ScpiSRQDelegate(ScpiInstrument sender);

        private ScpiSRQDelegate srqListeners = null;
        private int srqListenerCount = 0;
        private object srqLock = new object();

        /// <summary>
        /// This event is called whenever a SRQ is generated by the instrument.
        /// Adding a handler to this event will automatically enable SRQ transactions from the instrument when the instrument is opened/closed, or while the instrument is open.
        /// 
        /// To disable SRQ transactions all handlers added must be removed.
        /// </summary>
        public event ScpiSRQDelegate SRQ
        {
            add
            {
                lock (srqLock)
                {
                    try
                    {
                        srqListeners += value;
                        srqListenerCount++;
                        if ((srqListenerCount == 1) && IsConnected)
                            EnableSRQ();
                    }
                    catch
                    {
                        srqListeners -= value;
                        srqListenerCount--;
                        RaiseError(Visa.viUninstallHandler(scpiIO.InstrumentHandle, Visa.VI_EVENT_SERVICE_REQ, null, 0));
                        throw;
                    }
                }
            }
            remove
            {
                lock (srqLock)
                {
                    srqListeners -= value;
                    srqListenerCount--;
                    if ((srqListenerCount == 0) && IsConnected)
                        DisableSRQ();
                }
            }
        }

        private void invokeSrqListeners()
        {
            var listeners = srqListeners;
            if (listeners != null)
                listeners(this);
        }

        private void EnableSRQ()
        {
            RaiseError(Visa.viInstallHandler(scpiIO.InstrumentHandle, Visa.VI_EVENT_SERVICE_REQ, new Visa.viEventHandler((vi, evt, context, handle) => { invokeSrqListeners(); return Visa.VI_SUCCESS; }), 0));
            RaiseError(Visa.viEnableEvent(scpiIO.InstrumentHandle, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_HNDLR, Visa.VI_NULL));
        }

        private void DisableSRQ()
        {
            RaiseError(Visa.viDisableEvent(scpiIO.InstrumentHandle, Visa.VI_EVENT_SERVICE_REQ, Visa.VI_ALL_MECH));
            RaiseError(Visa.viUninstallHandler(scpiIO.InstrumentHandle, Visa.VI_EVENT_SERVICE_REQ, null, 0));
        }

        private void OpenSRQ()
        {
            lock (srqLock)
                if (srqListenerCount > 0)
                    EnableSRQ();
        }

        private void CloseSRQ()
        {
            lock (srqLock)
                if (srqListenerCount > 0)
                    DisableSRQ();
        }
        #endregion

    }
}

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
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.Text;

namespace OpenTap
{
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
    [Display("Generic SCPI Instrument", Description: "Allows you to configure a VISA based connection to a SCPI instrument.")]
    public class ScpiInstrument : Instrument, IScpiInstrument
    {
        private readonly IScpiIO2 scpiIO;
        
        /// <summary> Some instruments does not support reading the status byte. This is detected during Open. </summary>
        bool readStbSupported = true;
        
        /// <summary> since sending scpi commands/queries (and reading) is not thread safe, but steps using SCPI can run in multiple threads
        /// We lock IO with this commandLock.</summary>
        readonly object commandLock = new object();

        IScpiIO IScpiInstrument.IO { get { return scpiIO; } }

        static readonly TraceSource staticLog = OpenTap.Log.CreateSource("SCPI");

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


        private static void RaiseError2(int error)
        {
            if (error < 0)
                throw new VISAException(0, error);
        }

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

        static bool checkVisaAddressAsync(string str, int millisecondTimeout)
        {
            if (visa_failed) return true;
            bool ok = true;
            using (var semaphore = new Semaphore(0, 1))
            {
                TapThread.Start(() =>
                {
                    try
                    {
                        ok = checkVisaAddress(str);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                if (!semaphore.WaitOne(millisecondTimeout))
                    visa_failed = true;
            }
            return ok;
        }
        
        static bool checkVisaAddress(string str)
        {
            if (visa_failed) return true;

            try
            {
                short ifType = 0;
                short partNumber = 0;
                var error = Visa.viParseRsrc(visa_resource, str, ref ifType, ref partNumber);
                if (error < 0) return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        static Memorizer<string, bool> validator = new Memorizer<string, bool>(s => checkVisaAddressAsync(s, 5000))
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

        
        #region Settings
        /// <summary>
        /// The VISA address of the instrument that this class represents a connection to.
        /// </summary>
        [Display("Address", Groups: new []{"VISA"}, Order: 1, Description: "The VISA address of the instrument e.g. 'TCPIP::1.2.3.4::INSTR' or 'GPIB::14::INSTR'")]
        [VisaAddress]
        public string VisaAddress { get; set; }
        
        /// <summary>
        /// The timeout used by the underlying VISA driver when communicating with the instrument [ms].
        /// </summary>
        [Display("I/O Timeout", Groups: new []{"VISA"}, Order: 1, Description: "The timeout used in the VISA driver when communicating with the instrument. (Default is 2 s and resolutions is 1 ms)")]
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
        [Display("Lock Queries", Groups: new []{"VISA", "Locking"}, Order: 2.6, Collapsed: true, Description: "If enabled the instrument will acquire an exclusive lock when performing SCPI queries. This might degrade performance")]
        [EnabledIf("Lock", false)]
        public bool FinegrainedLock { get; set; }

        /// <summary>
        /// If enabled ScpiInstrument acquires an exclusive lock when opening the instrument.
        /// </summary>
        [Display("Lock Instrument", Groups: new []{"VISA", "Locking"}, Order: 3, Collapsed: true, Description: "If enabled the instrument will be opened with exclusive access. This will disallow other clients from accessing the instrument.")]
        public bool Lock { get; set; }

        
        /// <summary>
        /// Specifies how many times the SCPI instrument should retry an operation, if it was canceled by another host locking the device.
        /// </summary>
        [Display("Lock Retries", Groups: new []{"VISA", "Locking"}, Order: 3, Collapsed: true, Description: "Specifies how many times the SCPI instrument should retry an operation, if it was canceled by another host locking the device.")]
        public uint LockRetries
        {
            get { return lockRetries; }
            set { lockRetries = value; }
        }

        /// <summary>
        /// Specifies how long the SCPI instrument should wait before it retries an operation, if it was canceled by another host locking the device.
        /// </summary>
        [Unit("s", true)]
        [Display("Lock Hold Off", Groups: new []{"VISA", "Locking"}, Order: 2.8, Collapsed: true, Description: "Specifies how long the SCPI instrument should wait before it retries an operation, if it was canceled by another host locking the device.")]
        public double LockHoldoff
        {
            get { return lockHoldoff; }
            set { lockHoldoff = value; }
        }

        /// <summary>
        /// When enabled, causes the instrument driver to ask the instrument SYST:ERR? after every command. Useful when debugging.
        /// </summary>
        [Display("Error Checking", Groups: new []{"VISA", "Debug"}, Order: 4, Collapsed: true, Description: "When enabled, the instrument driver will ask the instrument SYST:ERR? after every command.")]
        public bool QueryErrorAfterCommand { get; set; }

        /// <summary>
        /// When true, <see cref="Open"/> will send VIClear() right after establishing a connection.
        /// </summary>
        [Display("Send VIClear On Connect", Groups: new []{"VISA", "Debug"}, Order: 4.1, Collapsed: true, Description: "Send VIClear() when opening the connection to the instrument.")]
        public bool SendClearOnConnect { get; set; }

        /// <summary> Gets or sets whether Verbose SCPI logging is enabled. </summary>
        [Display("Verbose SCPI Logging", Groups: new []{"VISA", "Debug"}, Order:4.2, Collapsed:true, Description: "Enables verbose logging of SCPI communication.")]
        public bool VerboseLoggingEnabled { get; set; } = true;

        /// <summary>
        /// When true, will send *IDN? right after establishing a connection.
        /// </summary>
        [Display("Send *IDN? On Connect", Groups: new[] { "VISA", "Debug" }, Order: 4.3, Collapsed: true, Description: "Send *IDN? when opening the connection to the instrument.")]
        public bool SendIDNOnConnect { get; set; }

        /// <summary>
        /// When true, will send *CLS right after establishing a connection.
        /// </summary>
        [Display("Send *CLS On Connect", Groups: new[] { "VISA", "Debug" }, Order: 4.4, Collapsed: true, Description: "Send *CLS when opening the connection to the instrument.")]
        public bool SendCLSOnConnect { get; set; }

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
        /// <summary>  Initializes a new instance of the ScpiInstrument class. </summary>
        public ScpiInstrument() : this(new ScpiIO())
        {
        }

        /// <summary> Initialize a new instance of ScpiInstrument, specifying a IScpiIO interface to use. </summary>
        /// <param name="io"> An IO Implementation for doing communication. </param>
        public ScpiInstrument(IScpiIO2 io)
        {
            Name = "SCPI";
            this.scpiIO = io;
            IoTimeout = 2000;

            // Just trigger the use of the resource manager to test if VISA libraries is installed.
            GetResourceManager();

            // default value for settings:
            SendClearOnConnect = true;
            SendIDNOnConnect = true;
            SendCLSOnConnect = true;
            Rules.Add(() => LockHoldoff >= 0, "Lock holdoff must be positive.", nameof(LockHoldoff));
            Rules.Add(() => IoTimeout >= 0, "I/O timeout must be positive.", nameof(IoTimeout));
            Rules.Add(visaAddrValid, "Invalid VISA address format.", nameof(VisaAddress));
            Rules.Add(() => !Regex.IsMatch(VisaAddress ?? "", IpPattern), () => "Invalid VISA address, did you mean 'TCPIP::" + VisaAddress + "::INSTR'?", nameof(VisaAddress));
            
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
            if (scpiIO is ScpiIO && visa_failed)
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
                        SetTerminationCharacter(scpiIO.ID);

                        if (SendIDNOnConnect)
                        {
                            IdnString = QueryIdn();
                            IdnString = IdnString.Trim();
                            Log.Info("Now connected to: " + IdnString);
                        }
                        if (SendCLSOnConnect)
                        {
                            CommandCls(); // Empty error log
                        }

                        try
                        {
                            scpiIO.OpenSRQ();
                        }
                        catch
                        {
                            Log.Error("Unable to attach SRQ handler.");
                            throw;
                        }

                        try
                        {
                            DoReadSTB();
                        }
                        catch
                        {
                            readStbSupported = false;
                        }
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
                    scpiIO.CloseSRQ();
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
            lock(commandLock)
                RaiseError(scpiIO.DeviceClear());
        }

        /// <summary>
        /// Reads the status byte.
        /// </summary>
        /// <returns></returns>
        protected virtual short DoReadSTB()
        {
            byte status = 0;
            lock(commandLock)
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
            // copy readBuffer to avoid reading/writing the thread static variable more than necessary.
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
                    string text = LockRetry(() => ReadString(length));
                    if (text.LastOrDefault() == TerminationCharacter)
                    {
                        // Do nothing, this is a hack to fix an issue with instruments including the termination
                        // character as the last character in the block.
                    }else
                    {
                        text += TerminationCharacter;
                        // Read the terminating character to get it off the output buffer
                        LockRetry(() => ReadString());    
                    }
                    
                    TerminationCharacterEnabled = true;

                    

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
                throw new ArgumentNullException(nameof(query));
            if (!IsConnected)
                throw new IOException("Not connected.");

            lock (commandLock)
            {
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

                    Log.Error("SCPI query failed ({0})", query);
                    Log.Error(ex);
                    return null;
                }

                IsConnected = true;
                if (!isSilent && VerboseLoggingEnabled)
                    Log.Debug("SCPI << {0}", result);
                return result;
            }
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

        private T[] ReadIEEEBlock<T>() where T : struct
        {
            return LockRetry<T[]>(() => DoReadIEEEBlock<T>());
        }

        //Work around for bug found IVI Shared Components. #2487
        private T[] DoReadIEEEBlock<T>(bool seekToBlock = true, bool flushToEND = true) where T: struct
        {
            //Variable Declarations
            int dataSize;
            int dataSizeLen;
            byte[] dataBytes;

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
   
            //Variables to process bytes coming back from the instrument
            int dataTypeLen = Marshal.SizeOf<T>();
            
            //Part 2) Format the bytes that come back from the read.
            //Note, if the instrument returns big endian data, we have to correct for it.
            var data = new T[dataSize / dataTypeLen];

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
                throw new ArgumentNullException(nameof(query));

            lock (commandLock)
            {
                if (FinegrainedLock && !Lock)
                    LockRetry(() => DoLock());

                try
                {
                    ScpiCommandInternal(query, false);
                    switch (Type.GetTypeCode(typeof(T)))
                    {
                        case TypeCode.Byte:
                        case TypeCode.SByte:
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Single:
                        case TypeCode.Double:
                            return ReadIEEEBlock<T>();

                        // Do binary conversion for 64 bit integers
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        {
                            // TODO: This looks unnecessary
                            // Figure out if any special handling is required for 64 bit reads at all
                            byte[] result = ReadIEEEBlock<byte>();

                            if (result.Length < 8)
                                return Array.Empty<T>();

                            T[] arr = new T[result.Length / 8];
                            Buffer.BlockCopy(result, 0, arr, 0, arr.Length * 8);
                            return arr;
                        }
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
                throw new ArgumentNullException(nameof(command));
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
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
                throw new ArgumentNullException(nameof(command));
            
            ScpiCommandInternal(command, QueryErrorAfterCommand);
        }
        
        void ScpiCommandInternal(string command, bool checkErrors)
        {
            if (!IsConnected)
                throw new IOException("Not connected.");

            lock (commandLock)
            {
                OnActivity();
                try
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    LockRetry(() => WriteString(command));
                    timer.Stop();
                    if (VerboseLoggingEnabled)
                        Log.Debug(timer, "SCPI >> {0}", command);

                    if (checkErrors)
                    {
                        WaitForOperationComplete();
                        queryErrors(noAllocation: true);
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
        }

        /// <summary>
        /// Sends a IEEE Block SCPI command to the instrument.
        /// </summary>
        public virtual void ScpiIEEEBlockCommand(string command, byte[] data)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (command.Contains("?"))
                throw new ArgumentException("command is a query: " + command);
            
            if (!IsConnected)
                throw new IOException("Not connected.");

            lock (commandLock)
            {
                OnActivity();

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
                        queryErrors(noAllocation: true);
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
        }

        /// <summary>
        /// Sends a IEEE Block SCPI command to the instrument with a Streaming interface for large data size.  Uses DirectIO.
        /// </summary>
        public virtual void ScpiIEEEBlockCommand(string command, Stream data, long maxSize = 0)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (command.Contains("?"))
                throw new ArgumentException("command is a query: " + command);
            if (!IsConnected)
                throw new IOException("Not connected.");

            lock (commandLock)
            {
                OnActivity();
                //TapThread.Sleep(); // Just giving the TestPlan a chance to abort if it has been requested to do so
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
                            queryErrors(noAllocation: true);
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

        /// <summary>Returns all the errors on the instrument error stack. Clears the list in the same call.</summary>
        /// <remarks>better performance version of QueryErrors that does not use additional memory when no errors exists.</remarks>
        /// <param name="suppressLogMessages">Dont emit log messages.</param>
        /// <param name="maxErrors">The maximal number of error messages to read.</param>
        /// <param name="noAllocation">Dont do any allocation. The result will always be empty.</param>
        /// <returns></returns>
        IList<ScpiError> queryErrors(bool suppressLogMessages = false, int maxErrors = 1000, bool noAllocation = false)
        {
            bool errorExists()
            {
                if (readStbSupported)
                    return (LockRetry(() => DoReadSTB()) & 0x4) != 0x00;
                return true;
            }

            IList<ScpiError> errors = Array.Empty<ScpiError>();
            while ( errorExists() && errors.Count < maxErrors)
            {
                ScpiError error = queryErrorParse();
                if (error.Code == 0)
                    break;
                LogEventType logLevel = LogEventType.Error;
                if (ScpiErrorsLogLevelOverrides.ContainsKey(error.Code))
                {
                    logLevel = ScpiErrorsLogLevelOverrides[error.Code];
                }
                if (!suppressLogMessages)
                {
                    Log.Debug("SCPI >> SYSTem:ERRor?");
                    Log.TraceEvent(logLevel, 0, string.Format("SCPI << {0}", error));
                }

                if (!noAllocation)
                {
                    if (errors.IsReadOnly)
                        errors = new List<ScpiError>();
                    errors.Add(error);
                }
            }
            return errors;
        }
        
        /// <summary>
        /// Returns all the errors on the instrument error stack. Clears the list in the same call.
        /// </summary>
        /// <param name="suppressLogMessages">if true the errors will not be logged.</param>
        /// <param name="maxErrors">The max number of errors to retrieve. Useful if instrument generates errors faster than they can be read.</param>
        /// <returns></returns>
        public List<ScpiError> QueryErrors(bool suppressLogMessages = false, int maxErrors = 1000)
        {
            lock (commandLock)
            {
                var errors = queryErrors(suppressLogMessages, maxErrors);
                if (errors is List<ScpiError> lst)
                    return lst;

                return errors.ToList();
            }
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
            string errorStr = QueryErr(true).Trim();
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
                QueryOpc();
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
        /// </summary>
        public void Reset()
        {
            CommandRst();
        }

        /// <summary> *IDN / Queries the instrument for a IDN string. </summary>
        protected virtual string QueryIdn() => ScpiQuery("*IDN?");
        
        /// <summary> *OPC / Operation Complete Query </summary>
        protected virtual string QueryOpc() => ScpiQuery("*OPC?");
        
        /// <summary> *RST / Reset Command </summary>
        protected virtual void CommandRst() => ScpiCommand("*RST");
        
        /// <summary> *CLS / Clear Status Command </summary>
        protected virtual void CommandCls() => ScpiCommand("*CLS");
        
        /// <summary> SYST:ERR? / Queries the instrument for errors.
        /// This will normally be in a format like '123,"Error message"'.  </summary>
        protected virtual string QueryErr(bool isSilent = false) => ScpiQuery("SYST:ERR?", isSilent);
        

        #region Service Request routines
        /// <summary>
        /// A delegate that is used by the <see cref="SRQ"/> event.
        /// </summary>
        /// <param name="sender">A reference to the <see cref="ScpiInstrument"/> that the SRQ originated from.</param>
        public delegate void ScpiSRQDelegate(ScpiInstrument sender);

        Dictionary<ScpiSRQDelegate,ScpiIOSrqDelegate> srqDelegates = new Dictionary<ScpiSRQDelegate, ScpiIOSrqDelegate>();
        /// <summary>
        /// This event is called whenever a SRQ is generated by the instrument.
        /// Adding a handler to this event will automatically enable SRQ transactions from the instrument when the instrument is opened/closed, or while the instrument is open.
        /// 
        /// To disable SRQ transactions all handlers added must be removed.
        /// </summary>
        public event ScpiSRQDelegate SRQ
        {
            // SRQ event delegates are primarily handled by the internal scpi IO
            // but the delegates needs to be converted into another kind to work.
            // hence the dictionary custom stuff.
            add
            {
                ScpiIOSrqDelegate delegate2 = x => value(this);
                scpiIO.SRQ += delegate2;
                srqDelegates[value] = delegate2;
            }
            remove
            {
                var del = srqDelegates[value];
                scpiIO.SRQ -= del;
                srqDelegates.Remove(value);
            }
        }
        
        #endregion
    }
}

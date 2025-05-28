//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Error codes from the IScpiIO methods
    /// </summary>
    public enum ScpiIOResult
    {
        /// <summary>
        /// Indicates successful completion.
        /// </summary>
        Success,
        /// <summary>
        /// Indicates successful completion because the requested amount of bytes were read/written.
        /// </summary>
        Success_MaxCount,
        /// <summary>
        /// Indicates successful completion because a terminating character was found.
        /// </summary>
        Success_TermChar,

        /// <summary>
        /// General unspecified error.
        /// </summary>
        Error_General,
        /// <summary>
        /// The call failed due to another client having a lock on the instrument.
        /// </summary>
        Error_ResourceLocked,
        /// <summary>
        /// Indicates that the call timed out.
        /// </summary>
        Error_Timeout,
        /// <summary>
        /// Indicates that the connection to the instrument was lost.
        /// </summary>
        Error_ConnectionLost,
        /// <summary>
        /// The resource could not be found.
        /// </summary>
        Error_ResourceNotFound
    }

    /// <summary>
    /// Types of VISA locks.
    /// </summary>
    public enum ScpiLockType
    {
        /// <summary>
        /// Indicates that the client should get an exclusive lock.
        /// </summary>
        Exclusive,
        /// <summary>
        /// Indicates that a client should get a shared lock.
        /// </summary>
        Shared
    }

    /// <summary>
    /// Represents low-level IO primitives for a given SCPI instrument.
    /// </summary>
    public interface IScpiIO
    {
        /// <summary>
        /// Clears the SCPI state, including any errors in the error queue.
        /// </summary>
        ScpiIOResult DeviceClear();
        /// <summary>
        /// Reads the status byte of the instrument.
        /// </summary>
        /// <param name="stb">The current status byte.</param>
        ScpiIOResult ReadSTB(ref byte stb);

        /// <summary>
        /// Reads a number of bytes from the instrument.
        /// </summary>
        /// <param name="buffer">The target buffer to read to.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="eoi">This will indicate whether an EOI indicator was received.</param>
        /// <param name="read">The number of bytes that was actually read if succesful.</param>
        ScpiIOResult Read(ArraySegment<byte> buffer, int count, ref bool eoi, ref int read);
        /// <summary>
        /// Writes a number of bytes to the instrument.
        /// </summary>
        /// <remarks>The returned error code will indicate whether the EOI was sent.</remarks>
        /// <param name="buffer">The buffer to write from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="written">The number of bytes that was actually written if succesful.</param>
        ScpiIOResult Write(ArraySegment<byte> buffer, int count, ref int written);

        /// <summary>
        /// Try to acquire a lock on the instrument.
        /// </summary>
        /// <param name="lockType">Indicates which kind of lock should be acquired.</param>
        /// <param name="sharedKey">In case the type of lock is </param>
        ScpiIOResult Lock(ScpiLockType lockType, string sharedKey = null);
        /// <summary>
        /// Unlock an instrument.
        /// </summary>
        ScpiIOResult Unlock();

        /// <summary>
        /// Indicates whether a <see cref="Write(ArraySegment{byte}, int, ref int)"/> should generate an end-of-message indicator when writing its last byte.
        /// </summary>
        bool SendEnd { get; set; }

        /// <summary>
        /// Indicates the timeout in milliseconds for any of the IO operations.
        /// </summary>
        int IOTimeoutMS { get; set; }
        /// <summary>
        /// Indicates the timeout in milliseconds for acquiring a lock.
        /// </summary>
        int LockTimeoutMS { get; set; }

        /// <summary>
        /// Sets the termination character, if any.
        /// </summary>
        byte TerminationCharacter { get; set; }
        /// <summary>
        /// Controls whether the IO operations should use a termination character.
        /// </summary>
        bool UseTerminationCharacter { get; set; }

        /// <summary>
        /// Returns the resource class of the connected instrument.
        /// </summary>
        string ResourceClass { get; }
    }
    
    /// <summary>
    /// Represents low-level IO primitives for a given SCPI instrument.
    /// </summary>
    public interface IScpiIO2 : IScpiIO
    {
        /// <summary> Opens a connection to the instrument. </summary>
        /// <param name="visaAddress">Visa address of the instrument.</param>
        /// <param name="lock">if the instrument should be locked when the connection is established.</param>
        /// <returns>Visa error code.</returns>
        ScpiIOResult Open(string visaAddress, bool @lock);
        /// <summary> Close the connection to the instrument. </summary>
        /// <returns>Visa Error code.</returns>
        ScpiIOResult Close();
        
        /// <summary> Resource ID attached to this instrument. </summary>
        int ID { get; }

        /// <summary> Callback from SRQ instrument events. </summary>
        event ScpiIOSrqDelegate SRQ;
        
        /// <summary>  Open SRQ Callback handling. </summary>
        void OpenSRQ();
        
        /// <summary> Close SRQ Callback handling. </summary>
        void CloseSRQ();
    }

    /// <summary>
    /// Events for scpi.
    /// </summary>
    public enum ScpiEvent
    {
        /// All enabled events.
        EVENT_ALL_ENABLED_EVENTS = Visa.VI_ALL_ENABLED_EVENTS,
        /// Notification that an asynchronous operation has completed.
        EVENT_IO_COMPLETION = Visa.VI_EVENT_IO_COMPLETION,
        /// Notification that a hardware trigger was received from a device.
        EVENT_TRIG = Visa.VI_EVENT_TRIG,
        /// Notification that a device is requesting service
        EVENT_SERVICE_REQ = Visa.VI_EVENT_SERVICE_REQ,
        /// Notification that the local controller has been sent a device clear message
        EVENT_CLEAR = Visa.VI_EVENT_CLEAR,
        /// Notification that an error condition has occurred during an operation invocation. (Note: the VI_QUEUE and VI_SUSPEND_HNDLR mechanisms cannot be used with this event.)
        EVENT_EXCEPTION = Visa.VI_EVENT_EXCEPTION,
        /// Notification that the GPIB controller has gained or lost CIC (controller in charge) status.
        EVENT_GPIB_CIC = Visa.VI_EVENT_GPIB_CIC,
        /// Notification that the GPIB controller has been addressed to talk.
        EVENT_GPIB_TALK = Visa.VI_EVENT_GPIB_TALK,
        /// Notification that the GPIB controller has been addressed to listen.
        EVENT_GPIB_LISTEN = Visa.VI_EVENT_GPIB_LISTEN,
        /// Notification that a vendor-specific PXI interrupt was received from the device. 
        EVENT_PXI_INTR = Visa.VI_EVENT_PXI_INTR,
        /// Notification that the VXI/VME SYSFAIL* line has been asserted. 
        EVENT_VXI_VME_SYSFAIL = Visa.VI_EVENT_VXI_VME_SYSFAIL,
        /// Notification that the VXI/VME SYSRESET* line has been asserted 
        EVENT_VXI_VME_SYSRESET = Visa.VI_EVENT_VXI_VME_SYSRESET,
        /// Notification that a VXI signal or VXI interrupt has been received from a device. 
        EVENT_VXI_SIGP = Visa.VI_EVENT_VXI_SIGP,
        /// Notification that a VXIbus interrupt was received from the device. 
        EVENT_VXI_VME_INTR = Visa.VI_EVENT_VXI_VME_INTR,
    }

    [Flags]
    public enum ScpiEventMechanism
    {
        QUEUE = Visa.VI_QUEUE,
        HNDLR = Visa.VI_HNDLR,
    }

    /// <summary>
    /// Represents low-level IO primitives for a given SCPI instrument. IScpiIO3 is the full interface needed to specify a VISA connection.
    /// </summary>
    public interface IScpiIO3 : IScpiIO2
    {
        /// <summary>
        /// Enables an event.
        /// </summary>
        public ScpiIOResult EnableEvent(ScpiEvent eventType, ScpiEventMechanism mechanism);

        /// <summary>
        /// Disables an event.
        /// </summary>
        public ScpiIOResult DisableEvent(ScpiEvent eventType, ScpiEventMechanism mechanism);

        /// <summary>
        /// Waits on an event.
        /// </summary>
        public ScpiIOResult WaitOnEvent(ScpiEvent eventType, int timeout, out ScpiEvent outEventType,
            out int outContext);
    }
    
    /// <summary> SCPI IO SRQ Event Delegate </summary>
    /// <param name="sender"></param>
    public delegate void ScpiIOSrqDelegate(IScpiIO2 sender);

    /// <summary> 
    /// Represents a connection to talk to any SCPI-enabled instrument.
    /// </summary>
    public interface IScpiInstrument : IInstrument
    {
        /// <summary>
        /// Get access to the low-level primitives of the connection.
        /// </summary>
        IScpiIO IO { get; }

        /// <summary>
        /// Sends a SCPI command to the instrument.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <remarks>Non-blocking.</remarks>
        void ScpiCommand(string command);
        /// <summary>
        /// Sends a SCPI query to the instrument and waits for a response.
        /// </summary>
        /// <param name="query">The SCPI query to send.</param>
        /// <param name="isSilent">True to suppress log messages.</param>
        /// <returns>The response from the instrument.</returns>
        string ScpiQuery(string query, bool isSilent = false);

        /// <summary>
        /// Sends a IEEE Block SCPI command to the instrument.
        /// </summary>
        void ScpiIEEEBlockCommand(string command, byte[] data);

        /// <summary>
        /// Sends a IEEE Block SCPI query to the instrument and waits for a response. The response is assumed to be IEEE block data.
        /// </summary>
        /// <param name="query">The SCPI query to send.</param>
        /// <returns>The response from the instrument.</returns>
        byte[] ScpiQueryBlock(string query);
    }
}

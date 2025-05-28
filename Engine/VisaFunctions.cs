using System;
using System.Text;

namespace OpenTap;

/// <summary>
/// Collection of all visa functions.
/// </summary>
public struct VisaFunctions
{
    /// <summary>
    /// Event handler for visa events.
    /// </summary>
    public delegate int ViEventHandler(int vi, int eventType, int context, int userHandle);
    
    /// <summary>
    /// This function returns a session to the Default Resource Manager resource.
    /// This function must be called before any VISA functions can be invoked.
    /// The first call to this function initializes the VISA system, including the Default Resource Manager resource, and also returns a session to that resource.
    /// Subsequent calls to this function return unique sessions to the same Default Resource Manager resource.
    /// </summary>
    /// <param name="sesn">Unique logical identifier to a Default Resource Manager session.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viOpenDefaultRM.htm</remarks>
    public delegate int ViOpenDefaultRmDelegate(out int sesn);
    /// <inheritdoc cref="ViOpenDefaultRmDelegate"/>
    public ViOpenDefaultRmDelegate ViOpenDefaultRmRef;
    
    /// <summary>
    /// This function queries a VISA system to locate the resources associated with a specified interface.
    /// This function matches the value specified in the expr parameter with the resources available for a particular interface.
    /// On successful completion, it returns the first resource found in the list and returns a count to indicate if there were more resources found that match the value specified in the expr parameter.
    /// </summary>
    /// <param name="sesn">Resource Manager session (should always be the Default Resource Manager for VISA returned from viOpenDefaultRM).</param>
    /// <param name="expr">This expression sets the criteria to search an interface or all interfaces for existing devices.</param>
    /// <param name="findList">Returns a handle identifying this search session. This handle will be used as an input in viFindNext.</param>
    /// <param name="retCount">Number of matches.</param>
    /// <param name="desc">Returns a string identifying the location of a device. Strings can then be passed to viOpen to establish a session to the given device.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viFindRsrc.htm</remarks>
    public delegate int ViFindRsrcDelegate(int sesn, string expr, out int findList, out int retCount, StringBuilder desc);
    /// <inheritdoc cref="ViFindRsrcDelegate"/>
    public ViFindRsrcDelegate ViFindRsrcRef;
    
    /// <summary>
    /// This function returns the next resource found in the list created by viFindRsrc.
    /// The list is referenced by the handle that was returned by viFindRsrc.
    /// </summary>
    /// <param name="findList">	Describes a find list. This parameter must be created by viFindRsrc.</param>
    /// <param name="desc">Returns a string identifying location of a device. Strings can be passed to viOpen to establish a session to the device.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viFindNext.htm</remarks>
    public delegate int ViFindNextDelegate(int findList, StringBuilder desc);
    /// <inheritdoc cref="ViFindRsrcDelegate"/>
    public ViFindNextDelegate ViFindNextRef;
    
    /// <summary>
    /// Parse a resource string to get the interface information.
    /// This operation parses a resource string to verify its validity.
    /// It should succeed for all strings returned by viFindRsrc and recognized by viOpen.
    /// This operation is useful if you want to know what interface a given resource descriptor would use without actually opening a session to it.
    /// </summary>
    /// <param name="sesn">Resource Manager session (should always be the Default Resource Manager for VISA returned from <see cref="VisaFunctions.ViOpenDefaultRmRef"/>).</param>
    /// <param name="desc">Unique symbolic name  (VISA address or VISA alias) of a resource.</param>
    /// <param name="intfType">Interface type of the given resource string.</param>
    /// <param name="intfNum">Board number of the interface of the given resource string.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viParseRsrc.htm</remarks>
    public delegate int ViParseRsrcDelegate(int sesn, string desc, ref short intfType, ref short intfNum);
    /// <inheritdoc cref="ViParseRsrcDelegate"/>
    public ViParseRsrcDelegate ViParseRsrcRef;
    
    /// <summary>
    /// This function parses a resource string to get extended interface information.
    /// It should succeed for all strings returned by viFindRsrc and recognized by viOpen.
    /// This operation is useful if you want to know what interface a given VISA address (resource descriptor) would use without actually opening a session to it.
    /// </summary>
    /// <param name="sesn">Resource Manager session (should always be the Default Resource Manager for VISA returned from <see cref="VisaFunctions.ViOpenDefaultRmRef"/>).</param>
    /// <param name="desc">Unique symbolic name of a resource.</param>
    /// <param name="intfType">Interface type of the given resource string.</param>
    /// <param name="intfNum">Board number of the interface of the given resource string.</param>
    /// <param name="rsrcClass">Specifies the resource class (for example, “INSTR”) of the given resource string, as defined in VISA Resource Classes.</param>
    /// <param name="expandedUnaliasedName">This is the expanded version of the given resource string.  The format should be similar to the VISA-defined canonical resource name.</param>
    /// <param name="aliasIfExists">Specifies the user-defined alias for the given resource string, if a VISA implementation allows aliases and an alias exists for the given resource string.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viParseRsrcEx.htm</remarks>
    public delegate int ViParseRsrcExDelegate(int sesn, string desc, ref short intfType, ref short intfNum,
        StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists);
    /// <inheritdoc cref="ViParseRsrcDelegate"/>
    public ViParseRsrcExDelegate ViParseRsrcExRef;
    
    /// <summary>
    /// This function opens a session to the specified device. It returns a session identifier that can be used to call any other functions to that device.
    /// </summary>
    /// <param name="sesn">Resource Manager session (should always be the Default Resource Manager for VISA returned from <see cref="VisaFunctions.ViOpenDefaultRmRef"/>).</param>
    /// <param name="viDesc">Unique symbolic name (VISA address) of a resource. Can also be a VISA alias (defined in the Keysight Connection Expert utility).</param>
    /// <param name="accessMode">
    /// Specifies the modes by which the resource is to be accessed.
    /// The value VI_EXCLUSIVE_LOCK is used to acquire an exclusive lock immediately upon opening a session.
    /// If a lock cannot be acquired, the session is closed and an error is returned.
    /// The VI_LOAD_CONFIG value is used to configure attributes specified by some external configuration utility.
    /// If this value is not used, the session uses the default values provided by this specification.
    /// Multiple access modes can be used simultaneously by specifying a "bit-wise OR" of the values. (Must use VI_NULL in VISA 1.0.)
    /// </param>
    /// <param name="timeout">
    /// If the accessMode parameter requires a lock,
    /// this parameter specifies the absolute time period (in milliseconds) that the resource waits to get unlocked before this operation returns an error.
    /// Otherwise, this parameter is ignored. (Must use VI_NULL in VISA 1.0.)
    /// Note: The timeout parameter affects ONLY the LOCK; it does not impact the overall viOpen command timing.
    /// </param>
    /// <param name="vi">Unique logical identifier reference to a session.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viOpen.htm</remarks>
    public delegate int ViOpenDelegate(int sesn, string viDesc, int accessMode, int timeout, out int vi);
    /// <inheritdoc cref="ViOpenDelegate"/>
    public ViOpenDelegate ViOpenRef;
    
    /// <summary>
    /// This function closes the specified resource manager session, device session, find list (returned from the viFindRsrc function),
    /// or event context (returned from the viWaitOnEvent function, or passed to an event handler).
    /// In this process, all the data structures that had been allocated for the specified vi are freed.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session, event, or find list.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viClose.htm</remarks>
    public delegate int ViCloseDelegate(int vi);
    /// <inheritdoc cref="ViCloseDelegate"/>
    public ViCloseDelegate ViCloseRef;
    
    /// <summary>
    /// This function retrieves the state of an attribute for the specified session.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session, event, or find list.</param>
    /// <param name="attrName">Resource attribute for which the state query is made.</param>
    /// <param name="attrValue">
    /// The state of the queried attribute for a specified resource.
    /// The interpretation of the returned value is defined by the individual resource.
    /// Note that you must allocate space for character strings returned.
    /// </param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viGetAttribute.htm</remarks>
    public delegate int ViGetAttributeBDelegate(int vi, int attrName, out byte attrValue);
    /// <inheritdoc cref="ViGetAttributeBDelegate"/>
    public ViGetAttributeBDelegate ViGetAttribute1Ref;
    
    /// <inheritdoc cref="ViGetAttributeBDelegate"/>
    public delegate int ViGetAttributeSbDelegate(int vi, int attrName, StringBuilder attrValue);
    /// <inheritdoc cref="ViGetAttributeBDelegate"/>
    public ViGetAttributeSbDelegate ViGetAttribute2Ref;
    
    /// <inheritdoc cref="ViGetAttributeBDelegate"/>
    public delegate int ViGetAttributeIDelegate(int vi, int attrName, out int attrValue);
    /// <inheritdoc cref="ViGetAttributeBDelegate"/>
    public ViGetAttributeIDelegate ViGetAttribute3Ref;
    
    /// <summary>
    /// This function sets the state of an attribute for the specified session.
    /// The viSetAttribute operation is used to modify the state of an attribute for the specified session, event, or find list.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session, event, or find list.</param>
    /// <param name="attrName">Resource attribute for which the state is modified.</param>
    /// <param name="attrValue">The state of the attribute to be set for the specified resource. The interpretation of the individual attribute value is defined by the resource.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viSetAttribute.htm</remarks>
    public delegate int ViSetAttributeBDelegate(int vi, int attrName, byte attrValue);
    /// <inheritdoc cref="ViSetAttributeBDelegate"/>
    public ViSetAttributeBDelegate ViSetAttribute1Ref;
    
    /// <inheritdoc cref="ViSetAttributeBDelegate"/>
    public delegate int ViSetAttributeIDelegate(int vi, int attrName, int attrValue);
    /// <inheritdoc cref="ViSetAttributeBDelegate"/>
    public ViSetAttributeIDelegate ViSetAttribute2Ref;
    
    /// <summary>
    /// This function returns a user-readable string that describes the status code passed to the function.
    /// If a status code cannot be interpreted by the session, viStatusDesc returns the warning VI_WARN_UNKNOWN_STATUS.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session, event, or find list.</param>
    /// <param name="status">Status code to interpret.</param>
    /// <param name="desc">The user-readable string interpretation of the status code passed to the function. Must be at least 256 characters to receive output.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viStatusDesc.htm</remarks>
    public delegate int ViStatusDescDelegate(int vi, int status, StringBuilder desc);
    /// <inheritdoc cref="ViStatusDescDelegate"/>
    public ViStatusDescDelegate ViStatusDescRef;
    
    /// <summary>
    /// This function enables notification of an event identified by the eventType parameter for mechanisms specified in the mechanism parameter.
    /// The specified session can be enabled  to queue events by specifying VI_QUEUE.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="eventType">Logical event identifier.</param>
    /// <param name="mechanism">
    /// Specifies event handling mechanisms to be enabled.
    /// The queuing mechanism is enabled by VI_QUEUE, and the callback mechanism is enabled by VI_HNDLR or VI_SUSPEND_HNDLR.
    /// It is possible to enable both mechanisms simultaneously by specifying "bit-wise OR" of VI_QUEUE and one of the two mode values for the callback mechanism.
    /// </param>
    /// <param name="context">VI_NULL (Not used for VISA 1.0.)</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viEnableEvent.htm</remarks>
    public delegate int ViEnableEventDelegate(int vi, int eventType, short mechanism, int context);
    /// <inheritdoc cref="ViEnableEventDelegate"/>
    public ViEnableEventDelegate ViEnableEventRef;
    
    /// <summary>
    /// This function disables servicing of an event identified by the eventType parameter for the mechanisms specified in the mechanism parameter.
    /// Specifying VI_ALL_ENABLED_EVENTS for the eventType parameter allows a session to stop receiving all events.
    /// The session can stop receiving queued events by specifying VI_QUEUE.
    /// Applications can stop receiving callback events by specifying either VI_HNDLR or VI_SUSPEND_HNDLR.
    /// Specifying VI_ALL_MECH disables both the queuing and callback mechanisms.
    /// viDisableEvent prevents new event occurrences from being added to the queue(s).
    /// However, event occurrences already existing in the queue(s) are not discarded.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="eventType">Logical event identifier.</param>
    /// <param name="mechanism">
    /// Specifies event handling mechanisms to be disabled. The queuing mechanism is disabled by specifying VI_QUEUE.
    /// The callback mechanism is disabled by specifying VI_HNDLR or VI_SUSPEND_HNDLR.
    /// It is possible to disable both mechanisms simultaneously  by specifying VI_ALL_MECH.
    /// </param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viDisableEvent.htm</remarks>
    public delegate int ViDisableEventDelegate(int vi, int eventType, short mechanism);
    /// <inheritdoc cref="ViDisableEventDelegate"/>
    public ViDisableEventDelegate ViDisableEventRef;
    
    /// <summary>
    /// This function allows applications to install handlers on sessions for event callbacks.
    /// The handler specified in the handler parameter is installed along with previously installed handlers for the specified event.
    /// Applications can specify a value in the userHandle parameter that is passed to the handler on its invocation.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="eventType">Logical event identifier.</param>
    /// <param name="handler">Interpreted as a valid reference to a handler to be installed by an application.</param>
    /// <param name="userHandle">A value specified by an application that can be used for identifying handlers uniquely for an event type.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viInstallHandler.htm</remarks>
    public delegate int ViInstallHandlerDelegate(int vi, int eventType, ViEventHandler handler, int userHandle);
    /// <inheritdoc cref="ViInstallHandlerRef"/>
    public ViInstallHandlerDelegate ViInstallHandlerRef;
    
    /// <summary>
    /// This function allows applications to uninstall handlers for events on sessions.
    /// Applications should also specify the value in the userHandle parameter that was passed to viInstallHandler while installing the handler.
    /// VISA identifies handlers uniquely using the handler reference and the userHandle.
    /// All the handlers or which the handler reference and the userHandle matches are uninstalled.  
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="eventType">Logical event identifier.</param>
    /// <param name="handler">Interpreted as a valid reference to a handler to be uninstalled by an application.</param>
    /// <param name="userHandle">A value specified by an application that can be used for identifying handlers uniquely in a session for an event.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viUninstallHandler.htm</remarks>
    public delegate int
        ViUninstallHandlerDelegate(int vi, int eventType, ViEventHandler handler, int userHandle);
    /// <inheritdoc cref="ViUninstallHandlerRef"/>
    public ViUninstallHandlerDelegate ViUninstallHandlerRef;
    
    /// <summary>
    /// This function waits for an occurrence of the specified event for a given session.
    /// In particular, this function suspends execution of an application thread and waits for an event inEventType for at least the time period specified by timeout.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="eventType">Logical identifier of the event(s) to wait for.</param>
    /// <param name="timeout">
    /// Absolute time period in time units that the resource shall wait for a specified event to occur before returning the time elapsed error.
    /// The time unit is in milliseconds.
    /// </param>
    /// <param name="outEventType">Logical identifier of the event actually received.</param>
    /// <param name="outContext">A handle specifying the unique occurrence of an event.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viWaitOnEvent.htm</remarks>
    public delegate int ViWaitOnEventDelegate(int vi, int eventType, int timeout, out int outEventType,
        out int outContext);
    /// <inheritdoc cref="ViWaitOnEventDelegate"/>
    public ViWaitOnEventDelegate ViWaitOnEventRef;
    
    /// <summary>
    /// This function synchronously transfers data from a device.
    /// The data that is read is stored in the buffer represented by buf.
    /// This function returns only when the transfer terminates.
    /// Only one synchronous read function can occur at any one time.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="buffer">Represents the location of a buffer to receive data from device.</param>
    /// <param name="count">Number of bytes to be read.</param>
    /// <param name="retCount">Represents the location of an integer that will be set to the number of bytes actually transferred.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viRead.htm</remarks>
    public delegate int ViReadDelegate(int vi, ArraySegment<byte> buffer, int count, out int retCount);
    /// <inheritdoc cref="ViReadDelegate"/>
    public ViReadDelegate ViReadRef;
    
    /// <summary>
    /// This function synchronously transfers data to a device. The data to be written is in the buffer represented by buf. This function returns only when the transfer terminates. Only one synchronous write function can occur at any one time. If you pass VI_NULL as the retCount parameter to the viWrite operation, the number of bytes transferred will not be returned. This may be useful if it is important to know only whether the operation succeeded or failed.
    /// </summary>
    /// <param name="vi">Unique logical identifier of a session.</param>
    /// <param name="buffer">Represents the location of a data block to be sent to device.</param>
    /// <param name="count">Specifies number of bytes to be written.</param>
    /// <param name="retCount">Represents the location of an integer that will be set to the number of bytes actually transferred.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viWrite.htm</remarks>
    public delegate int ViWriteDelegate(int vi, ArraySegment<byte> buffer, int count, out int retCount);
    /// <inheritdoc cref="ViWriteDelegate"/>
    public ViWriteDelegate ViWriteRef;
    
    /// <summary>
    /// Read a status byte of the service request.
    /// This operation reads a service request status from a service requester (the message-based device).
    /// For example, on the IEEE 488.2 interface, the message is read by polling devices.
    /// For other types of interfaces, a message is sent in response to a service request to retrieve status information.
    /// </summary>
    /// <param name="vi">Unique logical identifier to the session.</param>
    /// <param name="status">Service request status byte.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viReadSTB.htm</remarks>
    public delegate int ViReadStbDelegate(int vi, ref short status);
    /// <inheritdoc cref="ViReadStbDelegate"/>
    public ViReadStbDelegate ViReadStbRef;
    
    /// <summary>
    /// Clear a device. This operation performs an IEEE 488.1-style clear of the device.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    public delegate int ViClearDelegate(int vi);
    /// <inheritdoc cref="ViClearDelegate"/>
    public ViClearDelegate ViClearRef;
    
    /// <summary>
    /// This function is used to obtain a lock on the specified resource.
    /// The caller can specify the type of lock requested (exclusive or shared lock) and the length of time the operation will suspend while waiting to acquire the lock before timing out.
    /// This function can also be used for sharing and nesting locks.
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <param name="lockType">Specifies the type of lock requested, which can be VI_EXCLUSIVE_LOCK or VI_SHARED_LOCK.</param>
    /// <param name="timeout">
    /// Absolute time period (in milliseconds) that a resource waits to get unlocked by the locking session before returning this operation with an error.
    /// VI_TMO_IMMEDIATE and VI_TMO_INFINITE are also valid values.
    /// </param>
    /// <param name="requestedKey">
    /// This parameter is not used and should be set to VI_NULL when lockType is VI_EXCLUSIVE_LOCK (exclusive lock).
    /// When trying to lock the resource as VI_SHARED_LOCK (shared lock), a session can either set it to VI_NULL so that VISA generates an accessKey for the session, or the session can suggest an accessKey to use for the shared lock.
    /// </param>
    /// <param name="accessKey">
    /// This parameter should be set to VI_NULL when lockType is VI_EXCLUSIVE_LOCK (exclusive lock).
    /// When trying to lock the resource as VI_SHARED_LOCK (shared lock), the resource returns a unique access key for the lock if the operation succeeds.
    /// This accessKey can then be passed to other sessions to share the lock.
    /// </param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viLock.htm</remarks>
    public delegate int ViLockDelegate(int vi, int lockType, int timeout, string requestedKey,
        StringBuilder accessKey);
    /// <inheritdoc cref="ViLockDelegate"/>
    public ViLockDelegate ViLockRef;
    
    /// <summary>
    /// This function is used to relinquish a lock previously obtained using the <see cref="VisaFunctions.ViLockRef"/> function. 
    /// </summary>
    /// <param name="vi">Unique logical identifier to a session.</param>
    /// <remarks>https://helpfiles.keysight.com/IO_Libraries_Suite/English/IOLS_Linux/VISA/Content/visa/viUnlock.htm</remarks>
    public delegate int ViUnlockDelegate(int vi);
    /// <inheritdoc cref="ViUnlockDelegate"/>
    public ViUnlockDelegate ViUnlockRef;
}
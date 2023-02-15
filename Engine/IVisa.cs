//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Text;

namespace OpenTap 
{ 
    /// <summary>
    /// Interface implemented by VISA libraries
    /// </summary>
    public interface IVisa
    {
        /// <summary>Open default RM session</summary>
        int viOpenDefaultRM(out int sesn);
        /// <summary>Find device</summary>
        int viFindRsrc(int sesn, string expr, out int vi, out int retCount, StringBuilder desc);
        /// <summary>Find next device</summary>
        int viFindNext(int vi, StringBuilder desc);
        /// <summary>Parse resource string to get interface information</summary>
        int viParseRsrc(int sesn, string desc, ref short intfType, ref short intfNum);
        /// <summary>Parse resource string to get extended interface information</summary>
        int viParseRsrcEx(int sesn, string desc, ref short intfType, ref short intfNum, StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists);
        /// <summary>Open session</summary>
        int viOpen(int sesn, string viDesc, int mode, int timeout, out int vi);
        /// <summary>Close session</summary>
        int viClose(int vi);
        /// <summary>Get attribute, returning a byte</summary>
        int viGetAttribute1(int vi, int attrName, out byte attrValue);
        /// <summary>Get attribute, filling a pre-allocated StringBuilder buffer</summary>
        int viGetAttribute2(int vi, int attrName, StringBuilder attrValue);
        /// <summary>Get attribute, returning an int</summary>
        int viGetAttribute3(int vi, int attrName, out int attrValue);
        /// <summary>Set attribute, providing a byte value</summary>
        int viSetAttribute1(int vi, int attrName, byte attrValue);
        /// <summary>Set attribute, providing an int value</summary>
        int viSetAttribute2(int vi, int attrName, int attrValue);
        /// <summary>Get status code description</summary>
        int viStatusDesc(int vi, int status, StringBuilder desc);
        /// <summary>Enable event</summary>
        int viEnableEvent(int vi, int eventType, short mechanism, int context);
        /// <summary>Disable event</summary>
        int viDisableEvent(int vi, int eventType, short mechanism);
        /// <summary>Install handler</summary>
        int viInstallHandler(int vi, int eventType, viEventHandler handler, int UserHandle);
        /// <summary>Uninstall handler</summary>
        int viUninstallHandler(int vi, int eventType, viEventHandler handler, int userHandle);
        /// <summary>Read data from device</summary>
        unsafe int viRead(int vi, ArraySegment<Byte> buffer, int count, out int retCount);
        /// <summary>Write data to device</summary>
        unsafe int viWrite(int vi, ArraySegment<Byte> buffer, int count, out int retCount);
        /// <summary>Read status byte</summary>
        int viReadSTB(int vi, ref short status);
        /// <summary>Clear a device</summary>
        int viClear(int vi);
        /// <summary>Lock resource</summary>
        int viLock(int vi, int lockType, int timeout, string requestedKey, StringBuilder accessKey);
        /// <summary>Unlock resource</summary>
        int viUnlock(int vi);
        /// <summary>Event handler prototype</summary>
        delegate int viEventHandler(int vi, int eventType, int context, int userHandle);
    }
}
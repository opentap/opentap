//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

internal static class Visa
{
    // --------------------------------------------------------------------------------
    //  Adapted from visa32.bas which was distributed by IVI Foundation
    //  Distributed By Kesight Technologies, Inc.
    // --------------------------------------------------------------------------------
    //  Title   : visa32.cs
    //  Date    : 09-13-2016
    //  Purpose : VISA definitions for C#
    // -------------------------------------------------------------------------
    public const int VI_SPEC_VERSION = 0x00500100;

    public delegate int viEventHandler(int vi, int eventType, int context, int userHandle);

    #region Delegates
    private delegate int viOpenDefaultRMDelegate(out int sesn);
    private delegate int viFindRsrcDelegate(int sesn, string expr, out int vi, out int retCount, StringBuilder desc);
    private delegate int viFindNextDelegate(int vi, StringBuilder desc);
    private delegate int viParseRsrcDelegate(int sesn, string desc, ref short intfType, ref short intfNum);
    private delegate int viParseRsrcExDelegate(int sesn, string desc, ref short intfType, ref short intfNum, StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists);
    private delegate int viOpenDelegate(int sesn, string viDesc, int mode, int timeout, out int vi);
    private delegate int viCloseDelegate(int vi);
    private delegate int viGetAttributeDelegate1(int vi, int attrName, out byte attrValue);
    private delegate int viGetAttributeDelegate2(int vi, int attrName, StringBuilder attrValue);
    private delegate int viGetAttributeDelegate3(int vi, int attrName, out int attrValue);
    private delegate int viSetAttributeDelegate1(int vi, int attrName, byte attrValue);
    private delegate int viSetAttributeDelegate2(int vi, int attrName, int attrValue);
    private delegate int viStatusDescDelegate(int vi, int status, StringBuilder desc);
    private delegate int viEnableEventDelegate(int vi, int eventType, short mechanism, int context);
    private delegate int viDisableEventDelegate(int vi, int eventType, short mechanism);
    private delegate int viInstallHandlerDelegate(int vi, int eventType, viEventHandler handler, int UserHandle);
    private delegate int viUninstallHandlerDelegate(int vi, int eventType, viEventHandler handler, int userHandle);
    private unsafe delegate int viReadDelegate(int vi, byte* buffer, int count, out int retCount);
    private unsafe delegate int viWriteDelegate(int vi, byte* buffer, int count, out int retCount);
    private delegate int viReadSTBDelegate(int vi, ref short status);
    private delegate int viClearDelegate(int vi);
    private delegate int viLockDelegate(int vi, int lockType, int timeout, string requestedKey, StringBuilder accessKey);
    private delegate int viUnlockDelegate(int vi);

    private static viOpenDefaultRMDelegate viOpenDefaultRMRef;
    private static viFindRsrcDelegate viFindRsrcRef;
    private static viFindNextDelegate viFindNextRef;
    private static viParseRsrcDelegate viParseRsrcRef;
    private static viParseRsrcExDelegate viParseRsrcExRef;
    private static viOpenDelegate viOpenRef;
    private static viCloseDelegate viCloseRef;
    private static viGetAttributeDelegate1 viGetAttribute1Ref;
    private static viGetAttributeDelegate2 viGetAttribute2Ref;
    private static viGetAttributeDelegate3 viGetAttribute3Ref;
    private static viSetAttributeDelegate1 viSetAttribute1Ref;
    private static viSetAttributeDelegate2 viSetAttribute2Ref;
    private static viStatusDescDelegate viStatusDescRef;
    private static viEnableEventDelegate viEnableEventRef;
    private static viDisableEventDelegate viDisableEventRef;
    private static viInstallHandlerDelegate viInstallHandlerRef;
    private static viUninstallHandlerDelegate viUninstallHandlerRef;
    private static viReadDelegate viReadRef;
    private static viWriteDelegate viWriteRef;
    private static viReadSTBDelegate viReadSTBRef;
    private static viClearDelegate viClearRef;
    private static viLockDelegate viLockRef;
    private static viUnlockDelegate viUnlockRef;

    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string filename);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procname);

    [DllImport("libdl.so")]
    static extern IntPtr dlopen(string filename, int flags);

    [DllImport("libdl.so")]
    static extern IntPtr dlsym(IntPtr handle, string symbol);

    const int RTLD_NOW = 2; // for dlopen's flags 

    static T GetSymbol<T>(IntPtr s)
    {
        return Marshal.GetDelegateForFunctionPointer<T>(s);
    }

    static Visa()
    {
        bool IsWin32 = OpenTap.OperatingSystem.Current == OpenTap.OperatingSystem.Windows; 

        Func<IntPtr> LoadLib;
        Func<IntPtr, string, int, IntPtr> LoadSym;

        if (IsWin32)
        {
            LoadLib = () => LoadLibrary("visa32.dll");
            LoadSym = (lib_handle, name, ord) => GetProcAddress(lib_handle, name);
        }
        else
        {
            LoadLib = () =>
            {
                string[] paths = {"./libvisa32.so", "./libvisa.so", "libvisa32.so", "libvisa.so", "libiovisa.so", "./libiovisa.so"};
                return paths.Select(x => dlopen(x, flags: RTLD_NOW))
                    .FirstOrDefault(x => x != IntPtr.Zero);
            };
            LoadSym = (lib_handle, name, ord) => dlsym(lib_handle, name);
        }

        var lib = LoadLib();

        viOpenDefaultRMRef = GetSymbol<viOpenDefaultRMDelegate>(LoadSym(lib, "viOpenDefaultRM", 141));

        viFindRsrcRef = GetSymbol<viFindRsrcDelegate>(LoadSym(lib, "viFindRsrc", 129));
        viFindNextRef = GetSymbol<viFindNextDelegate>(LoadSym(lib, "viFindNext", 130));
        viParseRsrcRef = GetSymbol<viParseRsrcDelegate>(LoadSym(lib, "viParseRsrc", 146));
        viParseRsrcExRef = GetSymbol<viParseRsrcExDelegate>(LoadSym(lib, "viParseRsrcEx", 147));

        viOpenRef = GetSymbol<viOpenDelegate>(LoadSym(lib, "viOpen", 131));
        viCloseRef = GetSymbol<viCloseDelegate>(LoadSym(lib, "viClose", 132));

        viReadRef = GetSymbol<viReadDelegate>(LoadSym(lib, "viRead", 256));
        viWriteRef = GetSymbol<viWriteDelegate>(LoadSym(lib, "viWrite", 257));
        viReadSTBRef = GetSymbol<viReadSTBDelegate>(LoadSym(lib, "viReadSTB", 259));
        viClearRef = GetSymbol<viClearDelegate>(LoadSym(lib, "viClear", 260));

        viLockRef = GetSymbol<viLockDelegate>(LoadSym(lib, "viLock", 144));
        viUnlockRef = GetSymbol<viUnlockDelegate>(LoadSym(lib, "viUnlock", 145));

        viGetAttribute1Ref = GetSymbol<viGetAttributeDelegate1>(LoadSym(lib, "viGetAttribute", 133));
        viGetAttribute2Ref = GetSymbol<viGetAttributeDelegate2>(LoadSym(lib, "viGetAttribute", 133));
        viGetAttribute3Ref = GetSymbol<viGetAttributeDelegate3>(LoadSym(lib, "viGetAttribute", 133));
        viSetAttribute1Ref = GetSymbol<viSetAttributeDelegate1>(LoadSym(lib, "viSetAttribute", 134));
        viSetAttribute2Ref = GetSymbol<viSetAttributeDelegate2>(LoadSym(lib, "viSetAttribute", 134));

        viStatusDescRef = GetSymbol<viStatusDescDelegate>(LoadSym(lib, "viStatusDesc", 142));

        viEnableEventRef = GetSymbol<viEnableEventDelegate>(LoadSym(lib, "viEnableEvent", 135));
        viDisableEventRef = GetSymbol<viDisableEventDelegate>(LoadSym(lib, "viDisableEvent", 136));
        viInstallHandlerRef = GetSymbol<viInstallHandlerDelegate>(LoadSym(lib, "viInstallHandler", 139));
        viUninstallHandlerRef = GetSymbol<viUninstallHandlerDelegate>(LoadSym(lib, "viUninstallHandler", 140));
    }
    #endregion

    internal static int viOpenDefaultRM(out int sesn) { return viOpenDefaultRMRef(out sesn); }

    internal static int viFindRsrc(int sesn, string expr, out int vi, out int retCount, StringBuilder desc) { return viFindRsrcRef(sesn, expr, out vi, out retCount, desc); }
    internal static int viFindNext(int vi, StringBuilder desc) { return viFindNextRef(vi, desc); }

    internal static int viParseRsrc(int sesn, string desc, ref short intfType, ref short intfNum) { return viParseRsrcRef(sesn, desc, ref intfType, ref intfNum); }
    internal static int viParseRsrcEx(int sesn, string desc, ref short intfType, ref short intfNum, StringBuilder rsrcClass, StringBuilder expandedUnaliasedName, StringBuilder aliasIfExists) { return viParseRsrcExRef(sesn, desc, ref intfType, ref intfNum, rsrcClass, expandedUnaliasedName, aliasIfExists); }

    internal static int viOpen(int sesn, string viDesc, int mode, int timeout, out int vi) { return viOpenRef(sesn, viDesc, mode, timeout, out vi); }
    internal static int viClose(int vi) { return viCloseRef(vi); }

    internal static int viGetAttribute(int vi, int attrName, out byte attrValue) { return viGetAttribute1Ref(vi, attrName, out attrValue); }
    internal static int viGetAttribute(int vi, int attrName, StringBuilder attrValue) { return viGetAttribute2Ref(vi, attrName, attrValue); }
    internal static int viGetAttribute(int vi, int attrName, out int attrValue) { return viGetAttribute3Ref(vi, attrName, out attrValue); }
    internal static int viSetAttribute(int vi, int attrName, byte attrValue) { return viSetAttribute1Ref(vi, attrName, attrValue); }
    internal static int viSetAttribute(int vi, int attrName, int attrValue) { return viSetAttribute2Ref(vi, attrName, attrValue); }

    internal static int viStatusDesc(int vi, int status, StringBuilder desc) { return viStatusDescRef(vi, status, desc); }

    internal static int viEnableEvent(int vi, int eventType, short mechanism, int context) { return viEnableEventRef(vi, eventType, mechanism, context); }
    internal static int viDisableEvent(int vi, int eventType, short mechanism) { return viDisableEventRef(vi, eventType, mechanism); }
    internal static int viInstallHandler(int vi, int eventType, viEventHandler handler, int UserHandle) { return viInstallHandlerRef(vi, eventType, handler, UserHandle); }
    internal static int viUninstallHandler(int vi, int eventType, viEventHandler handler, int userHandle) { return viUninstallHandlerRef(vi, eventType, handler, userHandle); }

    internal unsafe static int viRead(int vi, ArraySegment<byte> buffer, int count, out int retCount)
    {
        if (buffer.Count < count)
            throw new ArgumentException("Amount of bytes requested is larger than the space available in buffer.");
        
        fixed (byte* p = &buffer.Array[buffer.Offset])
        {
            return viReadRef(vi, p, count, out retCount);
        }
    }
    internal unsafe static int viWrite(int vi, ArraySegment<byte> buffer, int count, out int retCount)
    {
        if (buffer.Count < count)
            throw new ArgumentException("Amount of bytes requested to be written is larger than the space present in buffer.");

        fixed (byte* p = &buffer.Array[buffer.Offset])
        {
            return viWriteRef(vi, p, count, out retCount);
        }
    }

    internal static int viReadSTB(int vi, ref short status) { return viReadSTBRef(vi, ref status); }

    internal static int viClear(int vi) { return viClearRef(vi); }

    internal static int viLock(int vi, int lockType, int timeout, string requestedKey, StringBuilder accessKey) { return viLockRef(vi, lockType, timeout, requestedKey, accessKey); }
    internal static int viUnlock(int vi) { return viUnlockRef(vi); }

    #region - Constants -------------------------------------------------------------

    #region - Attributes ------------------------------------------------------------
    public const int VI_ATTR_RSRC_CLASS = unchecked((int)0xbfff0001);
    public const int VI_ATTR_RSRC_NAME = unchecked((int)0xbfff0002);
    public const int VI_ATTR_RSRC_IMPL_VERSION = 0x3fff0003;
    public const int VI_ATTR_RSRC_LOCK_STATE = 0x3fff0004;
    public const int VI_ATTR_MAX_QUEUE_LENGTH = 0x3fff0005;
    public const int VI_ATTR_USER_DATA_32 = 0x3fff0007;
    public const int VI_ATTR_FDC_CHNL = 0x3fff000d;
    public const int VI_ATTR_FDC_MODE = 0x3fff000f;
    public const int VI_ATTR_FDC_GEN_SIGNAL_EN = 0x3fff0011;
    public const int VI_ATTR_FDC_USE_PAIR = 0x3fff0013;
    public const int VI_ATTR_SEND_END_EN = 0x3fff0016;
    public const int VI_ATTR_TERMCHAR = 0x3fff0018;
    public const int VI_ATTR_TMO_VALUE = 0x3fff001a;
    public const int VI_ATTR_GPIB_READDR_EN = 0x3fff001b;
    public const int VI_ATTR_IO_PROT = 0x3fff001c;
    public const int VI_ATTR_DMA_ALLOW_EN = 0x3fff001e;
    public const int VI_ATTR_ASRL_BAUD = 0x3fff0021;
    public const int VI_ATTR_ASRL_DATA_BITS = 0x3fff0022;
    public const int VI_ATTR_ASRL_PARITY = 0x3fff0023;
    public const int VI_ATTR_ASRL_STOP_BITS = 0x3fff0024;
    public const int VI_ATTR_ASRL_FLOW_CNTRL = 0x3fff0025;
    public const int VI_ATTR_RD_BUF_OPER_MODE = 0x3fff002a;
    public const int VI_ATTR_RD_BUF_SIZE = 0x3fff002b;
    public const int VI_ATTR_WR_BUF_OPER_MODE = 0x3fff002d;
    public const int VI_ATTR_WR_BUF_SIZE = 0x3fff002e;
    public const int VI_ATTR_SUPPRESS_END_EN = 0x3fff0036;
    public const int VI_ATTR_TERMCHAR_EN = 0x3fff0038;
    public const int VI_ATTR_DEST_ACCESS_PRIV = 0x3fff0039;
    public const int VI_ATTR_DEST_BYTE_ORDER = 0x3fff003a;
    public const int VI_ATTR_SRC_ACCESS_PRIV = 0x3fff003c;
    public const int VI_ATTR_SRC_BYTE_ORDER = 0x3fff003d;
    public const int VI_ATTR_SRC_INCREMENT = 0x3fff0040;
    public const int VI_ATTR_DEST_INCREMENT = 0x3fff0041;
    public const int VI_ATTR_WIN_ACCESS_PRIV = 0x3fff0045;
    public const int VI_ATTR_WIN_BYTE_ORDER = 0x3fff0047;
    public const int VI_ATTR_GPIB_ATN_STATE = 0x3fff0057;
    public const int VI_ATTR_GPIB_ADDR_STATE = 0x3fff005c;
    public const int VI_ATTR_GPIB_CIC_STATE = 0x3fff005e;
    public const int VI_ATTR_GPIB_NDAC_STATE = 0x3fff0062;
    public const int VI_ATTR_GPIB_SRQ_STATE = 0x3fff0067;
    public const int VI_ATTR_GPIB_SYS_CNTRL_STATE = 0x3fff0068;
    public const int VI_ATTR_GPIB_HS488_CBL_LEN = 0x3fff0069;
    public const int VI_ATTR_CMDR_LA = 0x3fff006b;
    public const int VI_ATTR_VXI_DEV_CLASS = 0x3fff006c;
    public const int VI_ATTR_MAINFRAME_LA = 0x3fff0070;
    public const int VI_ATTR_MANF_NAME = unchecked((int)0xbfff0072);
    public const int VI_ATTR_MODEL_NAME = unchecked((int)0xbfff0077);
    public const int VI_ATTR_VXI_VME_INTR_STATUS = 0x3fff008b;
    public const int VI_ATTR_VXI_TRIG_STATUS = 0x3fff008d;
    public const int VI_ATTR_VXI_VME_SYSFAIL_STATE = 0x3fff0094;
    public const int VI_ATTR_WIN_BASE_ADDR_32 = 0x3fff0098;
    public const int VI_ATTR_WIN_SIZE_32 = 0x3fff009a;
    public const int VI_ATTR_ASRL_AVAIL_NUM = 0x3fff00ac;
    public const int VI_ATTR_MEM_BASE_32 = 0x3fff00ad;
    public const int VI_ATTR_ASRL_CTS_STATE = 0x3fff00ae;
    public const int VI_ATTR_ASRL_DCD_STATE = 0x3fff00af;
    public const int VI_ATTR_ASRL_DSR_STATE = 0x3fff00b1;
    public const int VI_ATTR_ASRL_DTR_STATE = 0x3fff00b2;
    public const int VI_ATTR_ASRL_END_IN = 0x3fff00b3;
    public const int VI_ATTR_ASRL_END_OUT = 0x3fff00b4;
    public const int VI_ATTR_ASRL_REPLACE_CHAR = 0x3fff00be;
    public const int VI_ATTR_ASRL_RI_STATE = 0x3fff00bf;
    public const int VI_ATTR_ASRL_RTS_STATE = 0x3fff00c0;
    public const int VI_ATTR_ASRL_XON_CHAR = 0x3fff00c1;
    public const int VI_ATTR_ASRL_XOFF_CHAR = 0x3fff00c2;
    public const int VI_ATTR_WIN_ACCESS = 0x3fff00c3;
    public const int VI_ATTR_RM_SESSION = 0x3fff00c4;
    public const int VI_ATTR_VXI_LA = 0x3fff00d5;
    public const int VI_ATTR_MANF_ID = 0x3fff00d9;
    public const int VI_ATTR_MEM_SIZE_32 = 0x3fff00dd;
    public const int VI_ATTR_MEM_SPACE = 0x3fff00de;
    public const int VI_ATTR_MODEL_CODE = 0x3fff00df;
    public const int VI_ATTR_SLOT = 0x3fff00e8;
    public const int VI_ATTR_INTF_INST_NAME = unchecked((int)0xbfff00e9);
    public const int VI_ATTR_IMMEDIATE_SERV = 0x3fff0100;
    public const int VI_ATTR_INTF_PARENT_NUM = 0x3fff0101;
    public const int VI_ATTR_RSRC_SPEC_VERSION = 0x3fff0170;
    public const int VI_ATTR_INTF_TYPE = 0x3fff0171;
    public const int VI_ATTR_GPIB_PRIMARY_ADDR = 0x3fff0172;
    public const int VI_ATTR_GPIB_SECONDARY_ADDR = 0x3fff0173;
    public const int VI_ATTR_RSRC_MANF_NAME = unchecked((int)0xbfff0174);
    public const int VI_ATTR_RSRC_MANF_ID = 0x3fff0175;
    public const int VI_ATTR_INTF_NUM = 0x3fff0176;
    public const int VI_ATTR_TRIG_ID = 0x3fff0177;
    public const int VI_ATTR_GPIB_REN_STATE = 0x3fff0181;
    public const int VI_ATTR_GPIB_UNADDR_EN = 0x3fff0184;
    public const int VI_ATTR_DEV_STATUS_BYTE = 0x3fff0189;
    public const int VI_ATTR_FILE_APPEND_EN = 0x3fff0192;
    public const int VI_ATTR_VXI_TRIG_SUPPORT = 0x3fff0194;
    public const int VI_ATTR_TCPIP_ADDR = unchecked((int)0xbfff0195);
    public const int VI_ATTR_TCPIP_HOSTNAME = unchecked((int)0xbfff0196);
    public const int VI_ATTR_TCPIP_PORT = 0x3fff0197;
    public const int VI_ATTR_TCPIP_DEVICE_NAME = unchecked((int)0xbfff0199);
    public const int VI_ATTR_TCPIP_NODELAY = 0x3fff019a;
    public const int VI_ATTR_TCPIP_KEEPALIVE = 0x3fff019b;
    public const int VI_ATTR_4882_COMPLIANT = 0x3fff019f;
    public const int VI_ATTR_USB_SERIAL_NUM = unchecked((int)0xbfff01a0);
    public const int VI_ATTR_USB_INTFC_NUM = 0x3fff01a1;
    public const int VI_ATTR_USB_PROTOCOL = 0x3fff01a7;
    public const int VI_ATTR_USB_MAX_INTR_SIZE = 0x3fff01af;
    public const int VI_ATTR_PXI_DEV_NUM = 0x3fff0201;
    public const int VI_ATTR_PXI_FUNC_NUM = 0x3fff0202;
    public const int VI_ATTR_PXI_BUS_NUM = 0x3fff0205;
    public const int VI_ATTR_PXI_CHASSIS = 0x3fff0206;
    public const int VI_ATTR_PXI_SLOTPATH = unchecked((int)0xbfff0207);
    public const int VI_ATTR_PXI_SLOT_LBUS_LEFT = 0x3fff0208;
    public const int VI_ATTR_PXI_SLOT_LBUS_RIGHT = 0x3fff0209;
    public const int VI_ATTR_PXI_TRIG_BUS = 0x3fff020a;
    public const int VI_ATTR_PXI_STAR_TRIG_BUS = 0x3fff020b;
    public const int VI_ATTR_PXI_STAR_TRIG_LINE = 0x3fff020c;
    public const int VI_ATTR_PXI_SRC_TRIG_BUS = 0x3fff020d;
    public const int VI_ATTR_PXI_DEST_TRIG_BUS = 0x3fff020e;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR0 = 0x3fff0211;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR1 = 0x3fff0212;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR2 = 0x3fff0213;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR3 = 0x3fff0214;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR4 = 0x3fff0215;
    public const int VI_ATTR_PXI_MEM_TYPE_BAR5 = 0x3fff0216;
    public const int VI_ATTR_PXI_MEM_BASE_BAR0_32 = 0x3fff0221;
    public const int VI_ATTR_PXI_MEM_BASE_BAR1_32 = 0x3fff0222;
    public const int VI_ATTR_PXI_MEM_BASE_BAR2_32 = 0x3fff0223;
    public const int VI_ATTR_PXI_MEM_BASE_BAR3_32 = 0x3fff0224;
    public const int VI_ATTR_PXI_MEM_BASE_BAR4_32 = 0x3fff0225;
    public const int VI_ATTR_PXI_MEM_BASE_BAR5_32 = 0x3fff0226;
    public const int VI_ATTR_PXI_MEM_BASE_BAR0_64 = 0x3fff0228;
    public const int VI_ATTR_PXI_MEM_BASE_BAR1_64 = 0x3fff0229;
    public const int VI_ATTR_PXI_MEM_BASE_BAR2_64 = 0x3fff022a;
    public const int VI_ATTR_PXI_MEM_BASE_BAR3_64 = 0x3fff022b;
    public const int VI_ATTR_PXI_MEM_BASE_BAR4_64 = 0x3fff022c;
    public const int VI_ATTR_PXI_MEM_BASE_BAR5_64 = 0x3fff022d;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR0_32 = 0x3fff0231;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR1_32 = 0x3fff0232;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR2_32 = 0x3fff0233;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR3_32 = 0x3fff0234;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR4_32 = 0x3fff0235;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR5_32 = 0x3fff0236;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR0_64 = 0x3fff0238;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR1_64 = 0x3fff0239;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR2_64 = 0x3fff023a;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR3_64 = 0x3fff023b;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR4_64 = 0x3fff023c;
    public const int VI_ATTR_PXI_MEM_SIZE_BAR5_64 = 0x3fff023d;
    public const int VI_ATTR_PXI_IS_EXPRESS = 0x3fff0240;
    public const int VI_ATTR_PXI_SLOT_LWIDTH = 0x3fff0241;
    public const int VI_ATTR_PXI_MAX_LWIDTH = 0x3fff0242;
    public const int VI_ATTR_PXI_ACTUAL_LWIDTH = 0x3fff0243;
    public const int VI_ATTR_PXI_DSTAR_BUS = 0x3fff0244;
    public const int VI_ATTR_PXI_DSTAR_SET = 0x3fff0245;
    public const int VI_ATTR_PXI_ALLOW_WRITE_COMBINE = 0x3fff0246;
    public const int VI_ATTR_TCPIP_HISLIP_OVERLAP_EN = 0x3fff0300;
    public const int VI_ATTR_TCPIP_HISLIP_VERSION = 0x3fff0301;
    public const int VI_ATTR_TCPIP_HISLIP_MAX_MESSAGE_KB = 0x3fff0302;
    public const int VI_ATTR_TCPIP_IS_HISLIP = 0x3fff0303;
    public const int VI_ATTR_JOB_ID = 0x3fff4006;
    public const int VI_ATTR_EVENT_TYPE = 0x3fff4010;
    public const int VI_ATTR_SIGP_STATUS_ID = 0x3fff4011;
    public const int VI_ATTR_RECV_TRIG_ID = 0x3fff4012;
    public const int VI_ATTR_INTR_STATUS_ID = 0x3fff4023;
    public const int VI_ATTR_STATUS = 0x3fff4025;
    public const int VI_ATTR_RET_COUNT_32 = 0x3fff4026;
    public const int VI_ATTR_BUFFER = 0x3fff4027;
    public const int VI_ATTR_RECV_INTR_LEVEL = 0x3fff4041;
    public const int VI_ATTR_OPER_NAME = unchecked((int)0xbfff4042);
    public const int VI_ATTR_GPIB_RECV_CIC_STATE = 0x3fff4193;
    public const int VI_ATTR_RECV_TCPIP_ADDR = unchecked((int)0xbfff4198);
    public const int VI_ATTR_USB_RECV_INTR_SIZE = 0x3fff41b0;
    public const int VI_ATTR_USB_RECV_INTR_DATA = unchecked((int)0xbfff41b1);
    public const int VI_ATTR_PXI_RECV_INTR_SEQ = 0x3fff4240;
    public const int VI_ATTR_PXI_RECV_INTR_DATA = 0x3fff4241;

    public const int VI_ATTR_WIN_BASE_ADDR_64 = 0x3fff009b;
    public const int VI_ATTR_WIN_SIZE_64 = 0x3fff009c;
    public const int VI_ATTR_MEM_BASE_64 = 0x3fff00d0;
    public const int VI_ATTR_MEM_SIZE_64 = 0x3fff00d1;

    // In VISA C, these are only defined in 64-bit applications
    public const int VI_ATTR_USER_DATA_64 = 0x3fff000a;
    public const int VI_ATTR_RET_COUNT_64 = 0x3fff4028;
    #endregion Attributes

    #region - Bitness dependent attributes ------------------------------------------
    private static bool Is64bit { get { return (IntPtr.Size == 8); } }
    public static int VI_ATTR_USER_DATA { get { if (Is64bit) { return VI_ATTR_USER_DATA_64; } else { return VI_ATTR_USER_DATA_32; } } }
    public static int VI_ATTR_RET_COUNT { get { if (Is64bit) { return VI_ATTR_RET_COUNT_64; } else { return VI_ATTR_RET_COUNT_32; } } }
    public static int VI_ATTR_WIN_BASE_ADDR { get { if (Is64bit) { return VI_ATTR_WIN_BASE_ADDR_64; } else { return VI_ATTR_WIN_BASE_ADDR_32; } } }
    public static int VI_ATTR_WIN_SIZE { get { if (Is64bit) { return VI_ATTR_WIN_SIZE_64; } else { return VI_ATTR_WIN_SIZE_32; } } }
    public static int VI_ATTR_MEM_BASE { get { if (Is64bit) { return VI_ATTR_MEM_BASE_64; } else { return VI_ATTR_MEM_BASE_32; } } }
    public static int VI_ATTR_MEM_SIZE { get { if (Is64bit) { return VI_ATTR_MEM_SIZE_64; } else { return VI_ATTR_MEM_SIZE_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR0 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR0_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR0_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR1 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR1_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR1_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR2 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR2_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR2_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR3 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR3_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR3_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR4 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR4_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR4_32; } } }
    public static int VI_ATTR_PXI_MEM_BASE_BAR5 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_BASE_BAR5_64; } else { return VI_ATTR_PXI_MEM_BASE_BAR5_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR0 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR0_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR0_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR1 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR1_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR1_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR2 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR2_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR2_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR3 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR3_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR3_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR4 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR4_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR4_32; } } }
    public static int VI_ATTR_PXI_MEM_SIZE_BAR5 { get { if (Is64bit) { return VI_ATTR_PXI_MEM_SIZE_BAR5_64; } else { return VI_ATTR_PXI_MEM_SIZE_BAR5_32; } } }
    #endregion - Bitness dependent attributes

    #region - Event Types -----------------------------------------------------------
    public const int VI_EVENT_IO_COMPLETION = 1073684489;
    public const int VI_EVENT_TRIG = -1073799158;
    public const int VI_EVENT_SERVICE_REQ = 1073684491;
    public const int VI_EVENT_CLEAR = 1073684493;
    public const int VI_EVENT_EXCEPTION = -1073799154;
    public const int VI_EVENT_GPIB_CIC = 1073684498;
    public const int VI_EVENT_GPIB_TALK = 1073684499;
    public const int VI_EVENT_GPIB_LISTEN = 1073684500;
    public const int VI_EVENT_VXI_VME_SYSFAIL = 1073684509;
    public const int VI_EVENT_VXI_VME_SYSRESET = 1073684510;
    public const int VI_EVENT_VXI_SIGP = 1073684512;
    public const int VI_EVENT_VXI_VME_INTR = -1073799135;
    public const int VI_EVENT_PXI_INTR = 1073684514;
    public const int VI_EVENT_TCPIP_CONNECT = 1073684534;
    public const int VI_EVENT_USB_INTR = 1073684535;
    public const int VI_ALL_ENABLED_EVENTS = 1073709055;
    #endregion

    #region - Completion and Error Codes --------------------------------------------
    public const int VI_SUCCESS = 0;
    public const int VI_SUCCESS_EVENT_EN = 1073676290;
    public const int VI_SUCCESS_EVENT_DIS = 1073676291;
    public const int VI_SUCCESS_QUEUE_EMPTY = 1073676292;
    public const int VI_SUCCESS_TERM_CHAR = 1073676293;
    public const int VI_SUCCESS_MAX_CNT = 1073676294;
    public const int VI_SUCCESS_DEV_NPRESENT = 1073676413;
    public const int VI_SUCCESS_TRIG_MAPPED = 1073676414;
    public const int VI_SUCCESS_QUEUE_NEMPTY = 1073676416;
    public const int VI_SUCCESS_NCHAIN = 1073676440;
    public const int VI_SUCCESS_NESTED_SHARED = 1073676441;
    public const int VI_SUCCESS_NESTED_EXCLUSIVE = 1073676442;
    public const int VI_SUCCESS_SYNC = 1073676443;
    public const int VI_WARN_QUEUE_OVERFLOW = 1073676300;
    public const int VI_WARN_CONFIG_NLOADED = 1073676407;
    public const int VI_WARN_NULL_OBJECT = 1073676418;
    public const int VI_WARN_NSUP_ATTR_STATE = 1073676420;
    public const int VI_WARN_UNKNOWN_STATUS = 1073676421;
    public const int VI_WARN_NSUP_BUF = 1073676424;
    public const int VI_WARN_EXT_FUNC_NIMPL = 1073676457;
    public const int VI_ERROR_SYSTEM_ERROR = -1073807360;
    public const int VI_ERROR_INV_OBJECT = -1073807346;
    public const int VI_ERROR_RSRC_LOCKED = -1073807345;
    public const int VI_ERROR_INV_EXPR = -1073807344;
    public const int VI_ERROR_RSRC_NFOUND = -1073807343;
    public const int VI_ERROR_INV_RSRC_NAME = -1073807342;
    public const int VI_ERROR_INV_ACC_MODE = -1073807341;
    public const int VI_ERROR_TMO = -1073807339;
    public const int VI_ERROR_CLOSING_FAILED = -1073807338;
    public const int VI_ERROR_INV_DEGREE = -1073807333;
    public const int VI_ERROR_INV_JOB_ID = -1073807332;
    public const int VI_ERROR_NSUP_ATTR = -1073807331;
    public const int VI_ERROR_NSUP_ATTR_STATE = -1073807330;
    public const int VI_ERROR_ATTR_READONLY = -1073807329;
    public const int VI_ERROR_INV_LOCK_TYPE = -1073807328;
    public const int VI_ERROR_INV_ACCESS_KEY = -1073807327;
    public const int VI_ERROR_INV_EVENT = -1073807322;
    public const int VI_ERROR_INV_MECH = -1073807321;
    public const int VI_ERROR_HNDLR_NINSTALLED = -1073807320;
    public const int VI_ERROR_INV_HNDLR_REF = -1073807319;
    public const int VI_ERROR_INV_CONTEXT = -1073807318;
    public const int VI_ERROR_QUEUE_OVERFLOW = -1073807315;
    public const int VI_ERROR_NENABLED = -1073807313;
    public const int VI_ERROR_ABORT = -1073807312;
    public const int VI_ERROR_RAW_WR_PROT_VIOL = -1073807308;
    public const int VI_ERROR_RAW_RD_PROT_VIOL = -1073807307;
    public const int VI_ERROR_OUTP_PROT_VIOL = -1073807306;
    public const int VI_ERROR_INP_PROT_VIOL = -1073807305;
    public const int VI_ERROR_BERR = -1073807304;
    public const int VI_ERROR_IN_PROGRESS = -1073807303;
    public const int VI_ERROR_INV_SETUP = -1073807302;
    public const int VI_ERROR_QUEUE_ERROR = -1073807301;
    public const int VI_ERROR_ALLOC = -1073807300;
    public const int VI_ERROR_INV_MASK = -1073807299;
    public const int VI_ERROR_IO = -1073807298;
    public const int VI_ERROR_INV_FMT = -1073807297;
    public const int VI_ERROR_NSUP_FMT = -1073807295;
    public const int VI_ERROR_LINE_IN_USE = -1073807294;
    public const int VI_ERROR_NSUP_MODE = -1073807290;
    public const int VI_ERROR_SRQ_NOCCURRED = -1073807286;
    public const int VI_ERROR_INV_SPACE = -1073807282;
    public const int VI_ERROR_INV_OFFSET = -1073807279;
    public const int VI_ERROR_INV_WIDTH = -1073807278;
    public const int VI_ERROR_NSUP_OFFSET = -1073807276;
    public const int VI_ERROR_NSUP_VAR_WIDTH = -1073807275;
    public const int VI_ERROR_WINDOW_NMAPPED = -1073807273;
    public const int VI_ERROR_RESP_PENDING = -1073807271;
    public const int VI_ERROR_NLISTENERS = -1073807265;
    public const int VI_ERROR_NCIC = -1073807264;
    public const int VI_ERROR_NSYS_CNTLR = -1073807263;
    public const int VI_ERROR_NSUP_OPER = -1073807257;
    public const int VI_ERROR_INTR_PENDING = -1073807256;
    public const int VI_ERROR_ASRL_PARITY = -1073807254;
    public const int VI_ERROR_ASRL_FRAMING = -1073807253;
    public const int VI_ERROR_ASRL_OVERRUN = -1073807252;
    public const int VI_ERROR_TRIG_NMAPPED = -1073807250;
    public const int VI_ERROR_NSUP_ALIGN_OFFSET = -1073807248;
    public const int VI_ERROR_USER_BUF = -1073807247;
    public const int VI_ERROR_RSRC_BUSY = -1073807246;
    public const int VI_ERROR_NSUP_WIDTH = -1073807242;
    public const int VI_ERROR_INV_PARAMETER = -1073807240;
    public const int VI_ERROR_INV_PROT = -1073807239;
    public const int VI_ERROR_INV_SIZE = -1073807237;
    public const int VI_ERROR_WINDOW_MAPPED = -1073807232;
    public const int VI_ERROR_NIMPL_OPER = -1073807231;
    public const int VI_ERROR_INV_LENGTH = -1073807229;
    public const int VI_ERROR_INV_MODE = -1073807215;
    public const int VI_ERROR_SESN_NLOCKED = -1073807204;
    public const int VI_ERROR_MEM_NSHARED = -1073807203;
    public const int VI_ERROR_LIBRARY_NFOUND = -1073807202;
    public const int VI_ERROR_NSUP_INTR = -1073807201;
    public const int VI_ERROR_INV_LINE = -1073807200;
    public const int VI_ERROR_FILE_ACCESS = -1073807199;
    public const int VI_ERROR_FILE_IO = -1073807198;
    public const int VI_ERROR_NSUP_LINE = -1073807197;
    public const int VI_ERROR_NSUP_MECH = -1073807196;
    public const int VI_ERROR_INTF_NUM_NCONFIG = -1073807195;
    public const int VI_ERROR_CONN_LOST = -1073807194;
    public const int VI_ERROR_MACHINE_NAVAIL = -1073807193;
    public const int VI_ERROR_NPERMISSION = -1073807192;
    #endregion

    #region - Other VISA Definitions ------------------------------------------------
    public const short VI_FIND_BUFLEN = 256;

    public const short VI_NULL = 0;
    public const short VI_TRUE = 1;
    public const short VI_FALSE = 0;

    public const short VI_INTF_GPIB = 1;
    public const short VI_INTF_VXI = 2;
    public const short VI_INTF_GPIB_VXI = 3;
    public const short VI_INTF_ASRL = 4;
    public const short VI_INTF_PXI = 5;
    public const short VI_INTF_TCPIP = 6;
    public const short VI_INTF_USB = 7;

    public const short VI_PROT_NORMAL = 1;
    public const short VI_PROT_FDC = 2;
    public const short VI_PROT_HS488 = 3;
    public const short VI_PROT_4882_STRS = 4;
    public const short VI_PROT_USBTMC_VENDOR = 5;

    public const short VI_FDC_NORMAL = 1;
    public const short VI_FDC_STREAM = 2;

    public const short VI_LOCAL_SPACE = 0;
    public const short VI_A16_SPACE = 1;
    public const short VI_A24_SPACE = 2;
    public const short VI_A32_SPACE = 3;
    public const short VI_A64_SPACE = 4;
    public const short VI_PXI_ALLOC_SPACE = 9;
    public const short VI_PXI_CFG_SPACE = 10;
    public const short VI_PXI_BAR0_SPACE = 11;
    public const short VI_PXI_BAR1_SPACE = 12;
    public const short VI_PXI_BAR2_SPACE = 13;
    public const short VI_PXI_BAR3_SPACE = 14;
    public const short VI_PXI_BAR4_SPACE = 15;
    public const short VI_PXI_BAR5_SPACE = 16;
    public const short VI_OPAQUE_SPACE = -1;

    public const short VI_UNKNOWN_LA = -1;
    public const short VI_UNKNOWN_SLOT = -1;
    public const short VI_UNKNOWN_LEVEL = -1;
    public const short VI_UNKNOWN_CHASSIS = -1;

    public const short VI_QUEUE = 1;
    public const short VI_HNDLR = 2;
    public const short VI_SUSPEND_HNDLR = 4;
    public const short VI_ALL_MECH = -1;
    public const short VI_ANY_HNDLR = 0;

    public const short VI_TRIG_ALL = -2;
    public const short VI_TRIG_SW = -1;
    public const short VI_TRIG_TTL0 = 0;
    public const short VI_TRIG_TTL1 = 1;
    public const short VI_TRIG_TTL2 = 2;
    public const short VI_TRIG_TTL3 = 3;
    public const short VI_TRIG_TTL4 = 4;
    public const short VI_TRIG_TTL5 = 5;
    public const short VI_TRIG_TTL6 = 6;
    public const short VI_TRIG_TTL7 = 7;
    public const short VI_TRIG_TTL8 = 32;
    public const short VI_TRIG_TTL9 = 33;
    public const short VI_TRIG_TTL10 = 34;
    public const short VI_TRIG_TTL11 = 35;
    public const short VI_TRIG_ECL0 = 8;
    public const short VI_TRIG_ECL1 = 9;
    public const short VI_TRIG_PANEL_IN = 27;
    public const short VI_TRIG_PANEL_OUT = 28;

    public const short VI_TRIG_PROT_DEFAULT = 0;
    public const short VI_TRIG_PROT_ON = 1;
    public const short VI_TRIG_PROT_OFF = 2;
    public const short VI_TRIG_PROT_SYNC = 5;
    public const short VI_TRIG_PROT_RESERVE = 6;
    public const short VI_TRIG_PROT_UNRESERVE = 7;

    public const short VI_READ_BUF = 1;
    public const short VI_WRITE_BUF = 2;
    public const short VI_READ_BUF_DISCARD = 4;
    public const short VI_WRITE_BUF_DISCARD = 8;
    public const short VI_IO_IN_BUF = 16;
    public const short VI_IO_OUT_BUF = 32;
    public const short VI_IO_IN_BUF_DISCARD = 64;
    public const short VI_IO_OUT_BUF_DISCARD = 128;

    public const short VI_FLUSH_ON_ACCESS = 1;
    public const short VI_FLUSH_WHEN_FULL = 2;
    public const short VI_FLUSH_DISABLE = 3;

    public const short VI_NMAPPED = 1;
    public const short VI_USE_OPERS = 2;
    public const short VI_DEREF_ADDR = 3;
    public const short VI_DEREF_ADDR_BYTE_SWAP = 4;

    public const int VI_TMO_IMMEDIATE = 0;
    public const int VI_TMO_INFINITE = -1;

    public const short VI_NO_LOCK = 0;
    public const short VI_EXCLUSIVE_LOCK = 1;
    public const short VI_SHARED_LOCK = 2;
    public const short VI_LOAD_CONFIG = 4;

    public const short VI_NO_SEC_ADDR = -1;

    public const short VI_ASRL_PAR_NONE = 0;
    public const short VI_ASRL_PAR_ODD = 1;
    public const short VI_ASRL_PAR_EVEN = 2;
    public const short VI_ASRL_PAR_MARK = 3;
    public const short VI_ASRL_PAR_SPACE = 4;

    public const short VI_ASRL_STOP_ONE = 10;
    public const short VI_ASRL_STOP_ONE5 = 15;
    public const short VI_ASRL_STOP_TWO = 20;

    public const short VI_ASRL_FLOW_NONE = 0;
    public const short VI_ASRL_FLOW_XON_XOFF = 1;
    public const short VI_ASRL_FLOW_RTS_CTS = 2;
    public const short VI_ASRL_FLOW_DTR_DSR = 4;

    public const short VI_ASRL_END_NONE = 0;
    public const short VI_ASRL_END_LAST_BIT = 1;
    public const short VI_ASRL_END_TERMCHAR = 2;
    public const short VI_ASRL_END_BREAK = 3;

    public const short VI_STATE_ASSERTED = 1;
    public const short VI_STATE_UNASSERTED = 0;
    public const short VI_STATE_UNKNOWN = -1;

    public const short VI_BIG_ENDIAN = 0;
    public const short VI_LITTLE_ENDIAN = 1;

    public const short VI_DATA_PRIV = 0;
    public const short VI_DATA_NPRIV = 1;
    public const short VI_PROG_PRIV = 2;
    public const short VI_PROG_NPRIV = 3;
    public const short VI_BLCK_PRIV = 4;
    public const short VI_BLCK_NPRIV = 5;
    public const short VI_D64_PRIV = 6;
    public const short VI_D64_NPRIV = 7;

    public const short VI_WIDTH_8 = 1;
    public const short VI_WIDTH_16 = 2;
    public const short VI_WIDTH_32 = 4;
    public const short VI_WIDTH_64 = 8;

    public const short VI_GPIB_REN_DEASSERT = 0;
    public const short VI_GPIB_REN_ASSERT = 1;
    public const short VI_GPIB_REN_DEASSERT_GTL = 2;
    public const short VI_GPIB_REN_ASSERT_ADDRESS = 3;
    public const short VI_GPIB_REN_ASSERT_LLO = 4;
    public const short VI_GPIB_REN_ASSERT_ADDRESS_LLO = 5;
    public const short VI_GPIB_REN_ADDRESS_GTL = 6;

    public const short VI_GPIB_ATN_DEASSERT = 0;
    public const short VI_GPIB_ATN_ASSERT = 1;
    public const short VI_GPIB_ATN_DEASSERT_HANDSHAKE = 2;
    public const short VI_GPIB_ATN_ASSERT_IMMEDIATE = 3;

    public const short VI_GPIB_HS488_DISABLED = 0;
    public const short VI_GPIB_HS488_NIMPL = -1;

    public const short VI_GPIB_UNADDRESSED = 0;
    public const short VI_GPIB_TALKER = 1;
    public const short VI_GPIB_LISTENER = 2;

    public const short VI_VXI_CMD16 = 512;
    public const short VI_VXI_CMD16_RESP16 = 514;
    public const short VI_VXI_RESP16 = 2;
    public const short VI_VXI_CMD32 = 1024;
    public const short VI_VXI_CMD32_RESP16 = 1026;
    public const short VI_VXI_CMD32_RESP32 = 1028;
    public const short VI_VXI_RESP32 = 4;

    public const short VI_ASSERT_SIGNAL = -1;
    public const short VI_ASSERT_USE_ASSIGNED = 0;
    public const short VI_ASSERT_IRQ1 = 1;
    public const short VI_ASSERT_IRQ2 = 2;
    public const short VI_ASSERT_IRQ3 = 3;
    public const short VI_ASSERT_IRQ4 = 4;
    public const short VI_ASSERT_IRQ5 = 5;
    public const short VI_ASSERT_IRQ6 = 6;
    public const short VI_ASSERT_IRQ7 = 7;

    public const short VI_UTIL_ASSERT_SYSRESET = 1;
    public const short VI_UTIL_ASSERT_SYSFAIL = 2;
    public const short VI_UTIL_DEASSERT_SYSFAIL = 3;

    public const short VI_VXI_CLASS_MEMORY = 0;
    public const short VI_VXI_CLASS_EXTENDED = 1;
    public const short VI_VXI_CLASS_MESSAGE = 2;
    public const short VI_VXI_CLASS_REGISTER = 3;
    public const short VI_VXI_CLASS_OTHER = 4;
    public const short VI_PXI_ADDR_NONE = 0;
    public const short VI_PXI_ADDR_MEM = 1;
    public const short VI_PXI_ADDR_IO = 2;
    public const short VI_PXI_ADDR_CFG = 3;

    public const short VI_TRIG_UNKNOWN = -1;
    public const short VI_PXI_LBUS_UNKNOWN = -1;
    public const short VI_PXI_LBUS_NONE = 0;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_0 = 1000;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_1 = 1001;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_2 = 1002;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_3 = 1003;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_4 = 1004;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_5 = 1005;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_6 = 1006;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_7 = 1007;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_8 = 1008;
    public const short VI_PXI_LBUS_STAR_TRIG_BUS_9 = 1009;
    public const short VI_PXI_STAR_TRIG_CONTROLLER = 1413;

    #endregion

    #region - Backward Compatibility Macros -----------------------------------------
    public const int VI_ERROR_INV_SESSION = -1073807346;
    public const int VI_INFINITE = -1;
    public const short VI_NORMAL = 1;
    public const short VI_FDC = 2;
    public const short VI_HS488 = 3;
    public const short VI_ASRL488 = 4;
    public const short VI_ASRL_IN_BUF = 16;
    public const short VI_ASRL_OUT_BUF = 32;
    public const short VI_ASRL_IN_BUF_DISCARD = 64;
    public const short VI_ASRL_OUT_BUF_DISCARD = 128;
    #endregion

    #endregion
}

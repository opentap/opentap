//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OpenTap.Package.SetAsmInfo
{
    internal enum UpdateMethod
    {
        ILDasm,
        Mono
    }

    internal class SetAsmInfo
    {
        class VERSION_INFO_CHUNK
        {
            public UInt16 wType;

            public string Key;
            public byte[] Value;
            public string ValueText;
            public List<VERSION_INFO_CHUNK> Children;

            public byte[] GetData()
            {
                if (wType == 1)
                    if (ValueText != null)
                        Value = System.Text.Encoding.Unicode.GetBytes(ValueText + '\0');

                using (var ms = new MemoryStream())
                {
                    ms.Write(BitConverter.GetBytes((UInt16)0), 0, 2);
                    if (wType == 1)
                        ms.Write(BitConverter.GetBytes((UInt16)(Value.Length / 2)), 0, 2);
                    else
                        ms.Write(BitConverter.GetBytes((UInt16)Value.Length), 0, 2);

                    ms.Write(BitConverter.GetBytes((UInt16)wType), 0, 2);

                    var bytes = System.Text.Encoding.Unicode.GetBytes(Key + '\0');
                    ms.Write(bytes, 0, bytes.Length);
                    Align(ms);

                    ms.Write(Value, 0, Value.Length);
                    if (wType != 1)
                        Align(ms);

                    foreach (var ch in Children)
                    {
                        Align(ms);

                        var data = ch.GetData();
                        ms.Write(data, 0, data.Length);
                    }

                    ms.Seek(0, 0);
                    ms.Write(BitConverter.GetBytes((UInt16)ms.Length), 0, 2);

                    return ms.ToArray();
                }
            }

            private static void Align(Stream st)
            {
                int left = (4 - (int)st.Length) & 3;

                for (int i = 0; i < left; i++)
                    st.WriteByte(0);
            }

            public VERSION_INFO_CHUNK(byte[] data)
            {
                var wLength = BitConverter.ToUInt16(data, 0);
                var wValueLength = BitConverter.ToUInt16(data, 2);
                wType = BitConverter.ToUInt16(data, 4);

                if (wType == 1)
                    wValueLength *= 2;

                int end = -1;
                for (int i = 6; i < data.Length; i += 2)
                {
                    if ((data[i] == 0) && (data[i + 1] == 0))
                    {
                        end = i + 2;
                        break;
                    }
                }

                Key = System.Text.Encoding.Unicode.GetString(data, 6, end - 6 - 2);
                Align(ref end);

                Value = new byte[wValueLength];
                Array.Copy(data, end, Value, 0, wValueLength);
                end = end + wValueLength;
                Align(ref end);

                if (wType == 1)
                {
                    if (Value.Length >= 2)
                        ValueText = System.Text.Encoding.Unicode.GetString(Value, 0, Value.Length - 2);
                    else
                        ValueText = null;
                }

                Children = new List<VERSION_INFO_CHUNK>();

                while (end < (data.Length - 1))
                {
                    var left = data.Length - end;
                    var subLen = BitConverter.ToUInt16(data, end);

                    if (subLen == 0)
                        break;
                    else if (subLen <= left)
                        Children.Add(new VERSION_INFO_CHUNK(data.Skip(end).Take(subLen).ToArray()));
                    else
                        throw new Exception("Invalid size");

                    end += subLen;
                    Align(ref end);
                }
            }

            private static void Align(ref int end)
            {
                end = (end + 3) & ~3;
            }

            public override string ToString()
            {
                return Key;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct VS_FIXEDFILEINFO
        {
            public UInt32 dwSignature;
            public UInt32 dwStrucVersion;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 4)]
            public UInt16[] dwFileVersion;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2, SizeConst = 4)]
            public UInt16[] dwProductVersion;

            public UInt32 dwFileFlagsMask;
            public UInt32 dwFileFlags;
            public UInt32 dwFileOS;
            public UInt32 dwFileType;
            public UInt32 dwFileSubtype;
            public UInt32 dwFileDateMS;
            public UInt32 dwFileDateLS;
        }

        class Win32Resource
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr FreeLibrary(IntPtr lpModuleName);
            
            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr FindResource(IntPtr hModule, int lpType, int lpName);

            [DllImport("Kernel32.dll", EntryPoint = "SizeofResource", SetLastError = true)]
            private static extern uint SizeofResource(IntPtr hModule, IntPtr hResource);

            [DllImport("Kernel32.dll", EntryPoint = "LoadResource", SetLastError = true)]
            private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResource);

            [DllImport("Kernel32.dll", EntryPoint = "LockResource", SetLastError = true)]
            private static extern IntPtr LockResource(IntPtr hResource);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)]bool bDeleteExistingResources);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool UpdateResource(IntPtr hUpdate, int lpType, int lpName, ushort wLanguage, IntPtr lpData, uint cbData);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

            private static UInt16 MAKELANGID(UInt16 PrimaryLang, UInt16 SubLang)
            {
                return (UInt16)((SubLang << 10) | PrimaryLang);
            }

            [System.Flags]
            enum LoadLibraryFlags : uint
            {
                DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
                LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
                LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
                LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
                LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
                LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
                LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
                LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
                LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
                LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
            }

            private const int LANGUAGE_NEUTRAL = 1;

            private const int VS_VERSION_INFO = 1;

            private const int LANG_NEUTRAL = 0;

            private const int SUBLANG_NEUTRAL = 0;
            private const int SUBLANG_DEFAULT = 1;
            private const int SUBLANG_SYS_DEFAULT = 2;

            private const int RT_VERSION = 16;

            public static byte[] ReadVersionResource(string libname)
            {
                var hModule = LoadLibraryEx(libname, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE | LoadLibraryFlags.DONT_RESOLVE_DLL_REFERENCES);
                if (hModule == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception();

                try
                {
                    IntPtr loc = FindResource(hModule, VS_VERSION_INFO, RT_VERSION);
                    if (loc == IntPtr.Zero)
                        throw new System.ComponentModel.Win32Exception("Could not find version information.");

                    uint size = SizeofResource(hModule, loc);
                    IntPtr hResource = LoadResource(hModule, loc);
                    if (hResource == IntPtr.Zero)
                        throw new System.ComponentModel.Win32Exception("Could not load version information.");

                    IntPtr x = LockResource(hResource);
                    if (x == IntPtr.Zero)
                        throw new System.ComponentModel.Win32Exception("Could not get address of version information.");

                    var dest = new byte[size];
                    Marshal.Copy(x, dest, 0, (int)size);

                    return dest;
                }
                finally
                {
                    FreeLibrary(hModule);
                }
            }

            internal static void WriteVersionResource(string filename, byte[] data)
            {
                IntPtr x = Marshal.AllocHGlobal(data.Length);
                try
                {
                    var hResource = BeginUpdateResource(filename, false);
                    if (hResource == IntPtr.Zero)
                        throw new System.ComponentModel.Win32Exception("Could not begin the version information update procedure.");

                    Marshal.Copy(data, 0, x, data.Length);

                    if (!UpdateResource(hResource, RT_VERSION, VS_VERSION_INFO, MAKELANGID(LANG_NEUTRAL, SUBLANG_NEUTRAL), x, (uint)data.Length))
                        throw new System.ComponentModel.Win32Exception();

                    if (!EndUpdateResource(hResource, false))
                        throw new System.ComponentModel.Win32Exception();
                }
                finally
                {
                    Marshal.FreeHGlobal(x);
                }
            }
        }

        private static byte ParseHex(string data)
        {
            return Convert.ToByte(data, 16);
        }

        private static string ParseString(byte[] dataBytes)
        {
            if (dataBytes.Length < 5) throw new Exception("Invalid string while parsing assembly attribute.");

            if (BitConverter.ToUInt16(dataBytes, 0) != 1) throw new Exception("Invalid string while parsing assembly attribute.");

            int pos = 2;
            Func<byte> ReadByte = () =>
            {
                return dataBytes[pos++];
            };

            // Read compressed uint32
            Func<uint> ReadCompressed = () =>
            {
                byte first = ReadByte();
                if ((first & 0x80) == 0)
                    return first;

                if ((first & 0x40) == 0)
                    return ((uint)(first & ~0x80) << 8)
                        | ReadByte();

                return ((uint)(first & ~0xc0) << 24)
                    | (uint)ReadByte() << 16
                    | (uint)ReadByte() << 8
                    | ReadByte();
            };

            var len = ReadCompressed();

            if (len == 0) return "";

            return System.Text.Encoding.UTF8.GetString(dataBytes, pos, (int)len);
        }

        private static string EncodeStr(string data)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(data);

            var ms = new MemoryStream();

            Action<byte> WriteByte = ms.WriteByte;

            Action<UInt32> WriteCompressedUInt32 = value =>
            {
                if (value < 0x80)
                    WriteByte((byte)value);
                else if (value < 0x4000)
                {
                    WriteByte((byte)(0x80 | (value >> 8)));
                    WriteByte((byte)(value & 0xff));
                }
                else
                {
                    WriteByte((byte)((value >> 24) | 0xc0));
                    WriteByte((byte)((value >> 16) & 0xff));
                    WriteByte((byte)((value >> 8) & 0xff));
                    WriteByte((byte)(value & 0xff));
                }
            };

            Action<UInt16> WriteUInt16 = value =>
            {
                WriteByte((byte)value);
                WriteByte((byte)(value >> 8));
            };

            WriteUInt16(1); // Start indicator
            WriteCompressedUInt32((UInt32)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
            WriteUInt16(0); // End indicator

            return string.Join(" ", ms.ToArray().Select(x => x.ToString("X2")));
        }

        private static string ReplaceAttribute(string data, string attribute, Func<string, string> replace)
        {
            Regex replaceAttributeRegex = new Regex(@"(?<name>.custom[\s]+instance[\s]+void[\s]+[^=]*" + attribute + @"[^.]*\.ctor\(string\)[^(]*)(?<content>\(((?<data>[^)/]*)?(//[^\n]*\n?)?)*\)[\s]*(//[^\n]*)?)", RegexOptions.Multiline);

            data = replaceAttributeRegex.Replace(data, match =>
            {
                var dataBytes = match.Groups["data"].Captures.OfType<Capture>().SelectMany(x => x.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseHex)).ToArray();

                return string.Format("{0} ({1})", match.Groups["name"].Value, EncodeStr(replace(ParseString(dataBytes))));
            }, 1);

            return data;
        }

        static Regex r = new Regex(@"(?<data>.assembly\s*'[^{]*\{((?<BR>\{)|(?<-BR>\})|[^{}]*)+.ver\s*)(?<version>[^\r\n]*)", RegexOptions.Compiled | RegexOptions.Multiline);

        private static string ReplaceAssemblyVersion(string data, Version version)
        {
            return r.Replace(data, ma =>
            {
                return ma.Groups["data"].Value + string.Format("{0}:{1}:{2}:{3}", version.Major, version.Minor, version.Build, version.Revision);
            }, 1);
        }

        public static void SetInfo(string filename, Version version, Version fileVersion, SemanticVersion infoVersion, UpdateMethod updateMethod = UpdateMethod.ILDasm)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var data = Win32Resource.ReadVersionResource(filename);

                    var versionInfo = new VERSION_INFO_CHUNK(data);

                    // Update fixed file info
                    var info = CopyFrom<VS_FIXEDFILEINFO>(versionInfo.Value);

                    if (fileVersion != null)
                    {
                        EncodeVersion(ref info.dwFileVersion, fileVersion);
                        EncodeVersion(ref info.dwProductVersion, fileVersion);

                        versionInfo.Value = CopyTo(info);
                    }

                    var trl = versionInfo.Children.First(c => c.Key == "VarFileInfo").Children.First(c => c.Key == "Translation");
                    var translation = BitConverter.ToUInt16(trl.Value, 0);

                    foreach (var sfi in versionInfo.Children.Where(c => c.Key == "StringFileInfo"))
                    {
                        foreach (var table in sfi.Children)
                        {
                            foreach (var str in table.Children)
                            {
                                switch (str.Key)
                                {
                                    case "FileVersion":
                                        if (fileVersion != null)
                                            str.ValueText = fileVersion.ToString(3);
                                        break;
                                    case "ProductVersion":
                                        if (infoVersion != null)
                                            str.ValueText = infoVersion.ToString();
                                        else if (fileVersion != null)
                                            str.ValueText = fileVersion.ToString(3);
                                        break;
                                }
                            }
                        }
                    }

                    data = versionInfo.GetData();

                    Win32Resource.WriteVersionResource(filename, data);
                }
                catch (Exception ex)
                {
                    throw new Exception("Error while updating version information.", ex);
                }
            }

            if (updateMethod == UpdateMethod.ILDasm)
            {
                var isDLL = (Path.GetExtension(filename) ?? "").Equals(".dll", StringComparison.InvariantCultureIgnoreCase);

                var tmpIL = Path.GetTempFileName();

                if (!IL.Disassemble(filename, tmpIL))
                    throw new Exception(string.Format("Failed to disassemble input DLL/EXE {0}.", filename));

                var data = File.ReadAllText(tmpIL);

                data = ReplaceAssemblyVersion(data, version);

                if (fileVersion != null)
                    data = ReplaceAttribute(data, "AssemblyFileVersionAttribute", x => fileVersion.ToString());

                if (infoVersion != null)
                    data = ReplaceAttribute(data, "AssemblyInformationalVersionAttribute", attrValue => infoVersion.ToString());

                File.WriteAllText(tmpIL, data);

                var tmpDest = Path.GetTempFileName();

                if (!IL.Assemble(tmpIL, tmpDest, isDLL, Path.ChangeExtension(tmpIL, "res")))
                    throw new Exception(string.Format("Failed to assemble IL code."));

                Utils.FileCopy(tmpDest, filename);

                File.Delete(tmpDest);
                File.Delete(tmpIL);
            }
            else
            {
                var resolver = new TapMonoResolver();
                var asm = AssemblyDefinition.ReadAssembly(filename, new ReaderParameters { AssemblyResolver = resolver, InMemory = true, ReadingMode = ReadingMode.Immediate });

                if (version != null)
                    asm.Name.Version = version;

                foreach (var attr in asm.CustomAttributes)
                {
                    if (fileVersion != null && attr.AttributeType.FullName == typeof(AssemblyFileVersionAttribute).FullName)
                        attr.ConstructorArguments[0] = new CustomAttributeArgument(attr.ConstructorArguments[0].Type, fileVersion.ToString());

                    if (infoVersion != null && attr.AttributeType.FullName == typeof(AssemblyInformationalVersionAttribute).FullName)
                        attr.ConstructorArguments[0] = new CustomAttributeArgument(attr.ConstructorArguments[0].Type, infoVersion.ToString());
                }

                asm.Write(filename);
            }
        }

        private static T CopyFrom<T>(byte[] data)
        {
            var size = Marshal.SizeOf<T>();

            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(data, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static byte[] CopyTo<T>(T x)
        {
            var size = Marshal.SizeOf<T>();

            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr<T>(x, ptr, false);
                var data = new byte[size];
                Marshal.Copy(ptr, data, 0, size);
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static void EncodeVersion(ref UInt16[] dwVersion, Version fileVersion)
        {
            dwVersion[1] = (UInt16)fileVersion.Major;
            dwVersion[0] = (UInt16)fileVersion.Minor;
            dwVersion[3] = (UInt16)fileVersion.Build;
        }

        private class TapMonoResolver : BaseAssemblyResolver
        {
            ILookup<string, AssemblyData> searchedAssemblies = PluginManager.GetSearchedAssemblies().ToLookup(asm => asm.Name);

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                var subset = searchedAssemblies[name.Name];

                var found = subset.FirstOrDefault(asm => asm.Version == name.Version) ?? subset.FirstOrDefault(asm => OpenTap.Utils.Compatible(asm.Version, name.Version));

                ReaderParameters customParameters = new ReaderParameters() { AssemblyResolver = new TapMonoResolver() };

                if (found == null) // Try find dependency from already loaded assemblies
                {
                    var neededAssembly = new AssemblyName(name.ToString());
                    var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(s => s.GetName().Name == neededAssembly.Name);
                    if (loadedAssembly != null)
                        return AssemblyDefinition.ReadAssembly(loadedAssembly.Location, customParameters);
                }

                if (found != null)
                    return AssemblyDefinition.ReadAssembly(found.Location, customParameters);
                else
                    return base.Resolve(name, parameters);
            }
        }
    }
}

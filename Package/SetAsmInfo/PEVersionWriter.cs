using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace OpenTap.Package.SetAsmInfo
{
    static class CONSTANTS
    {
        public const uint onlyHighBit = 0x80000000;
        public const uint allButHighBit = ~onlyHighBit;

    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct _IMAGE_FILE_HEADER
    {
        public UInt16 Machine;
        public UInt16 NumberOfSections;
        public UInt32 TimeDateStamp;
        public UInt32 PointerToSymbolTable;
        public UInt32 NumberOfSymbols;
        public UInt16 SizeOfOptionalHeader;
        public UInt16 Characteristics;
    }



    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct RESOURCE_DIRECTORY_TABLE
    {
        public int Characteristics;
        public int TimeStamp;
        public short Major;
        public short Minor;
        public short NumberOfNameEntries;
        public short NumberOfIdEntries;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct RESOURCE_DIRECTORY_ENTRY
    {

        [FieldOffset(0)] public uint NameOffset;
        [FieldOffset(0)] public uint IntegerId;
        [FieldOffset(4)] public uint DataEntryOffset;
        [FieldOffset(4)] public uint SubdirectoryOffset;

        public bool IsSubdirectory()
        {
            return (SubdirectoryOffset & CONSTANTS.onlyHighBit) != 1;
        }

        public uint GetAddress()
        {

            if (IsSubdirectory())
                return SubdirectoryOffset & CONSTANTS.allButHighBit;
            return DataEntryOffset;
        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct _SECTION_HEADER
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Name;

        public int VirtualSize;
        public int VirtualAddress;
        public int SizeOfRawData;
        public int PointerToRawData;
        public int PointerToRelocations;
        public int PointerToLinenumbers;
        public short NumberOfRelocations;
        public short NumberOfLinenumbers;
        public int Characteristics;
    }

    internal class PEVersionWriter
    {
        private static T GetStruct<T>(byte[] buf, uint offset) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var buf2 = new byte[size];
            Array.Copy(buf, offset, buf2, 0, size);
            var handle = GCHandle.Alloc(buf2, GCHandleType.Pinned);
            var result = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            handle.Free();
            return result;
        }



        private static TraceSource log = Log.CreateSource(nameof(PEVersionWriter));

        private const string ProductVersion = "ProductVersion";
        private const string FileVersion = "FileVersion";
        private static void SetResourceVersion(byte[] file, _SECTION_HEADER section, SemanticVersion infoVersion, Version fileVersion)
        {
            var resourceTable = new byte[section.SizeOfRawData];
            Array.Copy(file, section.PointerToRawData, resourceTable, 0, section.SizeOfRawData);

            var table = GetStruct<RESOURCE_DIRECTORY_TABLE>(resourceTable, 0);
            var entry = GetStruct<RESOURCE_DIRECTORY_ENTRY>(resourceTable, (uint)Marshal.SizeOf<RESOURCE_DIRECTORY_TABLE>());

            while (entry.IsSubdirectory() && table.NumberOfIdEntries > 0)
            {
                var next = entry.GetAddress();
                table = GetStruct<RESOURCE_DIRECTORY_TABLE>(resourceTable, next);
                entry = GetStruct<RESOURCE_DIRECTORY_ENTRY>(resourceTable, next + (uint)Marshal.SizeOf<RESOURCE_DIRECTORY_TABLE>());
            }


            var rsrcString = Encoding.Unicode.GetString(resourceTable);


            var str = Encoding.Unicode.GetString(resourceTable);

            var fileVersionOffset = str.IndexOf(FileVersion) * 2;
            var productVersionOffset = str.IndexOf(ProductVersion) * 2;


            var fileVersion1 = new byte[100];
            Array.Copy(file,  section.PointerToRawData + fileVersionOffset, fileVersion1, 0, 100);
            var fileVersion1String = Encoding.Unicode.GetString(fileVersion1);

            Encoding.Unicode.GetString(file.Skip(section.PointerToRawData + fileVersionOffset).Take(100).ToArray());

            void InsertUnicode(int startIndex, byte[] payload)
            {
                while (file[startIndex] == '\0') startIndex++;
                int i1 = 0;
                for (int i = 0; i < payload.Length; i++)
                {
                    file[startIndex++] = payload[i];
                }

                while (file[startIndex] != 0)
                    file[startIndex++] = 0;

            }

            { // Insert file version
                if (fileVersion != null)
                {
                    var startIndex = section.PointerToRawData + fileVersionOffset + (FileVersion.Length * 2);
                    InsertUnicode(startIndex, Encoding.Unicode.GetBytes(fileVersion.ToString(3)));
                }
            }
            { // Insert product version
                var bytes = infoVersion == null
                        ? fileVersion == null ? null : Encoding.Unicode.GetBytes(fileVersion.ToString(3))
                        : Encoding.Unicode.GetBytes(infoVersion.ToString());
                if (bytes != null)
                {
                    var startIndex = section.PointerToRawData + productVersionOffset + (ProductVersion.Length * 2);
                    InsertUnicode(startIndex, bytes);
                }
            }
        }
        public static void SetVersionInfo(string filename, SemanticVersion infoVersion, Version fileVersion)
        {
            var bytes = File.ReadAllBytes(filename);
            var offset = BitConverter.ToUInt32(bytes, 0x3c);
            var res = Encoding.ASCII.GetString(bytes.Skip((int)offset).Take(4).ToArray());
            if (res != "PE\0\0")
                throw new Exception($"Provided file is not a PE file.");

            var headerStart = offset + 4;

            var coff_header = GetStruct<_IMAGE_FILE_HEADER>(bytes, headerStart);


            if (coff_header.Machine == 0x14c)
                log.Debug($"This is a 32 bit assembly");
            else if (coff_header.Machine == 0x8664)
                log.Debug($"This is a 64 bit assembly");

            var optionalHeaderStart = headerStart + 20;

            var optionalHeader = bytes.Skip((int)optionalHeaderStart).Take(coff_header.SizeOfOptionalHeader).ToArray();
            var magic = BitConverter.ToInt16(optionalHeader, 0);

            bool isPe32;

            if (magic == 0x10b)
            {
                log.Debug($"Format is PE32");
                isPe32 = true;
            }
            else if (magic == 0x20b)
            {
                log.Debug($"Format is PE32+");
                isPe32 = false;
            }
            else
            {
                throw new Exception($"Error parsing file.");
            }

            var resourceTable = BitConverter.ToInt64(optionalHeader, isPe32 ? 112 : 128);

            var sectionOffset = optionalHeaderStart + coff_header.SizeOfOptionalHeader + 2;

            log.Debug($"Assembly has {coff_header.NumberOfSections} sections.");


            for (int i = 0; i < coff_header.NumberOfSections; i++)
            {
                uint thisSectionOffset = (uint)(optionalHeaderStart + coff_header.SizeOfOptionalHeader + (i * 40));
                var sec = GetStruct<_SECTION_HEADER>(bytes, thisSectionOffset);
                var sectionName = Encoding.ASCII.GetString(sec.Name);

                if (sectionName.StartsWith(".rsrc"))
                {
                    SetResourceVersion(bytes, sec, infoVersion, fileVersion);
                    File.WriteAllBytes(filename, bytes);
                    return;
                }

            }

            throw new Exception($"Could not find resource section.");
        }
    }
}

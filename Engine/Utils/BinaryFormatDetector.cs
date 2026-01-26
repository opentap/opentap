using System;
using System.IO;

namespace OpenTap.Utilities;

internal enum ExecutableFormat
{
    Unknown,

    // Executables / binaries
    MZ,         // DOS MZ header (may or may not be a PE)
    PE,         // Windows PE (EXE / DLL, including .NET)
    DotNet,
    ELF,        // Linux
    MachO,      // macOS
    MachOFat,   // macOS Universal Binary
}


internal static class BinaryFormatDetector
{
    public static ExecutableFormat Detect(FileStream fs, string filePath)
    {
        byte[] header = new byte[4];

        int read = fs.Read(header, 0, 4);
        if (read < 4)
            return ExecutableFormat.Unknown;

        // -------------------------
        // MZ / PE (Windows)
        // -------------------------
        if (header[0] == 'M' && header[1] == 'Z')
        {
            // Fast PE check: e_lfanew + "PE\0\0"
            if (fs.Seek(0x3C, SeekOrigin.Begin) == 0x3C && fs.Read(header, 0, 4) == 4)
            {
                int peOffset =
                    header[0] |
                    (header[1] << 8) |
                    (header[2] << 16) |
                    (header[3] << 24);
                if(fs.Seek(peOffset, SeekOrigin.Begin) == peOffset && fs.Read(header, 0, 4) == 4)
                {
                    if (header[0] == 'P' &&
                        header[1] == 'E' &&
                        header[2] == 0 &&
                        header[3] == 0)
                    {
                        if (IsDotNet(fs, peOffset))
                        {
                            return ExecutableFormat.DotNet;
                        }

                        return ExecutableFormat.PE;
                    }
                }
            }

            return ExecutableFormat.MZ;
        }

        // -------------------------
        // ELF (Linux)
        // -------------------------
        if (header[0] == 0x7F &&
            header[1] == (byte)'E' &&
            header[2] == (byte)'L' &&
            header[3] == (byte)'F')
            return ExecutableFormat.ELF;

        // -------------------------
        // Mach-O (macOS)
        // -------------------------
        uint magic = ReadUInt32BE(header);

        switch (magic)
        {
            case 0xFEEDFACE:
            case 0xCEFAEDFE:
            case 0xFEEDFACF:
            case 0xCFFAEDFE:
                return ExecutableFormat.MachO;

            case 0xCAFEBABE:
            case 0xBEBAFECA:
                return ExecutableFormat.MachOFat;
        }

        return ExecutableFormat.Unknown;
    }
    
    private static bool IsDotNet(FileStream fs, int peHeaderOffset)
    {
        // 1. Determine if PE32 or PE32+ (64-bit)
        // Magic is at Start of Optional Header (peHeaderOffset + 24)
        fs.Seek(peHeaderOffset + 24, SeekOrigin.Begin);
        ushort magic = (ushort)(fs.ReadByte() | (fs.ReadByte() << 8));
        
        // 2. Locate the Data Directories
        // PE32 (0x10B) offset to Data Directories is 96 bytes from start of Optional Header
        // PE32+ (0x20B) offset is 112 bytes
        int dataDirOffset;
        if (magic == 0x10B) dataDirOffset = peHeaderOffset + 24 + 96;      // PE32
        else if (magic == 0x20B) dataDirOffset = peHeaderOffset + 24 + 112; // PE32+
        else return false;

        // 3. Check CLI Header (COM Descriptor Table)
        // It is the 15th directory entry (index 14)
        // Each entry is 8 bytes (4 VirtualAddress, 4 Size)
        int cliHeaderEntryOffset = dataDirOffset + (14 * 8);
        
        fs.Seek(cliHeaderEntryOffset, SeekOrigin.Begin);
        byte[] cliDir = new byte[8];
        if (fs.Read(cliDir, 0, 8) == 8)
        {
            uint address = BitConverter.ToUInt32(cliDir, 0);
            uint size = BitConverter.ToUInt32(cliDir, 4);

            // If the Address and Size are non-zero, it's a .NET assembly
            return address != 0 && size != 0;
        }

        return false;
    }

    private static uint ReadUInt32BE(ReadOnlySpan<byte> data)
    {
        return (uint)(
            (data[0] << 24) |
            (data[1] << 16) |
            (data[2] << 8) |
            data[3]);
    }
}

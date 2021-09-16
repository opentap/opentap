using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTap.Package.SetAsmInfo
{
    internal class PEVersionWriter
    {
        private static TraceSource log = Log.CreateSource(nameof(PEVersionWriter));

        private const string ProductVersion = "ProductVersion";
        private const string FileVersion = "FileVersion";
        private static void SetResourceVersion(byte[] file, int offset, SemanticVersion infoVersion, Version fileVersion)
        {
            var section = file.Skip(offset).Take(40).ToArray();
            var sizeOfSection = BitConverter.ToInt32(section, 16);
            var pointerToRawData = BitConverter.ToInt32(section, 20);

            var resourceTable = file.Skip(pointerToRawData).Take(sizeOfSection).ToArray();

            var rsrcString = Encoding.Unicode.GetString(resourceTable);

            var nNameEntries = BitConverter.ToInt16(resourceTable, 12);
            var nIdEntries = BitConverter.ToInt16(resourceTable, 14);

            var str = Encoding.Unicode.GetString(resourceTable);

            var fileVersionOffset = str.IndexOf(FileVersion) * 2;
            var productVersionOffset = str.IndexOf(ProductVersion) * 2;

            var size = BitConverter.ToInt16(resourceTable, fileVersionOffset - 2);


            var fileVersion1 = new byte[100];
            Array.Copy(file,  pointerToRawData + fileVersionOffset, fileVersion1, 0, 100);
            var fileVersion1String = Encoding.Unicode.GetString(fileVersion1);

            Encoding.Unicode.GetString(file.Skip(pointerToRawData + fileVersionOffset).Take(100).ToArray());

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

            // case "FileVersion":
            // if (fileVersion != null)
            //     str.ValueText = fileVersion.ToString(3);
            // break;
            // case "ProductVersion":
            // if (infoVersion != null)
            //     str.ValueText = infoVersion.ToString();
            // else if (fileVersion != null)
            //     str.ValueText = fileVersion.ToString(3);
            // break;
            //

            { // Insert file version
                if (fileVersion != null)
                {
                    var startIndex = pointerToRawData + fileVersionOffset + (FileVersion.Length * 2);
                    InsertUnicode(startIndex, Encoding.Unicode.GetBytes(fileVersion.ToString(3)));
                }
            }
            { // Insert product version
                var bytes = infoVersion == null
                        ? fileVersion == null ? null : Encoding.Unicode.GetBytes(fileVersion.ToString(3))
                        : Encoding.Unicode.GetBytes(infoVersion.ToString());
                if (bytes != null)
                {
                    var startIndex = pointerToRawData + productVersionOffset + (ProductVersion.Length * 2);
                    InsertUnicode(startIndex, bytes);
                }
            }
        }
        public static void SetVersionInfo(string filename, SemanticVersion infoVersion, Version fileVersion)
        {
            var bytes = File.ReadAllBytes(filename);
            var offset = BitConverter.ToInt32(bytes, 0x3c);
            var res = Encoding.ASCII.GetString(bytes.Skip(offset).Take(4).ToArray());
            if (res != "PE\0\0")
                throw new Exception($"Provided file is not a PE file.");

            var headerStart = offset + 4;

            short machine = BitConverter.ToInt16(bytes, headerStart);
            short numberOfSections = BitConverter.ToInt16(bytes, headerStart + 2);
            var sizeOfOptionalHeader = BitConverter.ToInt16(bytes, headerStart + 16);

            if (machine == 0x14c)
                log.Debug($"This is a 32 bit assembly");
            else if (machine == 0x8664)
                log.Debug($"This is a 64 bit assembly");

            var optionalHeaderStart = headerStart + 20;

            var optionalHeader = bytes.Skip(optionalHeaderStart).Take(sizeOfOptionalHeader).ToArray();
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

            var sectionOffset = optionalHeaderStart + sizeOfOptionalHeader + 2;

            log.Debug($"Assembly has {numberOfSections} sections.");


            for (int i = 0; i < numberOfSections; i++)
            {
                var thisSectionOffset = optionalHeaderStart + sizeOfOptionalHeader + (i * 40);
                var sectionName = Encoding.ASCII.GetString(bytes.Skip(thisSectionOffset).Take(5).ToArray());

                if (sectionName == ".rsrc")
                {
                    SetResourceVersion(bytes, thisSectionOffset, infoVersion, fileVersion);
                    File.WriteAllBytes(filename, bytes);
                    return;
                }

            }

            throw new Exception($"Could not find resource section.");
        }
    }
}

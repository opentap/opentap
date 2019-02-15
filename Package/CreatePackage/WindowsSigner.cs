//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package.CreatePackage
{
    [Display("Built-in SignTool runner")]
    public class WindowsSigner : IPackageSigner
    {
        private static string SigntoolPath = @"c:\Program Files (x86)\Microsoft SDKs\Windows\v7.1A\Bin\signtool.exe";

        public bool CanTransform(PackageDef package)
        {
            if (!File.Exists(SigntoolPath))
            {
                try
                {
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft SDKs");

                    var signtool = Directory.EnumerateFiles(path, "signtool.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (!String.IsNullOrEmpty(signtool))
                        SigntoolPath = signtool;
                }
                catch
                {
                    return false;
                }
            }

            return File.Exists(SigntoolPath);
        }


        public double GetOrder(PackageDef package)
        {
            return 0;
        }


        public bool Transform(string tempDir, PackageDef package)
        {
            if (!CanTransform(package))
                return false;


            foreach (PackageFile packageFile in package.Files)
            {
                if (string.IsNullOrEmpty(packageFile.Sign))
                    continue;

                var origFilename = Path.GetFileName(packageFile.RelativeDestinationPath);
                var newFilename = Path.Combine(tempDir, "Signed", origFilename);
                Directory.CreateDirectory(Path.GetDirectoryName(newFilename));

                int i = 1;
                while (File.Exists(newFilename)) { 
                    newFilename = Path.Combine(tempDir, "Signed", origFilename + i.ToString());
                    i++;
                }

                Utils.FileCopy(packageFile.FileName, newFilename);

                string certificateName = packageFile.Sign;
                var args = string.Format("sign /a /n \"{1}\" /t http://timestamp.verisign.com/scripts/timstamp.dll \"{0}\"", newFilename, certificateName);
                var ec = Utils.RunProgram(SigntoolPath, args);
                bool signed = ec == 0;

                if (signed)
                    packageFile.FileName = newFilename;
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}

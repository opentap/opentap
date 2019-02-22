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
using System.Xml.Serialization;

namespace OpenTap.Package.CreatePackage
{
    [Display("Sign")]
    public class SignData : ICustomPackageData
    {
        [XmlAttribute]
        public string Certificate { get; set; }
    }

    [Display("Built-in SignTool runner")]
    public class WindowsSigner : ICustomPackageAction
    {
        private static string SigntoolPath = @"c:\Program Files (x86)\Microsoft SDKs\Windows\v7.1A\Bin\signtool.exe";
        private static OpenTap.TraceSource log = Log.CreateSource("Sign");

        public PackageActionStage ActionStage => PackageActionStage.Create;

        private bool CanExecute()
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

        public bool Execute(PackageDef package, CustomPackageActionArgs customArguments)
        {
            if (!package.Files.Any(s => s.HasCustomData<SignData>()))
                return false;

            if (!CanExecute())
                throw new InvalidOperationException($"Unable to sign. Signtool not found.");


            foreach (PackageFile packageFile in package.Files)
            {
                if (!packageFile.HasCustomData<SignData>())
                    continue;

                SignData sign = packageFile.GetCustomData<SignData>();

                var origFilename = Path.GetFileName(packageFile.RelativeDestinationPath);
                var newFilename = Path.Combine(customArguments.TemporaryDirectory, "Signed", origFilename);
                Directory.CreateDirectory(Path.GetDirectoryName(newFilename));

                int i = 1;
                while (File.Exists(newFilename))
                {
                    newFilename = Path.Combine(customArguments.TemporaryDirectory, "Signed", origFilename + i.ToString());
                    i++;
                }

                ProgramHelper.FileCopy(packageFile.FileName, newFilename);

                string certificateName = sign.Certificate;
                var args = string.Format("sign /a /n \"{1}\" /t http://timestamp.verisign.com/scripts/timstamp.dll \"{0}\"", newFilename, certificateName);
                var ec = ProgramHelper.RunProgram(SigntoolPath, args);
                bool signed = ec == 0;

                if (signed)
                    packageFile.FileName = newFilename;
                else
                    throw new InvalidOperationException($"Unable to sign. Signtool returned exit code {ec}.");

                packageFile.RemoveCustomData<SignData>();
            }
            return true;
        }

        public int Order()
        {
            return 1000;
        }
    }
}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    class PackageDefLoadSaveCompare
    {
        /// <summary>
        /// Load the input as a PackageDef, saves it again and compares the saved output to the original input
        /// </summary>
        /// <param name="input"></param>
        private static void LoadSaveCompare(string input, string expectedOutput)
        {
            PackageDef pkg;
            byte[] inputArray = System.Text.Encoding.ASCII.GetBytes(input);
            using (MemoryStream inputStream = new MemoryStream(inputArray))
            {
                pkg = PackageDef.FromXml(inputStream);
            }
            string output = "";
            using (Stream str = new MemoryStream())
            {
                pkg.SaveTo(str);
                using (StreamReader reader = new StreamReader(str))
                {
                    reader.BaseStream.Seek(0, 0);
                    output = reader.ReadToEnd();
                }
            }
            XDocument inputDoc = XDocument.Parse(expectedOutput);
            XDocument outputDoc = XDocument.Parse(output);
            AssertElementEquals(inputDoc.Root, outputDoc.Root);
            AssertElementEquals(outputDoc.Root, inputDoc.Root);
        }

        private static void AssertElementEquals(XElement elm1, XElement elm2)
        {
            Assert.AreEqual(elm1.Attributes().Count(), elm2.Attributes().Count(), $"Number of attributes does not match on element {elm1.Name.LocalName}");
            foreach (XAttribute a1 in elm1.Attributes())
            {
                var a2 = elm2.Attribute(a1.Name);
                if (a2 == null)
                    Assert.Fail($"Attribute {a1.Name.LocalName} on element {elm1.Name.LocalName} does not exist in output.");
                if (a1.Value != a2.Value)
                    Assert.Fail($"Value of attribute {a1.Name.LocalName} on element {elm1.Name.LocalName} does not match. ('{a1.Value}' vs. '{a2.Value}')");
            }
            Assert.AreEqual(elm1.Elements().Count(), elm2.Elements().Count(), $"Number of child nodes does not match on element {elm1.Name.LocalName}");
            foreach (var n1 in elm1.Nodes())
            {
                if (n1 is XElement e1)
                {
                    XElement e2 = elm2.Element(e1.Name);
                    if (e2 == null)
                        Assert.Fail($"Output does not contain element {e1.Name.LocalName}.");
                    AssertElementEquals(e1, e2);
                }
                if (n1 is XText t1)
                {
                    Assert.IsTrue(elm2.Nodes().OfType<XText>().Any(t => t.Value == t1.Value), $"Text value '{t1.Value}' does not exist in output.");
                }
            }
        }

        /// <summary>
        /// Tests that OS attribute is kept even when empty (as it's default value is non empty ('Windows'))
        /// </summary>
        [Test]
        public void EmptyOS()
        {
            string input = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='' xmlns='http://opentap.io/schemas/package'>
    <Files>
        <File Path='OpenTap.dll'>
            <SetAssemblyInfo Attributes='Version'/>
        </File>
    </Files>
</Package>";
            LoadSaveCompare(input,input);
        }

        /// <summary>
        /// duplicate of the old PackageDefTests.SaveTo_Simple test
        /// </summary>
        [Test]
        public void Simple()
        {
            int retryCount = 10;
            while(retryCount-- > 0)
            {
                string input = @"<?xml version='1.0' encoding='utf-8'?>
    <Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='http://opentap.io/schemas/package'>
        <Description>Everything goes here<Status> tags</Status>.</Description>
        <Dependencies>
            <PackageDependency Package='DepName' Version='4.3.2'/>
        </Dependencies>
        <Files>
            <File Path='OpenTap.dll'>
                <SetAssemblyInfo Attributes='Version'/>
                <IgnoreDependency > System.Reflection.Metadata </IgnoreDependency>
            </File>
        </Files>
        <PackageActionExtensions>
            <ActionStep ActionName='install' Arguments='+x tap' ExeFile='chmod' ></ActionStep>
        </PackageActionExtensions>
    </Package>";
                LoadSaveCompare(input, input);
            }
        }

        /// <summary>
        /// Tests that the FileType attribute is removed if it has its default value 'tappackage'
        /// </summary>
        [Test]
        public void DefaultFileType()
        {
            string input = @"<?xml version='1.0' encoding='utf-8'?>
<Package FileType='tappackage' Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='http://opentap.io/schemas/package'>
</Package>";
            string output = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='http://opentap.io/schemas/package'>
</Package>";
            LoadSaveCompare(input, output);
        }

        /// <summary>
        /// Tests that the FileType attribute is removed if it has its default value 'tappackage'
        /// </summary>
        [Test]
        public void NoNamespace()
        {
            string input = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows'>
</Package>";
            string output = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='http://opentap.io/schemas/package'>
</Package>";
            LoadSaveCompare(input, output);
        }

        /// <summary>
        /// Tests that the FileType attribute is removed if it has its default value 'tappackage'
        /// </summary>
        [Test]
        public void WrongNamespace()
        {
            string input = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='wrong'>
</Package>";
            string output = @"<?xml version='1.0' encoding='utf-8'?>
<Package Name='Test' Version='1.2.3-alpha+test' Architecture='AnyCPU' OS='Windows' xmlns='http://opentap.io/schemas/package'>
</Package>";
            LoadSaveCompare(input, output);
        }
    }
}

using NUnit.Framework;
using System;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class FilePathTest
    {
        [Test]
        public void FilePathFilterValidationTest()
        {
            void validSyntax(string filter)
            {
                new FilePathAttribute(FilePathAttribute.BehaviorChoice.Open, filter);
            }

            void invalidSyntax(string filter)
            {
                try
                {
                    validSyntax(filter);
                    Assert.Fail("Should have thrown");
                }
                catch (FormatException)
                {

                }
            }

            validSyntax("");
            validSyntax("exe");
            validSyntax("Executable (*.exe)| *.exe");
            validSyntax("Text Document (*.txt) | *.txt | Executable (*.exe)| *.exe");
            invalidSyntax("*.txt");
            invalidSyntax("Executable (*.exe, *.dll)| *.exe, *.dll");
            validSyntax("Executable (*.exe, *.dll)| *.exe; *.dll");
            validSyntax("Executable (*.exe, *.dll)| *.exe; *.dll | Any File | *.*");
        }
    }
}

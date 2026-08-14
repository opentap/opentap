using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    /* Verify that package create conforms to the options documented at:
    *  https://doc.opentap.io/Developer%20Guide/Plugin%20Packaging%20and%20Versioning/Readme.html#wildcards
    */
    [TestFixture]
    public class PackageGlobTests
    {
        /* FromInputXml operates on the current working directory, but it is convenient for testing purposes to create a clean environment */
        static IDisposable CleanupDisposable(string workingDirectory)
        {
            var pwd = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(workingDirectory);
            return Utils.WithDisposable(() =>
            {
                Directory.SetCurrentDirectory(pwd);
                Directory.Delete(workingDirectory, true);
            });
        }
        static string PrepareFiles(params string[] files)
        {
            var basedir = Path.GetTempFileName();
            File.Delete(basedir);
            foreach (var file in files)
            {
                var path = Path.Combine(basedir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file);
            }
            return basedir;
        }

        static string PrepareXml(string basedir, params string[] files)
        {
            var xml = $"""
<?xml version="1.0" encoding="UTF-8"?>
<Package Name="GlobTest" Version="1.0.0" OS="Windows,Linux,MacOS" Architecture="AnyCPU">
  <Files>
""";
            foreach (var file in files)
            {
                xml += $"<File Path=\"{file}\" />";
            }


            xml += """
  </Files>
</Package>
""";
            var path = Path.Combine(basedir, "package.xml");
            File.WriteAllText(path, xml);
            return path;
        }

        // * Matches any number of any characters including none. Law* Law, Laws, or Lawyer
        [Test]
        public void TestStar()
        {
            var basedir = PrepareFiles("Law", "Laws", "Lawyer", "La- no match", "No match Lawyer", "No Match/Law");
            var xml = PrepareXml(basedir, "Law*");
            using var reset = CleanupDisposable(basedir);

            var pkg = PackageDefExt.FromInputXml(xml, basedir);
            Assert.That(pkg.Files.Count, Is.EqualTo(3));
            Assert.That(pkg.Files.Any(x => x.FileName == "Law"));
            Assert.That(pkg.Files.Any(x => x.FileName == "Laws"));
            Assert.That(pkg.Files.Any(x => x.FileName == "Lawyer"));
        }

        // ? Matches any single character. ?at Cat, cat, Bat or bat
        [Test]
        public void TestQuestionMark()
        {
            var basedir = PrepareFiles("cat", "bat", "No match cat");
            var xml = PrepareXml(basedir, "?at");
            using var reset = CleanupDisposable(basedir);

            var pkg = PackageDefExt.FromInputXml(xml, basedir);
            Assert.That(pkg.Files.Count, Is.EqualTo(2));
            Assert.That(pkg.Files.Any(x => x.FileName == "cat"));
            Assert.That(pkg.Files.Any(x => x.FileName == "bat"));

        }

        // ** Matches any number of path / directory segments. When used must be the only contents of a segment. /**/some.* /foo/bar/bah/some.txt, /some.txt, or /foo/some.txt.
        [Test]
        public void TestStarStar()
        {
            var basedir = PrepareFiles("some.txt", "foo/some.txt", "bar/baz/some.txt", "alpha/beta/something not matching.txt");
            var xml = PrepareXml(basedir, "**/some.*");
            using var reset = CleanupDisposable(basedir);

            var pkg = PackageDefExt.FromInputXml(xml, basedir);
            Assert.That(pkg.Files.Count, Is.EqualTo(3));
            Assert.That(pkg.Files.Any(x => x.FileName == "some.txt"));
            Assert.That(pkg.Files.Any(x => x.FileName == "foo/some.txt"));
            Assert.That(pkg.Files.Any(x => x.FileName == "bar/baz/some.txt"));
        }
    }
}


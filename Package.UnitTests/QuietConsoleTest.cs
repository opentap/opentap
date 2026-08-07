//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    [NonParallelizable]
    public class QuietConsoleTest
    {
        /// <summary>
        /// Adding a duplicate assembly of a different version (here Newtonsoft.Json) to the
        /// installation should produce a warning. Warnings are written to stderr, and with
        /// --quiet there should be NO messages other than those on stderr.
        /// </summary>
        [Test]
        public void DuplicateAssemblyWarningOnStderrAndQuietStdoutIsEmpty()
        {
            string installDir = ExecutorClient.ExeDir;
            string sourceAssembly = Path.Combine(installDir, "Newtonsoft.Json.dll");
            Assert.That(sourceAssembly, Does.Exist);

            string duplicateDir = Path.Combine(installDir, nameof(DuplicateAssemblyWarningOnStderrAndQuietStdoutIsEmpty));
            Directory.CreateDirectory(duplicateDir);
            try
            {
                // Create a duplicate Newtonsoft.Json with a different version.
                string duplicateAssembly = Path.Combine(duplicateDir, "Newtonsoft.Json.dll");
                File.Copy(sourceAssembly, duplicateAssembly, true);
                SetAsmInfo.SetAsmInfo.SetInfo(duplicateAssembly, new Version("1.2.3"), new Version("1.2.3"), SemanticVersion.Parse("1.2.3"));

                var (stdout, stderr, exitCode) = RunTap("package list --installed --quiet");
                Assert.That(exitCode, Is.EqualTo(0), $"tap exited with {exitCode}.\nstdout: {stdout}\nstderr: {stderr}");

                // The duplicate assembly warning should be written to stderr.
                Assert.That(stderr, Does.Contain("Multiple assemblies of different versions named Newtonsoft.Json"));

                // Under --quiet, nothing at all should be written to stdout.
                Assert.That(stdout, Is.Empty);
            }
            finally
            {
                Directory.Delete(duplicateDir, true);
            }
        }

        /// <summary>
        /// Without --quiet the duplicate assembly warning still goes to stderr while
        /// regular output goes to stdout.
        /// </summary>
        [Test]
        public void DuplicateAssemblyWarningNotOnStdout()
        {
            string installDir = ExecutorClient.ExeDir;
            string sourceAssembly = Path.Combine(installDir, "Newtonsoft.Json.dll");
            Assert.That(sourceAssembly, Does.Exist);

            string duplicateDir = Path.Combine(installDir, nameof(DuplicateAssemblyWarningNotOnStdout));
            Directory.CreateDirectory(duplicateDir);
            try
            {
                string duplicateAssembly = Path.Combine(duplicateDir, "Newtonsoft.Json.dll");
                File.Copy(sourceAssembly, duplicateAssembly, true);
                SetAsmInfo.SetAsmInfo.SetInfo(duplicateAssembly, new Version("1.2.3"), new Version("1.2.3"), SemanticVersion.Parse("1.2.3"));

                var (stdout, stderr, exitCode) = RunTap("package list --installed");
                Assert.That(exitCode, Is.EqualTo(0), $"tap exited with {exitCode}.\nstdout: {stdout}\nstderr: {stderr}");

                // The warning goes to stderr and only to stderr.
                Assert.That(stderr, Does.Contain("Multiple assemblies of different versions named Newtonsoft.Json"));
                Assert.That(stdout, Does.Not.Contain("Multiple assemblies of different versions"));

                // Regular output (the package list) still goes to stdout.
                Assert.That(stdout, Does.Contain("OpenTAP"));
            }
            finally
            {
                Directory.Delete(duplicateDir, true);
            }
        }

        static (string stdout, string stderr, int exitCode) RunTap(string arguments)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(ExecutorClient.ExeDir, "tap"),
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            // Avoid update check messages interfering with the output.
            p.StartInfo.EnvironmentVariables["OPENTAP_NO_UPDATE_CHECK"] = "true";

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var lockObj = new object();
            p.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    lock (lockObj)
                        stdout.AppendLine(e.Data);
            };
            p.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    lock (lockObj)
                        stderr.AppendLine(e.Data);
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (!p.WaitForExit(120_000))
            {
                p.Kill();
                Assert.Fail("tap process did not exit within the timeout.");
            }
            int exitCode = p.ExitCode;
            p.WaitForExit(); // wait for output processing to complete.

            lock (lockObj)
                return (stdout.ToString(), stderr.ToString(), exitCode);
        }
    }
}

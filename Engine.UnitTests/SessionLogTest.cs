//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap;
using System.IO;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SessionLogTest
    {
        TraceSource log =  OpenTap.Log.CreateSource("SessionLogTest");
        [Test]
        public void RenameSessionLogFile()
        {
            Log.Flush();

            var currentName = SessionLogs.GetSessionLogFilePath();
            SessionLogs.Rename("Log1.txt");
            SessionLogs.Rename("LogTest\\Log2.txt");
            string inlog = "This is written to log2";
            log.Debug(inlog);
            Log.Flush();
            SessionLogs.Flush();
            var file = File.Open("LogTest\\Log2.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (var reader = new StreamReader(file))
            {
                var part = reader.ReadToEnd();
                StringAssert.Contains(inlog, part);
            }

            Assert.AreEqual(SessionLogs.GetSessionLogFilePath(), "LogTest\\Log2.txt");
            SessionLogs.Rename(currentName);
            Assert.AreEqual(currentName, SessionLogs.GetSessionLogFilePath());
            Assert.IsFalse(File.Exists("Log1.txt"));
            Assert.IsFalse(File.Exists("LogTest\\Log2.txt"));
            Assert.IsTrue(Directory.Exists("LogTest"));
        }
    }
}

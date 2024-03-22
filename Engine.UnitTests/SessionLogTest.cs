//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Tap.Shared;

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
            SessionLogs.Rename("LogTest/Log2.txt");
            string inlog = "This is written to log2";
            log.Debug(inlog);
            Log.Flush();
            SessionLogs.Flush();
            var file = File.Open("LogTest/Log2.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (var reader = new StreamReader(file))
            {
                var part = reader.ReadToEnd();
                StringAssert.Contains(inlog, part);
            }

            Assert.AreEqual(Path.GetFullPath(SessionLogs.GetSessionLogFilePath()), Path.GetFullPath("LogTest/Log2.txt"));
            SessionLogs.Rename(currentName);
            Assert.AreEqual(currentName, SessionLogs.GetSessionLogFilePath());
            Assert.IsFalse(File.Exists("Log1.txt"));
            Assert.IsFalse(File.Exists("LogTest/Log2.txt"));
            Assert.IsTrue(Directory.Exists("LogTest"));
        }

         /// <summary>
         ///  Test the log rollover due to large file works.
         /// </summary>
        [Test]
        public void LogRolloverTest()
        {
            Log.Flush();
            var currentName = SessionLogs.GetSessionLogFilePath();
            
            var prev = SessionLogs.MaxTotalSizeOfSessionLogFiles;
            var prevLogFileMaxSize = SessionLogs.LogFileMaxSize;
            try
            {
                SessionLogs.MaxTotalSizeOfSessionLogFiles = 300_000;
                SessionLogs.LogFileMaxSize = 10_000;
                var guid = Guid.NewGuid();
                SessionLogs.Rename($"TestLogs/{guid}-RolloverTest.txt", true);
                // this should result in about 30 log files, but the number of files is limited to 20.
                for (int i = 0; i < 300; i++)
                {
                    log.Info(new string('?', 1000));
                    log.Flush();
                }
                var files1 = Directory.GetFiles("TestLogs/").Where(file => file.Contains(guid.ToString())).ToArray();
                Assert.AreEqual(20, files1.Length);
                var totalLength1 = files1.Select(file => new FileInfo(file).Length).Sum();
                // totalLength of log files should be around 20 * 10000. Limited by max number of files (20).
                
                Assert.IsTrue(totalLength1 - 20 * 10000 < 20 * 1000); // allow for an overshoot of 1kb per file.
                
                SessionLogs.LogFileMaxSize = 30_000;
                guid = Guid.NewGuid();
                SessionLogs.Rename($"TestLogs/{guid}-RolloverTest.txt", true);
                for (int i = 0; i < 300; i++)
                {
                    log.Info(new string('?', 1000));
                    log.Flush();
                }
                var files2 = Directory.GetFiles("TestLogs/").Where(file => file.Contains(guid.ToString())).ToArray();
                
                var totalLength2 = files2.Select(file => new FileInfo(file).Length).Sum();
                // now totalLength should be around 300000 limited by the maximally combined size.
                
                Assert.IsTrue(Math.Abs(totalLength2 - 300000) < 30000); // allow for an overshoot of 10kb.

                // any file from before should have been removed.
                Assert.IsFalse(files1.Any(File.Exists));
                
                guid = Guid.NewGuid();
                SessionLogs.Rename($"TestLogs/{guid}-RolloverTest.txt", true);
                foreach (var file in files2)
                {
                    File.Delete(file);
                }
                
                // now generate logs so there should be exactly two files.
                for (int i = 0; i < 7; i++)
                {
                    log.Info(new string('?', 5000));
                    log.Flush();
                }
                var files3 = Directory.GetFiles("TestLogs/").Where(file => file.Contains(guid.ToString())).ToArray();
                var totalLength3 = files3.Sum(x => new FileInfo(x).Length);
                Assert.IsTrue(Math.Abs(totalLength3 - 35000) < 5000);
                Assert.AreEqual(2, files3.Length);
                
                var activePath = files3.OrderBy(x => new FileInfo(x).Length).First();
                var currentLogFile = SessionLogs.GetSessionLogFilePath();
                Assert.IsTrue(PathUtils.AreEqual(activePath, currentLogFile));
                

            }
            finally
            {
                SessionLogs.MaxTotalSizeOfSessionLogFiles = prev;
                SessionLogs.LogFileMaxSize = prevLogFileMaxSize;
                SessionLogs.Rename(currentName);

            }
        }
    }
}

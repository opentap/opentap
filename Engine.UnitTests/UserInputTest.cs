//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class UserInputTest : IUserInputInterface
    {
        public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
        {
            
        }

        public class TestObject
        {
            public int X { get; set; }
        }
        [Test]
        [Ignore("Intermitantly fails on CI runners")]
        public void TestValuesExtremes()
        {
            var prev = UserInput.GetInterface();
            try
            {
                CliUserInputInterface.Load();
                TestObject obj = new TestObject();
                var sem = new SemaphoreSlim(0);
                bool exceptionHappened = false;
                try
                {
                    UserInput.Request(obj, TimeSpan.Zero, true); // should return immediately.
                    Assert.Fail("Timeout exception should have been thrown");
                }
                catch (TimeoutException)
                {

                }
                var trd = TapThread.Start(() =>
                {
                    try
                    {
                        UserInput.Request(obj, TimeSpan.MaxValue, true);
                    }
                    catch(OperationCanceledException)
                    {
                        exceptionHappened = true;
                    }

                    sem.Release();                    
                });
                trd.Abort();
                if (!sem.Wait(1000) || exceptionHappened == false)
                    Assert.Fail("Should have been canceled by thread");
            }
            finally
            {
                UserInput.SetInterface(prev);
            }
        }

        [Test]
        public void TestUserInputOrder()
        {
            using (Session.Create())
            {
                CliUserInputInterface.Load();
                var request = new TestRequest();
                using (var writer = new StringWriter())
                {
                    var prevOut = Console.Out;
                    try
                    {
                        Console.SetOut(writer);
                        UserInput.Request(request);

                        var log = writer.ToString()
                            .Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i <= 5; i++)
                            Assert.AreEqual(log[i], i.ToString());
                    }
                    finally
                    {
                        Console.SetOut(prevOut);
                    }
                }
            }
        }
    }
    
    public class TestRequest
    {
        [Display("g", Order: 2)]
        [Submit]
        [Browsable(true)]
        public string g { get; }
        
        [Display("e", Order: 2)]
        [Layout(LayoutMode.FloatBottom)]
        [Browsable(true)]
        public string e { get; }
        
        [Display("d", Order: 2)]
        [Browsable(true)]
        public string d { get; }
        
        [Display("c", Order: 3)]
        [Browsable(true)]
        public string c { get; }
        
        [Display("b", Order: 2)]
        [Browsable(true)]
        public string b { get; }
        
        [Display("a", Order: 1)]
        [Browsable(true)]
        public string a { get; }

        [Browsable(true)]
        public string f { get; }
        
        public TestRequest()
        {
            f = "0";
            a = "1";
            b = "2";
            d = "3";
            c = "4";
            e = "5";
            g = "6";
        }
    }
}

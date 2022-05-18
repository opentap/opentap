//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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

        class UserInputWithCallback
        {
            public int A { get; set; }
            [Submit(nameof(InvokeCalculation))]
            public int B { get; set; }
            public int C { get; private set; }

            public void InvokeCalculation()
            {
                if (B == 0) throw new UserInputRetryException("B cannot be 0", nameof(B));
                C = A * B;
            }
        }

        class TestInputInterface : CliUserInputInterface
        {
            CancellationToken cancel;
            public TestInputInterface(CancellationToken cancel) => this.cancel = cancel;
            public string Input { get; set; }
            int i;
            protected override ConsoleKeyInfo ReadKey()
            {
                if (i >= Input.Length)
                {
                    Task.Delay(-1, cancel).Wait();
                }
                    
                var chr = Input[i++];
                if (chr == '\n')
                {
                    return new ConsoleKeyInfo('\n', ConsoleKey.Enter, false, false, false);
                }
                return new ConsoleKeyInfo(chr, (ConsoleKey)chr, false, false, false);
            }
        }
        
        [Test]
        public void UserInputSubmitCallbackTest()
        {
            using (Session.Create())
            {
                var src = new CancellationTokenSource(); 
                UserInput.SetInterface(new TestInputInterface(src.Token){Input = "3\n2\n"});
                var request = new UserInputWithCallback();

                try
                {
                    UserInput.Request(request, TimeSpan.FromMinutes(2));
                    Assert.AreEqual(6, request.C);
                }
                finally
                {
                    src.Cancel();
                }
            }
        }
        
        [Test]
        public void UserInputSubmitCallbackTest2()
        {
            using (Session.Create())
            {
                var src = new CancellationTokenSource(); 
                // the first 3, 0 fails and the 4, 10 is inserted.
                UserInput.SetInterface(new TestInputInterface(src.Token){Input = "3\n0\n4\n10\n"});
                var request = new UserInputWithCallback();

                try
                {
                    UserInput.Request(request, TimeSpan.FromMinutes(2));
                    Assert.AreEqual(40, request.C);
                }
                finally
                {
                    src.Cancel();
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

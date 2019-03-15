//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
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
    }
}

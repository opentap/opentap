using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    public class StagedExecutorTests
    {
        public interface ITestExecutionStage : IExecutionStage
        {

        }

        public class FirstStage : ITestExecutionStage
        {
            public int Output = 0;
            public void Execute()
            {
                Debug.WriteLine("First stage executing.");
                Thread.Sleep(50);
                Output = 1;
            }
        }

        public class SecondStage : ITestExecutionStage
        {
            public FirstStage First { get; set; }
            
            public void Execute()
            {
                Assert.NotNull(First);
                Assert.AreEqual(1, First.Output);
                Debug.WriteLine("Second stage executing.");
                Thread.Sleep(50);
            }
        }

        public class ThirdStage : ITestExecutionStage
        {
            public SecondStage Dep { get; set; }

            public void Execute()
            {
                Assert.NotNull(Dep);
                Thread.Sleep(50);
            }
        }

        public class FourthStage : ITestExecutionStage
        {
            public static bool DidRun = false;
            public SecondStage Dep { get; set; }
            public ThirdStage Dep2 { get; set; }

            public void Execute()
            {
                Assert.NotNull(Dep);
                Assert.NotNull(Dep2);
                Thread.Sleep(50);
                DidRun = true;
            }
        }

        [Test]
        public void Execute()
        {
            var executor = new StagedExecutor(TypeData.FromType(typeof(ITestExecutionStage)));
            executor.Execute();
            Assert.IsTrue(FourthStage.DidRun, "Execute completed without the last stage getting executed.");
        }
    }
}

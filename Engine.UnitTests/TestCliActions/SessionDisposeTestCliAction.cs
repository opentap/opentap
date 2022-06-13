using System;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Engine.UnitTests
{
        [Display("session-dispose-test", Description: "A test that adds a session local that needs to be disposed..", Group: "test")]
    public class SessionDisposeTestCliAction : ICliAction
    {
        private static TraceSource log = Log.CreateSource("session-dispose"); 
        class DisposableClassTest : IDisposable
        {
            public void Dispose()
            {
                // log cannot be used at this point.
                Console.WriteLine("session-dispose-test disposed");
            }
        }

        static readonly SessionLocal<DisposableClassTest> Local = new SessionLocal<DisposableClassTest>(autoDispose: true);
        public int Execute(CancellationToken cancellationToken)
        {
            Local.Value = new DisposableClassTest();
            log.Info("Set disposable");
            TapThread.Start(() =>
            {
                while (true)
                {
                    TapThread.Sleep(1000);
                }
            });
            return 0;
        }
    }
}
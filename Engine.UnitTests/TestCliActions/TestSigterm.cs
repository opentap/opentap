
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.UnitTests
{
    [Display("sigterm")]
    public class SigtermAction : ICliAction
    {
        private static TraceSource log = Log.CreateSource(nameof(SigtermAction));
        public int Execute(CancellationToken cancellationToken)
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess().Id;
            log.Info($"Process {proc} running!");
            TapThread.Current.AbortToken.Register(() =>
            {
                log.Info("AbortToken handler was invoked!");
            });
            if (TapThread.Current.AbortToken.WaitHandle.WaitOne(10000))
            {
                log.Info("WaitHandle was triggered.");
                return 0;
            }
            else
            {
                log.Info("WaitHandle wait timed out.");
                return 1;
            }
        }
    }
}

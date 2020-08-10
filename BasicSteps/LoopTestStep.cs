using System.ComponentModel;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    public abstract class LoopTestStep : TestStep
    {
        protected CancellationTokenSource breakLoopToken { get; private set; }

        [Browsable(false)]
        protected CancellationToken BreakLoopRequested => breakLoopToken.Token;

        public LoopTestStep()
        {
            breakLoopToken = new CancellationTokenSource();
        }
        
        public void BreakLoop()
        {
            breakLoopToken.Cancel();
        }

        /// <summary> Always call base.Run in LoopTestStep inheritors. </summary>
        public override void Run()
        {
            breakLoopToken = new CancellationTokenSource();
        }
    }
}
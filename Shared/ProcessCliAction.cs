using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap
{
    [Browsable(false)]
    internal class ProcessCliAction : ICliAction
    {
        [CommandLineArgument("PipeHandle")] public string PipeHandle { get; set; }

        public int Execute(CancellationToken cancellationToken)
        {
            var client = new NamedPipeClientStream(".", PipeHandle, PipeDirection.InOut);
            var listener = new EventTraceListener();

            listener.MessageLogged += evts =>
            {
                if (client.IsConnected == false) return;
                client.WriteMessage(evts.ToArray());
            };

            try
            {
                client.Connect();
                var plan = client.ReadMessage<TestPlan>();

                Log.AddListener(listener);
                return (int)plan.Execute(Array.Empty<IResultListener>()).Verdict;
            }
            finally
            {
                // ensure the log is flushed before removing the listener.
                Log.Flush();
                Log.RemoveListener(listener);
                client.Dispose();
            }
        }
    }
}
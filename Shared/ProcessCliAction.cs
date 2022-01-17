using System;
using System.ComponentModel;
using System.IO.Pipes;
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
                foreach (var evt in evts)
                {
                    if (client.IsConnected == false) return;
                    // The log will be flushed before the client is disposed
                    client.WriteMessage(SerializationHelper.EventToBytes(evt));
                }
            };

            try
            {
                client.Connect();
                var msg = client.ReadMessage();
                var steps = (ITestStep)new TapSerializer().Deserialize(msg);
                var plan = new TestPlan();
                plan.ChildTestSteps.Add(steps);

                Log.AddListener(listener);

                return (int)plan.Execute(Array.Empty<IResultListener>()).Verdict;
            }
            finally
            {
                Log.RemoveListener(listener);
                client.Dispose();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
namespace OpenTap;

/// <summary> Arguments for TestPlan.Execute </summary>
public sealed class TestPlanExecuteArgs
{
    /// <summary> The result listeners used in the run. If not set, this will get the default value of ResultSettings.Current.</summary>
    public IEnumerable<IResultListener> ResultListeners { get; set; } = ResultSettings.Current;

    /// <summary> Additional Parameters for the run.</summary>
    public IEnumerable<ResultParameter> Parameters { get; set; }

    /// <summary> This is set when it is only wanted to run a subset of the steps in the test plan.</summary>
    public ISet<ITestStep> SelectedTestSteps { get; set; }

    /// <summary> Callback invoked when <see cref="OpenTap.TestPlanRun.Pause" /> is called from within the test plan.</summary>
    public Action<TestPlanPauseEventArgs> OnPauseRequestedCallback { get; set; }

    /// <summary> This cancellation token allows cancelling  the test plan execution. </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}

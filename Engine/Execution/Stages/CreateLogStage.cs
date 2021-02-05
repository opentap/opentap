//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    class CreateLogStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(CL)");

        public string fileStreamFile { get; private set; }
        public HybridStream logStream { get; private set; }
        public FileTraceListener planRunLog { get; private set; }

        protected override bool Execute(TestPlanExecutionContext context)
        {
            Log.Info("-----------------------------------------------------------------"); // Print this to the session log, just before attaching the run log

            fileStreamFile = FileSystemHelper.CreateTempFile();
            logStream = new HybridStream(fileStreamFile, 1024 * 1024);

            planRunLog = new FileTraceListener(logStream) { IsRelative = true };
            OpenTap.Log.AddListener(planRunLog);
            return true;
        }
    }
}

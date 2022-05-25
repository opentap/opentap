using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// Used to cancel a teststep without printing a stacktrace, will also set the verdice if necessary,
    /// </summary>
    public class ExpectedException : Exception
    {
        public ExpectedException(string message = "", Verdict verdict = Verdict.Error): base(message)
        {
            Verdict = verdict;
        }

        public Verdict Verdict { get; set; }

        
    }
}

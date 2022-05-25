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
        /// <summary>
        /// Creates a new expected exception with an optional message and verdict.
        /// </summary>
        public ExpectedException(string message = "", Verdict verdict = Verdict.Error): base(message)
        {
            Verdict = verdict;
        }

        /// <summary>
        /// The verdict of this teststep.
        /// </summary>
        public Verdict Verdict { get; set; }

        
    }
}

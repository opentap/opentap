using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// Used to cancel a teststep without printing a stacktrace, will also set the verdict if necessary.
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
        public virtual Verdict Verdict { get; set; }

        /// <summary>
        /// Handles this exception by writing the appropriate information to the log.
        /// </summary>
        public void Handle(string stepName)
        {
            if (string.IsNullOrWhiteSpace(Message))
            {
                TestPlan.Log.Info("Test step {0} stopped.", stepName);
            }
            else
            {
                TestPlan.Log.Info("{1}", Message);
            }
        }
    }
}

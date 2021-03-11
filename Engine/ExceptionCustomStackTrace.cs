using System;

namespace OpenTap
{
    /// <summary>
    /// Used to rethrow, adding more exception info but without generating a lot of extra stack lines.
    /// </summary>
    class ExceptionCustomStackTrace : Exception
    {
        public ExceptionCustomStackTrace(string message, string stackTrace, Exception innerException = null)
            : base(message, innerException)
        {
            StackTrace = stackTrace;
        }

        public override string StackTrace { get; }
    }
}
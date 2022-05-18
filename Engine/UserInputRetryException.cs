using System;

namespace OpenTap
{
    /// <summary>
    /// This exception is thrown by a SubmitAttribute callback to signal that an operation was unable to complete.
    /// </summary>
    public class UserInputRetryException : Exception
    {
        /// <summary> The members which were affected by the error, if any. </summary>
        public string[] Members { get; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="affectedMembers">The members affected by this. </param>
        public UserInputRetryException(string message, params string[] affectedMembers) : base(message)
        {
            this.Members = affectedMembers;
        }
    }
}
//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Cli
{
    /// <summary>
    /// Thrown from <see cref="OpenTap.Cli.ICliAction.Execute"/> to set a specific exit code
    /// </summary>
    public class ExitCodeException : Exception
    {
        /// <summary>
        /// Exit code to set when ending the process.
        /// </summary>
        public int ExitCode { get; private set; }
        /// <summary>
        /// Instanciates a new <see cref="ExitCodeException"/>
        /// </summary>
        /// <param name="exitcode"> Exit code to set when ending the process</param>
        /// <param name="message">Exception message to print in the log</param>
        public ExitCodeException(int exitcode, string message) : base(message)
        {
            ExitCode = exitcode;
        }
    }
}

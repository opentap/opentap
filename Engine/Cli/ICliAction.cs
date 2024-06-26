//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Threading;

namespace OpenTap.Cli
{
    /// <summary>
    /// Plugin type that defines a sub command for the OpenTAP CLI (tap.exe). 
    /// Deriving from this, and annotating the class and any public properties with <see cref="CommandLineArgumentAttribute"/> and <see cref="UnnamedCommandLineArgument"/> attributes
    /// will allow it to be called from the OpenTAP CLI.
    /// </summary>
    public interface ICliAction : ITapPlugin
    {        
        /// <summary>
        /// The code to be executed by the action.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        int Execute(CancellationToken cancellationToken);
    }
    
    internal static class ICliActionExecuteHelper
    {
        /// <summary>
        /// Logs the assembly name and version then executes the action with the given parameters.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="parameters">The parameters for the action.</param>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        public static int Execute(this ICliAction action, string[] parameters)
        {
            return action.PerformExecute(parameters);
        }
    }
}

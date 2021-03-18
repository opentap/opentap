using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Cli
{
    /// <summary>
    /// ExitCodes reserved by OpenTAP. Uses range 192 to 255. OpenTAP Plugins should use positive numbers between 1 and 191 for custom error codes.
    /// For best cross platform compatibility all exitcodes should be positive and between 0 and 255.
    /// </summary>
    public enum ExitCodes
    {
        /// <summary>
        /// CLI action completed successfully
        /// </summary>
        [Display("Success", "CLI Action completed successfully")]
        Success = 0,
        /// <summary>
        /// User cancelled CLI action
        /// </summary>
        [Display("UserCancelled", "User cancelled CLI action")]
        UserCancelled = 192,
        /// <summary>
        /// CLI action threw an unhandled exception
        /// </summary>
        [Display("GeneralException", "CLI action threw an unhandled exception")]
        GeneralException = 193,
        /// <summary>
        /// No CLI action found matching commands
        /// </summary>
        [Display("UnknownCliAction", "No CLI action found matching commands")]
        UnknownCliAction = 194,
        /// <summary>
        /// CLI action missing a license
        /// </summary>
        [Display("LicenseError", "CLI action missing a license")]
        LicenseError = 195,
        /// <summary>
        /// Unable to parse one or more arguments
        /// </summary>
        [Display("ArgumentParseError", "Unable to parse one or more arguments")]
        ArgumentParseError = 196,
        /// <summary>
        /// One or more arguments is incorrect
        /// </summary>
        [Display("ArgumentError", "One or more arguments is incorrect")]
        ArgumentError = 197,
        /// <summary>
        /// Network error occurred
        /// </summary>
        [Display("NetworkError", "Network error occurred")]
        NetworkError = 198,
    }
}

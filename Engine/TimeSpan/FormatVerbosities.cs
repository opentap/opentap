/* -----------------------------------------------------
 *
 * File: FormatVerbosities.cs
 * Author: sven.kopacz@keysight.com
 * Created: 21.10.2016
 *
 * -----------------------------------------------------
 * 
 * Description: See class summary
 *
 * -----------------------------------------------------
 */

using System;

namespace OpenTap
{
    /// <summary>   Indicates to what extend a formatter should be verbose. </summary>
    public enum FormatVerbosities
    {
        /// <summary>   For the unit minutes, a formatter would just return ":". </summary>
        SuperBrief,
        /// <summary>   For the unit seconds, a formatter would return "s". </summary>
        Brief,
        /// <summary>   For the unit seconds, a formatter would return "sec". </summary>
        Normal,
        /// <summary>   For the unit seconds, a formatter would return "second". </summary>
        Verbose
    }
    /// <summary>
    /// Attribute for giving directives to the TimeSpanControl provider, that shows the control provider in the GUI.
    /// </summary>
    public class TimeSpanFormatAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether 'None' is allowed as a textual representation of
        /// TimeSpan.Zero.
        /// </summary>
        ///
        /// <value> true if 'None' is allowed, false if not. </value>
        public bool AllowNone { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether we a negative timespan is allowed. If set to false,
        /// any negative input will automatically be turned into a positive TimeSpan.
        /// </summary>
        ///
        /// <value> true if negative values are allowed, false if not. </value>
        public bool AllowNegative { get; private set; }

        /// <summary>
        /// Sets the preferred verbosity for formatting the TimeSpan value.
        /// </summary>
        public FormatVerbosities Verbosity { get; private set; }

        /// <summary>
        /// Time span format attribute.
        /// </summary>
        /// <param name="allowNone"></param>
        /// <param name="allowNegative"></param>
        /// <param name="verbosity"></param>
        public TimeSpanFormatAttribute(bool allowNone = false, bool allowNegative = false, FormatVerbosities verbosity = FormatVerbosities.Normal)
        {
            AllowNone = allowNone;
            AllowNegative = allowNegative;
            Verbosity = verbosity;
        }
    }
}

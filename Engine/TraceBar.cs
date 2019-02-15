//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Text;


namespace OpenTap
{
    /// <summary>
    /// Utility class to help print an "ASCII-art" graph with limits in the log files and windows. 
    /// </summary>
    public class TraceBar
    {
        /// <summary>
        /// Gets or sets the lower limit which is also used to determine the range of the graph.
        /// </summary>
        public double LowerLimit { get; set; }

        /// <summary>
        /// Gets or sets the upper limit which is also used to determine the range of the graph.
        /// </summary>
        public double UpperLimit { get; set; }
        /// <summary>
        /// Gets or sets a Boolean value indicating whether the result will be printed as a number
        /// next to the bar.
        /// </summary>
        public bool ShowResult { get; set; }
        /// <summary>
        /// Gets or sets a Boolean value indicating whether the verdict (pass/fail) will be shown 
        /// next to the bar.
        /// </summary>
        public bool ShowVerdict { get; set; }
        /// <summary>
        /// Gets or sets the length of the bar in characters.
        /// </summary>
        public byte BarLength { get; set; }

        /// <summary>
        /// Combined verdict for all times GetBar has been called.
        /// </summary>
        public Verdict CombinedVerdict { get; private set; }

        /// <summary>
        /// Returns a string containing a "tracebar" ready to be printed in the log.
        /// </summary>
        /// <param name="result">The value to visualize in a TraceBar.</param>
        /// <returns>A string containing a "tracebar" ready to be printed in the log.</returns>
        public string GetBar(double result)
        {
            byte stringLength = BarLength;
            double width = (LowerLimit - UpperLimit);
            double stepSize = Math.Abs(width / stringLength);
            bool passed = result <= UpperLimit && result >= LowerLimit;
            Verdict verdict = passed ? Verdict.Pass : Verdict.Fail;
            if (double.IsNaN(result))
                verdict = Verdict.Inconclusive;
            if (verdict > CombinedVerdict)
                CombinedVerdict = verdict;
            StringBuilder bar;

            if (ShowResult)
            {
                bar = new StringBuilder();
                bar.AppendFormat("{0,12:G6} ", result);
            }
            else
                bar = new StringBuilder(stringLength + 20);

            bar.AppendFormat("{0,6}  ", LowerLimit);

            for (byte count = 0; count <= stringLength; count++)
            {
                if (count == 0 & result < LowerLimit)
                {
                    bar.Append('<');
                }
                else if (count == stringLength & result > UpperLimit)
                {
                    bar.Append('>');
                    break;
                }
                else if ((result >= (count * stepSize + LowerLimit)) && (result < ((count + 1) * stepSize + LowerLimit)))
                {
                    bar.Append('|');
                }
                else
                    bar.Append('-');
            }
            bar.AppendFormat(" {0,6}", UpperLimit);

            if (ShowVerdict)
            {
                if (!passed)
                {
                    bar.Append("  " + verdict.ToString());
                }
            }

            return bar.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the TraceBar class.
        /// </summary>
        public TraceBar()
        {
            BarLength = 30;
            LowerLimit = -100;
            UpperLimit = 100;
            ShowResult = true;
            ShowVerdict = true;
            CombinedVerdict = OpenTap.Verdict.NotSet;
        }

    }
}

//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// A <see cref="Connection"/> that has an RF cable loss parameter.
    /// </summary>
    [Display("RF Connection", "Directionless RF connection modeled as a set of cable loss points with frequency and loss values.", "Basic Connections")]
    public class RfConnection : Connection
    {
        /// <summary>
        /// Represents a point in a frequency/loss table.
        /// </summary>
        public class CableLossPoint : ValidatingObject
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CableLossPoint"/> class.
            /// </summary>
            public CableLossPoint()
            {
                Rules.Add(() => Frequency >= 0, "Frequency must not be negative.", "Frequency");
            }

            /// <summary>
            /// The frequency at which the loss is applied.
            /// </summary>
            [Unit("Hz", true)]
            public double Frequency { get; set; }
            /// <summary>
            /// The cable loss in dB.
            /// </summary>
            [Unit("dB")]
            public double Loss { get; set; }

            static readonly UnitAttribute frequencyUnit = typeof(CableLossPoint).GetProperty(nameof(Frequency)).GetAttribute<UnitAttribute>();
            static readonly UnitAttribute lossUnit = typeof(CableLossPoint).GetProperty(nameof(Loss)).GetAttribute<UnitAttribute>();

            /// <summary>
            /// Prints the loss point, e.g "10dB @ 100kHz".
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                var freqParser = new NumberFormatter(CultureInfo.CurrentCulture, frequencyUnit) { IsCompact = true };
                var lossParser = new NumberFormatter(CultureInfo.CurrentCulture, lossUnit) { IsCompact = true };

                return $"{lossParser.FormatNumber(Loss)} @{freqParser.FormatNumber(Frequency)}";
            }
        }

        /// <summary>
        /// The cable loss in dB between the two ports.
        /// </summary>
        [Display("Cable Loss")]
        public List<CableLossPoint> CableLoss { get; set; }
       

        /// <summary>
        /// Initializes a new instance of the <see cref="RfConnection"/> class.
        /// </summary>
        public RfConnection()
        {
            Name = "RF";
            CableLoss = new List<CableLossPoint>();
            Rules.Add(() => CableLoss.Any(c => c.Frequency < 0) == false, "Frequency must not be negative.", "CableLoss");
            Rules.Add(() => CableLoss.Count == CableLoss.Select(c => c.Frequency).Distinct().Count(), "A frequency may only be specified once.", "CableLoss");
            
            Rules.Add(() => PortDirectionsAreGood(), () => "An output port cannot be connected to another output port.", "Port1");
            Rules.Add(() => PortDirectionsAreGood(), () => "An output port cannot be connected to another output port.", "Port2");
        }

        private bool PortDirectionsAreGood()
        {
            return (Port1 == null) || (Port2 == null) || ((Port1 is OutputPort) ? !(Port2 is OutputPort) : true);
        }

        /// <summary>
        /// Given a particular frequency, an interpolated CableLoss value is returned, based on CableLoss values at the two closest frequencies.
        /// If exact frequency is defined, that CableLoss value will be returned.
        /// </summary>
        /// <param name="frequency"></param>
        /// <returns></returns>
        public double GetInterpolatedCableLoss(double frequency)
        {
            CableLoss = CableLoss.OrderBy(loss => loss.Frequency).ToList();
            CableLossPoint below = CableLoss.LastOrDefault(loss => loss.Frequency < frequency || (Math.Abs(loss.Frequency - frequency) < double.Epsilon)); //Check for below, or if value exists.
            CableLossPoint above = CableLoss.FirstOrDefault(loss => loss.Frequency > frequency);

            if (below != null && above != null)
            {
                return below.Loss + (frequency - below.Frequency) * (above.Loss - below.Loss) / (above.Frequency - below.Frequency);
            }
                
            else if (below != null)
                return below.Loss;
            else if (above != null)
                return above.Loss;
            throw new System.Exception("No cable loss values specified.");
        }
    }
}

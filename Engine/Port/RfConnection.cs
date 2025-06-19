//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
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
            internal class FrequencyComparer : IComparer<CableLossPoint>
            {
                public static readonly FrequencyComparer Instance = new FrequencyComparer();

                public int Compare(CableLossPoint x, CableLossPoint y)
                {
                    if (ReferenceEquals(x, y))
                        return 0;
                    if (ReferenceEquals(null, y))
                        return 1;
                    if (ReferenceEquals(null, x))
                        return -1;
                    var frequencyComparison = x.Frequency.CompareTo(y.Frequency);
                    return frequencyComparison;
                    
                }
            }
            
            /// <summary> Returns an error if the frequency is less 0. </summary>
            protected override string GetError(string propertyName = null)
            {
                if (propertyName == nameof(Frequency) || propertyName == null)
                {
                    if (Frequency < 0)
                        return "Frequency must not be negative";
                }
                return null;
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
        public double GetInterpolatedCableLoss(double frequency)
        {
            // If there are no cable loss points configured assume no loss.
            if (CableLoss.Count == 0) return 0.0;
            
            // If there is only one point there is nothing to interpolate
            if (CableLoss.Count == 1) return CableLoss[0].Loss;
            
            // Sort the loss table by frequency.
            // only do so if it actually needs to be sorted.
            if(!CableLoss.IsSortedBy(loss => loss.Frequency))
                CableLoss = CableLoss.OrderBy(loss => loss.Frequency).ToList();
            
            // for searching.
            var loss = new CableLossPoint { Frequency = frequency };
            
            // Find the index by binary search.
            // if the result >= 0 it means an exact match was found.
            // if the result <0 it means that the match lies somewhere between two points or at the bounds.
            int index = CableLoss.BinarySearch(loss, CableLossPoint.FrequencyComparer.Instance);
            
            // Calculate loss using linear interpolation or nearest neighbour when outside the bounds.
            if (index < 0)
            {
                // the match is at the bounds.
                // that means the index found is the first element greater than the searched frequency.
                // hence 0 -> the result is less than the minimum (nearest interpolation)
                // and count -> the result is greater than the maximum. (nearest interpolation).
                // otherwise interpolate between index -1 and index.
                index = ~index;
            }
            else
            { 
                // exact match.
                return CableLoss[index].Loss;
            }
            if (index == 0)
                return CableLoss[0].Loss;
            if (index == CableLoss.Count)
                return CableLoss[index - 1].Loss;
            
            CableLossPoint below = CableLoss[index - 1]; //Check for below, or if value exists.
            CableLossPoint above = CableLoss[index];

           // linear interpolation.
           return below.Loss + (frequency - below.Frequency) * (above.Loss - below.Loss) / (above.Frequency - below.Frequency);
           
        }
    }
}

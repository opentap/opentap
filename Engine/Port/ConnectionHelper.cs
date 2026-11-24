using System.Collections.Generic;
namespace OpenTap
{
    public static class ConnectionHelper
    {
        /// <summary>
        /// Given a particular frequency, an interpolated CableLoss value is returned, based on CableLoss values at the two closest frequencies.
        /// If exact frequency is defined, that CableLoss value will be returned.
        /// </summary>
        public static double GetInterpolatedCableLoss(this List<RfConnection.CableLossPoint> cableLoss, double frequency)
        {
            // If there are no cable loss points configured assume no loss.
            if (cableLoss.Count == 0) return 0.0;
            
            // If there is only one point there is nothing to interpolate
            if (cableLoss.Count == 1) return cableLoss[0].Loss;
            
            // Sort the loss table by frequency.
            // only do so if it actually needs to be sorted.
            if(!cableLoss.IsSortedBy(loss => loss.Frequency))
                cableLoss.Sort(RfConnection.CableLossPoint.FrequencyComparer.Instance);
            
            // for searching.
            var loss = new RfConnection.CableLossPoint { Frequency = frequency };
            
            // Find the index by binary search.
            // if the result >= 0 it means an exact match was found.
            // if the result <0 it means that the match lies somewhere between two points or at the bounds.
            int index = cableLoss.BinarySearch(loss, RfConnection.CableLossPoint.FrequencyComparer.Instance);
            
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
                return cableLoss[index].Loss;
            }
            if (index == 0)
                return cableLoss[0].Loss;
            if (index == cableLoss.Count)
                return cableLoss[index - 1].Loss;
            
            RfConnection.CableLossPoint below = cableLoss[index - 1]; //Check for below, or if value exists.
            RfConnection.CableLossPoint above = cableLoss[index];

            // linear interpolation.
            return below.Loss + (frequency - below.Frequency) * (above.Loss - below.Loss) / (above.Frequency - below.Frequency);
           
        }
    }
}

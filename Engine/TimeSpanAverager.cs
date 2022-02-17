using System;

namespace OpenTap
{
    class TimeSpanAverager
    {
        int averageCnt = 0;
        long[] weights = new long[10];
        int averageIndex = 0;

        public void PushTimeSpan(TimeSpan ts)
        {
            var indexOfValue = averageIndex = (averageIndex + 1) % weights.Length;
            weights[indexOfValue] = ts.Ticks;
            averageCnt = Math.Min(weights.Length, averageCnt + 1);
        }

        static TimeSpan defaultSpan = TimeSpan.FromSeconds(0.1);

        public TimeSpan GetAverage()
        {
            if (averageCnt == 0) return defaultSpan;
            long sum = 0;
            for(int i = 0; i < averageCnt; i++)
            {
                sum += weights[i];
            }

            var avg = TimeSpan.FromTicks(sum / averageCnt);
            return avg;
        }
    }
}
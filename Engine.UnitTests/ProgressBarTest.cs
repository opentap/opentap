//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    public class ProgressBarTest : TestStep
    {
        public double Sleep { get; set; }
        public int Fraction { get; set; }
        public int Step { get; set; }
        public int Start { get; set; }
        public string PreMessage { get; set; }
        public ProgressBarTest()
        {
            Sleep = 0.1;
            Fraction = 100;
            Step = 1;
            Start = 0;
            PreMessage = "Progress";
        }
        public override void Run()
        {
            Random rand = new Random();
            var watch2 = System.Diagnostics.Stopwatch.StartNew();
            Log.Info("{2} [{0}/{1}]", 0, Fraction, PreMessage);
            for (int i = Start; i < Fraction; i += Step)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                TestPlan.Sleep((int)(1000 * Sleep + rand.Next() % 100));
                Log.Debug("{2} [{0}/{1}]", i, Fraction, PreMessage);
            }
            Log.Debug("{1} [{0}/{0}] Completed", Fraction, PreMessage);
            Log.Info(watch2, "Simulating progress...");
        }
    }
}

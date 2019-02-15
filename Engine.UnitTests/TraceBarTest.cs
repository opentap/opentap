//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Linq;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TraceBarTest
    {
        [Test]
        public void TraceBarSimpleValues()
        {
            var bar = new TraceBar();
            bar.UpperLimit = 5;
            bar.LowerLimit = -2.4;

            var bar1 = bar.GetBar(2.5);
            Assert.AreEqual(Verdict.Pass, bar.CombinedVerdict);
            Assert.IsTrue(bar1.Contains('|'));
            bar.GetBar(4);
            Assert.AreEqual(Verdict.Pass, bar.CombinedVerdict);
            bar.GetBar(-2.6);
            Assert.AreEqual(Verdict.Fail, bar.CombinedVerdict);
            bar.GetBar(-20);
            Assert.AreEqual(Verdict.Fail, bar.CombinedVerdict);
            var bar2 = bar.GetBar(-200000);
            Assert.IsTrue(bar2.Contains('<') && bar2.Contains("Fail"));
            Assert.AreEqual(Verdict.Fail, bar.CombinedVerdict);
            var bar3 = bar.GetBar(200000);
            Assert.IsTrue(bar3.Contains('>') && bar3.Contains("Fail"));
            Assert.AreEqual(Verdict.Fail, bar.CombinedVerdict);
        }

        [Test]
        public void TraceBarOutsiderValues()
        {
            double[] outliers = new Double[] { Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN };
            foreach (var outlier in outliers)
            {
                var bar = new TraceBar();
                bar.UpperLimit = 5;
                bar.LowerLimit = -2.3;
                bar.GetBar(2.5);
                Assert.AreEqual(Verdict.Pass, bar.CombinedVerdict);
                bar.GetBar(-1);
                Assert.AreEqual(Verdict.Pass, bar.CombinedVerdict);
                var barString = bar.GetBar(outlier);
                if (double.IsNaN(outlier))
                {
                    Assert.AreEqual(Verdict.Inconclusive, bar.CombinedVerdict);
                    StringAssert.Contains("Inconclusive", barString);
                }
                else
                {
                    Assert.AreEqual(Verdict.Fail, bar.CombinedVerdict);
                    StringAssert.Contains("Fail", barString);
                }
            }
        }

    }
}

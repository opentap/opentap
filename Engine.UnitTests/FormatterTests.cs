using System;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class FormatterTests
    {
        [Test]
        public void TimeSpanFormatterTests()
        {
            Assert.AreEqual("01:02:03", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3), FormatVerbosities.SuperBrief, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("01:02:03.123", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3) + TimeSpan.FromMilliseconds(123), FormatVerbosities.SuperBrief, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("2.04:02:03.456", TimeSpanFormatter.Format(new TimeSpan(4, 2, 3) + TimeSpan.FromDays(2) + TimeSpan.FromMilliseconds(456), FormatVerbosities.SuperBrief, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));

            Assert.AreEqual("1h2m3s", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3), FormatVerbosities.Brief, unitSpacer: false, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("1 h 2 m 3 s", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3), FormatVerbosities.Brief, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("1 h 2 m 3.456 s", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3) + TimeSpan.FromMilliseconds(456), FormatVerbosities.Brief, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));

            Assert.AreEqual("1 hour 2 min 3 sec", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3), FormatVerbosities.Normal, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("1 hour 2 min 3.456 sec", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3) + TimeSpan.FromMilliseconds(456), FormatVerbosities.Normal, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));

            Assert.AreEqual("1 hour 2 minutes 3 seconds", TimeSpanFormatter.Format(new TimeSpan(1, 2, 3), FormatVerbosities.Verbose, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));

            Assert.AreEqual("2 days 4 hours 2 minutes 3.456 seconds", TimeSpanFormatter.Format(new TimeSpan(4, 2, 3) + TimeSpan.FromDays(2) + TimeSpan.FromMilliseconds(456), FormatVerbosities.Verbose, unitSpacer: true, numberFormatCulture: System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

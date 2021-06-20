using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestStepRunTests
    {
        [Test]
        public void StartTimeDefault()
        {
            Assert.AreEqual(new DateTime(), new TestStepRun(new DelayStep(), Guid.NewGuid()).StartTime);
        }

        [Test]
        public void StartTimeInvalidValue()
        {
            var attachedParameters = new List<ResultParameter> { new ResultParameter("", "StartTime", null) };
            var testStepRun = new TestStepRun(new DelayStep(), Guid.NewGuid(), attachedParameters);
            Assert.AreEqual(new DateTime(), testStepRun.StartTime);
        }

        [Test]
        public void StartTimeExactDate()
        {
            var date = DateTime.ParseExact("01.01.2000 10:00", "dd.MM.yyyy hh:mm", CultureInfo.InvariantCulture);
            var attachedParameters = new List<ResultParameter> { new ResultParameter("", "StartTime", date) };
            var testStepRun = new TestStepRun(new DelayStep(), Guid.NewGuid(), attachedParameters);
            Assert.AreEqual(date, testStepRun.StartTime);
        }
    }
}
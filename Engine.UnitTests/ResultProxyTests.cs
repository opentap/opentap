//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OpenTap.EngineUnitTestUtils;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ResultProxyTests : EngineTestBase
    {
        
        public class DelegateTestStep : TestStep
        {
            [XmlIgnore]
            public Action<ResultSource> RunAction { get; set; }

            public override void Run()
            {
                RunAction(Results);
            }
        }

        [Test]
        public void LargeValueTest()
        {
            TestPlan plan = new TestPlan();
            DelegateTestStep step = new DelegateTestStep();
            step.RunAction = (Results) =>
            {
                Results.Publish("LargeValueType", new List<string> { "a", "b", "c", "d" }, 0, 9.91e37, 10, 0); // This is the large  value that X-Apps returns when there was no measurement.
            };
            plan.ChildTestSteps.Add(step);
            TestPlanRunner.RunTestPlan(plan);
        }

        [Test]
        public void NaNValueTest()
        {
            TestPlan plan = new TestPlan();
            DelegateTestStep step = new DelegateTestStep();
            step.RunAction = (Results) =>
            {
                Results.Publish("NaNValueType", new List<string> { "a", "b", "c", "d" }, 0, double.NaN, 10, 0);
            };
            plan.ChildTestSteps.Add(step);
            TestPlanRunner.RunTestPlan(plan);
        }

        [Display("My object result")]
        internal class MyResult
        {
            [Display("Testing")]
            public int A { get; set; }

            [Display("Value [dBm]")]
            public double b { get; set; }
        }

        internal class TempResultListener : ResultListener
        {
            public ResultTable LastResult;

            public override void OnResultPublished(Guid stepRunID, ResultTable result)
            {
                base.OnResultPublished(stepRunID, result);
                LastResult = result;
            }
        }

        [Test]
        public void ObjectResultTest()
        {
            TempResultListener rl = new TempResultListener();

            ResultSettings.Current.Add(rl);

            TestPlan plan = new TestPlan();
            DelegateTestStep step = new DelegateTestStep();
            step.RunAction = (Results) =>
            {
                Results.Publish(new MyResult { A = 123, b = -123e-1 });
            };
            plan.ChildTestSteps.Add(step);
            TestPlanRunner.RunTestPlan(plan);

            Assert.IsNotNull(rl.LastResult);
            Assert.AreEqual("My object result", rl.LastResult.Name);
            Assert.AreEqual(2, rl.LastResult.Columns.Length);
            Assert.AreEqual(1, rl.LastResult.Rows);
            Assert.AreEqual("Testing", rl.LastResult.Columns[0].Name);
            Assert.AreEqual("Value [dBm]", rl.LastResult.Columns[1].Name);

            Assert.AreEqual(TypeCode.Int32, rl.LastResult.Columns[0].TypeCode);
            Assert.AreEqual(TypeCode.Double, rl.LastResult.Columns[1].TypeCode);

            Assert.AreEqual(123, rl.LastResult.Columns[0].GetValue<Int32>(0));
            Assert.AreEqual(-123e-1, rl.LastResult.Columns[1].GetValue<double>(0));

            ResultSettings.Current.Remove(rl);
        }

        [Test]
        public void AnonymousObjectResultTest()
        {
            TempResultListener rl = new TempResultListener();

            ResultSettings.Current.Add(rl);

            TestPlan plan = new TestPlan();
            DelegateTestStep step = new DelegateTestStep();
            step.RunAction = (Results) =>
            {
                Results.Publish("My object result", new { A = 123, b = -123e-1 });
            };
            plan.ChildTestSteps.Add(step);
            TestPlanRunner.RunTestPlan(plan);

            Assert.IsNotNull(rl.LastResult);
            Assert.AreEqual("My object result", rl.LastResult.Name);
            Assert.AreEqual(2, rl.LastResult.Columns.Length);
            Assert.AreEqual(1, rl.LastResult.Rows);
            Assert.AreEqual("A", rl.LastResult.Columns[0].Name);
            Assert.AreEqual("b", rl.LastResult.Columns[1].Name);

            Assert.AreEqual(TypeCode.Int32, rl.LastResult.Columns[0].TypeCode);
            Assert.AreEqual(TypeCode.Double, rl.LastResult.Columns[1].TypeCode);

            Assert.AreEqual(123, rl.LastResult.Columns[0].GetValue<Int32>(0));
            Assert.AreEqual(-123e-1, rl.LastResult.Columns[1].GetValue<double>(0));

            ResultSettings.Current.Remove(rl);
        }

        private class ResultValidator : ResultListener
        {
            public List<ResultTable> Results { get; set; }

            public ResultValidator()
            {
                Results = new List<ResultTable>();
            }

            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                Results.Add(result);
            }
        }

        [Test]
        public void ResultsProxyTest()
        {
            TestPlan tp = new TestPlan();
            tp.ChildTestSteps.Add(new DelegateTestStep
            {
                RunAction = (r) =>
                {
                    var rand = new Random();

                    IConvertible[] values = new IConvertible[3];
                    values[0] = rand.NextDouble();
                    values[1] = rand.NextDouble();
                    values[2] = rand.NextDouble();

                    r.Publish("Publish1", new List<string> { "a", "b", "c" }, values);
                    r.Publish("Publish2", new List<string> { "a", "b", "c" }, values);
                    r.PublishTable("PublishTable", new List<string> { "a", "b", "c" }, values.Select(v => new IConvertible[1] { v }).ToArray());

                    values[0] = rand.NextDouble();
                    values[1] = rand.NextDouble();
                    values[2] = rand.NextDouble();

                    values[0] = "abc";
                    values[1] = "rand";
                    values[2] = "rand.NextDouble()";
                    r.Publish("PublishStrings", new List<string> { "a", "b", "c" }, values);

                    values[0] = null;
                    values[1] = "rand";
                    values[2] = true;
                    r.Publish("PublishMixed", new List<string> { "a", "b", "c" }, values);
                }
            });

            var rl = new ResultValidator();
            tp.Execute(new List<ResultListener> { rl });

            Assert.AreEqual(5, rl.Results.Count, "Number of results");
            foreach (var res in rl.Results)
            {
                Assert.IsTrue(res.Name.StartsWith("Publish"), "Name of result");
                Assert.AreEqual(3, res.Columns.Length, "Number of columns in " + res.Name);

                Assert.AreEqual(1, res.Columns[0].Data.Length);
                Assert.AreEqual(1, res.Columns[1].Data.Length);
                Assert.AreEqual(1, res.Columns[2].Data.Length);

                Assert.AreEqual("a", res.Columns[0].Name);
                Assert.AreEqual("b", res.Columns[1].Name);
                Assert.AreEqual("c", res.Columns[2].Name);
            }
        }
    }
}

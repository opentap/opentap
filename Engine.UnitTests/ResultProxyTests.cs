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
    public class ResultProxyTests 
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

        public class ResultObjectBase
        {
            public double Freq { get; set; }
            public double Power { get; set; }
        }

        public class EvmResultObject : ResultObjectBase
        {
            public double Evm { get; set; }
        }


        [Test]
        public void DerivedObjectResultTest()
        {
            TempResultListener rl = new TempResultListener();

            ResultSettings.Current.Add(rl);

            TestPlan plan = new TestPlan();
            DelegateTestStep step = new DelegateTestStep();
            ResultObjectBase result = new EvmResultObject() { Freq = 1, Power = 2, Evm = 3 };
            step.RunAction = (Results) =>
            {
                Results.Publish(result);
            };
            plan.ChildTestSteps.Add(step);
            TestPlanRunner.RunTestPlan(plan);

            Assert.IsNotNull(rl.LastResult);
            Assert.AreEqual("EvmResultObject", rl.LastResult.Name);
            Assert.AreEqual(3, rl.LastResult.Columns.Length);
            Assert.AreEqual(1, rl.LastResult.Rows);
            Assert.AreEqual("Evm", rl.LastResult.Columns[0].Name);

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
            public List<ResultTable> Results { get; } = new List<ResultTable>();
            public List<(ResultTable,TestStepRun)> Results2 { get; } = new List<(ResultTable, TestStepRun)>();


            Dictionary<Guid, TestStepRun> stepRuns = new Dictionary<Guid, TestStepRun>();
            public override void OnTestStepRunStart(TestStepRun stepRun)
            {
                stepRuns[stepRun.Id] = stepRun;
            }

            public override void OnTestStepRunCompleted(TestStepRun stepRun)
            {
                stepRuns.Remove(stepRun.Id);
            }


            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                Results.Add(result);
                Results2.Add((result,stepRuns[stepRunId]));
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

        public class TestCls<T>
        {
            public class TestCls2<T2>
            {
                public T X { get; set; }
                public T2 Y { get; set; }
            }

            public T X { get; set; }
        }

        [Test]
        public void ResultTableName()
        {
            var rl = new ResultValidator();
            TestPlan tp = new TestPlan();
            tp.ChildTestSteps.Add(new DelegateTestStep
            {
                RunAction = (r) =>
                {
                    r.Publish(new TestCls<double>{ X = 1.0 });
                    r.Publish(new TestCls<double>.TestCls2<int> { X = 1.0, Y = 1 });
                }
            });
            var run = tp.Execute(new[] { rl });
            Assert.AreEqual("TestCls`1", rl.Results.First().Name);
            Assert.AreEqual("TestCls2`1", rl.Results.Last().Name);
            bool anyNullGroup = run.Parameters.Any(x => x.Group == null);
            Assert.IsFalse(anyNullGroup);
        }

        public class CheckNullGroupStep : TestStep
        {
            public override void Run()
            {
                bool anyNull = StepRun.Parameters.Any(x => x.Group == null);
                if (anyNull)
                    UpgradeVerdict(Verdict.Fail);
                else
                    UpgradeVerdict(Verdict.Pass);
            }
        }

        [Test]
        public void NoNullResultGroups()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new CheckNullGroupStep());
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
            Assert.IsTrue(run.Parameters.All(x => x.Group != null));
        }

        public class AdvancedResultsStep : TestStep
        {
            public class ResultCommentAttribute : Attribute, IParameter
            {
                public string Name => "Comment";

                public string ObjectType => "string";

                public string Group => "";

                public IConvertible Value { get; }
                public ResultCommentAttribute(string comment) => Value = comment;
            }
            
            [ResultComment("ResultCommentTest")]
            [Display("XY")]
            public class AdvancedResult
            {
                [Unit("s")]
                [Display("X", "This axis denotes some time spent.")]
                public double X { get; set; }
                [Unit("W")]
                [Display("Y", "This axis denotes some power.")]
                [ResultComment("ResultCommentTest2")]
                public double Y { get; set; }

                public static AdvancedResult New(double x, double y)
                {
                    return new AdvancedResult() {X = x, Y = y};
                }
            }
            public override void Run()
            {
                var c1 = new ResultColumn("X", Enumerable.Range(0, 10).ToArray(), new UnitAttribute("s"));
                var c2 = new ResultColumn("Y", Enumerable.Range(0, 10).ToArray(), new UnitAttribute("W"));
                var results = new ResultTable("XY", new []{c1, c2}, new []{new ResultParameter("Comment", "Test Result")});
                
                Results.Publish(results);
                Results.Publish(Enumerable.Range(0, 10).Select( x => AdvancedResult.New(x,x)));
            }
        }

        [Test]
        public void ResultTableAndColumnParameters()
        {
            var step = new AdvancedResultsStep();
            var plan = new TestPlan();
            plan.Steps.Add(step);
            var rl = new ResultValidator();
            plan.Execute(new[] {rl});

            void validatedResult(ResultTable t, string comment)
            {
                Assert.AreEqual(comment, t.Parameters["Comment"]);
                Assert.AreEqual("XY", t.Name);
                Assert.AreEqual(2, t.Columns.Length);
                Assert.AreEqual(10, t.Rows);
                var x = t.Columns[0];
                var y = t.Columns[1];

                Assert.AreEqual("X", x.Name);
                Assert.AreEqual("Y", y.Name);
                Assert.AreEqual(1, x.Data.GetValue(1));
                Assert.AreEqual(9, y.Data.GetValue(9));
                Assert.AreEqual("s", x.Parameters["Unit"]);
                Assert.AreEqual("W",y.Parameters["Unit"]);
                Assert.AreEqual("s", x.Parameters["OpenTap.Unit"]);
                
            }
            
            Assert.AreEqual(2, rl.Results2.Count);
            
            //verify that the same results were generated in two different ways
            validatedResult(rl.Results[0], "Test Result");
            validatedResult(rl.Results[1], "ResultCommentTest");
        }

        [Test]
        public void ResultOptimizerTest()
        {
            ResultColumn createColumn<T>(string name)
            {
                return new ResultColumn("x", new T[2]);
            }

            {
                var rc1x = createColumn<int>("X");
                var rc1y = createColumn<int>("Y");
                var rt1 = new ResultTable("XY", new[] {rc1x, rc1y});

                var rc2x =  createColumn<int>("X");
                var rc2y = createColumn<int>("X");
                var rt2 = new ResultTable("XY", new[] {rc2x, rc2y});
                Assert.IsTrue(ResultTableOptimizer.CanMerge(rt1, rt2));
                var merged = ResultTableOptimizer.MergeTables(new []{rt1, rt2});
                Assert.AreEqual(4, merged.Columns[0].Data.Length);
            }
            {
                var rc1x = createColumn<int>("X");
                var rc1y = createColumn<int>("Y");
                var rt1 = new ResultTable("XY", new[] {rc1x, rc1y});

                var rc2x =  createColumn<int>("X");
                var rc2y = createColumn<int>("X");
                rc2y = rc2y.AddParameters(new ResultParameter("Unit", "X"));
                var rt2 = new ResultTable("XY", new[] {rc2x, rc2y});
                Assert.IsTrue(ResultTableOptimizer.CanMerge(rt1, rt2) == false);
            }
        }
    }
}

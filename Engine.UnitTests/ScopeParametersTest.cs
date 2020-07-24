using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ScopeParametersTest
    {

        [Test]
        public void ScopeStepTest()
        {

            var diag = new DialogStep() {UseTimeout = true};
            var diag2 = new DialogStep();
            var scope = new SequenceStep();
            string parameterName = "Scope\"" + DisplayAttribute.GroupSeparator + "Title"; // name intentionally weird to mess with the serializer.
            scope.ChildTestSteps.Add(diag);
            scope.ChildTestSteps.Add(diag2);
            var member = TypeData.GetTypeData(diag).GetMember("Title");
            member.Parameterize(scope, diag, parameterName);
            member.Parameterize(scope, diag2, parameterName);
            TypeData.GetTypeData(diag).GetMember("Timeout").Parameterize(scope, diag, "Group\\The Timeout");

            var annotation = AnnotationCollection.Annotate(scope);
            var titleMember = annotation.GetMember(parameterName);
            titleMember.Get<IStringValueAnnotation>().Value = "New title";
            annotation.Write();
            Assert.AreEqual("New title", diag.Title);
            Assert.AreEqual("New title", diag2.Title);
            
            var timeoutMember = annotation.GetMember("Group\\The Timeout");
            Assert.IsFalse(timeoutMember.Get<IAccessAnnotation>().IsReadOnly);
            Assert.AreEqual("Group", TypeData.GetTypeData(scope).GetMember("Group\\The Timeout").GetDisplayAttribute().Group[0]);

            var plan = new TestPlan();
            plan.Steps.Add(scope);
            var str = new TapSerializer().SerializeToString(plan);
            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var scope2 = plan2.Steps[0];
            var annotation2 = AnnotationCollection.Annotate(scope2);
            var titleMember2 = annotation2.GetMember(parameterName);
            Assert.IsNotNull(titleMember2);
            titleMember2.Get<IStringValueAnnotation>().Value = "New Title 2";
            annotation2.Write();
            foreach (var step in scope2.ChildTestSteps.Cast<DialogStep>())
            {
                Assert.AreEqual(step.Title, "New Title 2");
            }

            var forwardedMember = (ParameterMemberData)TypeData.GetTypeData(scope2).GetMember(parameterName);
            Assert.IsNotNull(forwardedMember);

            member.Unparameterize(forwardedMember, scope2.ChildTestSteps[0]);
            Assert.IsNotNull(TypeData.GetTypeData(scope2).GetMember(parameterName));
            member.Unparameterize(forwardedMember, scope2.ChildTestSteps[1]);
            Assert.IsNull(TypeData.GetTypeData(scope2).GetMember(parameterName)); // last 'Title' removed.
        }

        [Test]
        public void MultiLevelScopeSerialization()
        {
            var plan = new TestPlan();
            var seq1 = new SequenceStep();
            var seq2 = new SequenceStep();
            var delay = new DelayStep();
            plan.ChildTestSteps.Add(seq1);
            seq1.ChildTestSteps.Add(seq2);
            seq2.ChildTestSteps.Add(delay);

            var member1 = TypeData.GetTypeData(delay).GetMember(nameof(DelayStep.DelaySecs))
                          .Parameterize(seq2, delay, "delay");
            member1.Parameterize(seq1, seq2, "delay");
            var str = new TapSerializer().SerializeToString(plan);

            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var member2 = TypeData.GetTypeData(plan2.ChildTestSteps[0]).GetMember(member1.Name);
            var val = member2.GetValue(plan2.ChildTestSteps[0]);
            Assert.AreEqual(delay.DelaySecs, val);
        }

        [Test]
        public void CyclicScopeTest()
        {
            var seq = new SequenceStep();
            var delay = new DelayStep()
            {
                DelaySecs = 1.5
            };
            seq.ChildTestSteps.Add(delay);

            var member = TypeData.GetTypeData(delay).GetMember("Time Delay");

             member.Parameterize(seq, delay, "something");
            
            var value = AnnotationCollection.Annotate(delay).GetMember("DelaySecs").Get<IObjectValueAnnotation>();
            var value2 = AnnotationCollection.Annotate(seq).GetMember("something").Get<IObjectValueAnnotation>();

            try
            {
                member.Parameterize(delay, seq, "something");
                Assert.Fail("Parameterize should have thrown an exception.");
            }
            catch (ArgumentException)
            {
                
            }

            // Stack overflow...
            value = AnnotationCollection.Annotate(delay).GetMember("DelaySecs").Get<IObjectValueAnnotation>();
            value2 = AnnotationCollection.Annotate(seq).GetMember("something").Get<IObjectValueAnnotation>();
            Assert.IsNotNull(value);
            Assert.IsNotNull(value2);
        }
        
        public class ScopeTestStep : TestStep{
            public int A { get; set; }
            public List<int> Collection = new List<int>();
            public Enabled<double> EnabledTest { get; set; } = new Enabled<double>();
            public override void Run()
            {
                Collection.Add(A);
                UpgradeVerdict(Verdict.Pass);
                OnPropertyChanged("");
            }
        }

        [Test]
        public void SweepLoopDisabledMembersOnMultiSelect()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterRangeStep();
            var sweep2 = new SweepParameterRangeStep();
            var numberstep = new ScopeTestStep();
            var numberstep2 = new ScopeTestStep();
            plan.ChildTestSteps.Add(sweep);
            plan.ChildTestSteps.Add(sweep2);
            sweep.ChildTestSteps.Add(numberstep);
            sweep2.ChildTestSteps.Add(numberstep2);
            var member = TypeData.GetTypeData(numberstep).GetMember("A");
            member.Parameterize(sweep, numberstep, "A");
            member.Parameterize(sweep2, numberstep2, "A");
            sweep.SelectedParameters = Enumerable.Empty<ParameterMemberData>().ToList();
            Assert.AreEqual(0, sweep.SelectedParameters.Count());
            {
                var a = AnnotationCollection.Annotate(sweep);
                var m = a.GetMember(nameof(SweepParameterRangeStep.SelectedParameters));
                var sweptMember = a.GetMember("A");
                Assert.IsTrue(sweptMember.Get<IEnabledAnnotation>().IsEnabled);
                var ms = m.Get<IMultiSelectAnnotationProxy>();
                var avail = m.Get<IAvailableValuesAnnotationProxy>();
                ms.SelectedValues = avail.AvailableValues;
                a.Write();
                sweptMember = a.GetMember("A");
                Assert.IsFalse(sweptMember.Get<IEnabledAnnotation>().IsEnabled);
            }
            {
                var a = AnnotationCollection.Annotate(new object[] {sweep, sweep2});
                var amem = a.GetMember("A");
                var ienabled = amem.Get<IEnabledAnnotation>();
                Assert.IsFalse(ienabled.IsEnabled);
            }
        }

        [Test]
        public void SweepLoopRange2Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterRangeStep();
            var numberstep = new ScopeTestStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(numberstep);
            var member = TypeData.GetTypeData(numberstep).GetMember("A");
            member.Parameterize(sweep, numberstep, "A");
            sweep.SelectedParameters = Enumerable.Empty<ParameterMemberData>().ToList();
            Assert.AreEqual(0, sweep.SelectedParameters.Count());
            {
                var a = AnnotationCollection.Annotate(sweep);
                var m = a.GetMember(nameof(SweepParameterRangeStep.SelectedParameters));
                var sweptMember = a.GetMember("A");
                Assert.IsTrue(sweptMember.Get<IEnabledAnnotation>().IsEnabled);
                var ms = m.Get<IMultiSelectAnnotationProxy>();
                var avail = m.Get<IAvailableValuesAnnotationProxy>();
                ms.SelectedValues = avail.AvailableValues;
                a.Write();
                sweptMember = a.GetMember("A");
                Assert.IsFalse(sweptMember.Get<IEnabledAnnotation>().IsEnabled);
            }
            
            Assert.AreEqual(1, sweep.SelectedParameters.Count());
            
            
            sweep.SweepStart = 1;
            sweep.SweepEnd = 10;
            sweep.SweepPoints = 10;

            Assert.IsTrue(string.IsNullOrEmpty(sweep.Error));
            plan.Execute();

            Assert.IsTrue(Enumerable.Range(1,10).SequenceEqual(numberstep.Collection));

            {
                
                var sweep2 = new SweepLoopRange();
                plan.ChildTestSteps.Add(sweep2);
                
                // verify that sweep Behavior selected value can be displayed.
                var annotation = AnnotationCollection.Annotate(sweep);
                var mem = annotation.GetMember(nameof(SweepParameterRangeStep.SweepBehavior));
                var proxy = mem.Get<IAvailableValuesAnnotationProxy>();
                var selectedBehavior = proxy.SelectedValue.Get<IStringReadOnlyValueAnnotation>();
                Assert.AreEqual("Linear", selectedBehavior.Value);
                
            }
        }

        [Test]
        public void SweepLoop2Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var step = new ScopeTestStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);
           
            
            sweep.SweepValues.Add(new SweepRow());
            sweep.SweepValues.Add(new SweepRow());

            TypeData.GetTypeData(step).GetMember(nameof(ScopeTestStep.A)).Parameterize(sweep, step, "Parameters \\ A");
            TypeData.GetTypeData(step).GetMember(nameof(ScopeTestStep.EnabledTest)).Parameterize(sweep, step, nameof(ScopeTestStep.EnabledTest));

            
            
            var td1 = TypeData.GetTypeData(sweep.SweepValues[0]);
            var memberA = td1.GetMember("Parameters \\ A");
            memberA.SetValue(sweep.SweepValues[0], 10);
            memberA.SetValue(sweep.SweepValues[1], 20);

            {
                // verify Enabled<T> works with SweepParameterStep.
                var annotation = AnnotationCollection.Annotate(sweep);
                var col = annotation.GetMember(nameof(SweepParameterStep.SelectedParameters)).Get<IStringReadOnlyValueAnnotation>().Value;
                Assert.AreEqual("A, EnabledTest", col);
                var elements = annotation.GetMember(nameof(SweepParameterStep.SweepValues))
                    .Get<ICollectionAnnotation>().AnnotatedElements
                    .Select(elem => elem.GetMember(nameof(ScopeTestStep.EnabledTest)))
                    .ToArray();
                annotation.Write();
                Assert.IsFalse((bool) elements[0].GetMember("IsEnabled").Get<IObjectValueAnnotation>().Value);
                elements[0].GetMember("IsEnabled").Get<IObjectValueAnnotation>().Value = true;
                annotation.Write();
                Assert.IsFalse((bool) elements[1].GetMember("IsEnabled").Get<IObjectValueAnnotation>().Value);
                Assert.IsTrue((bool) elements[0].GetMember("IsEnabled").Get<IObjectValueAnnotation>().Value);
            }

            var str = new TapSerializer().SerializeToString(plan);
            var plan2 = (TestPlan)new TapSerializer().DeserializeFromString(str);
            var sweep2 = (SweepParameterStep) plan2.Steps[0];
            var td2 = TypeData.GetTypeData(sweep2);
            var members2 = td2.GetMembers();
            var rows = sweep2.SweepValues;
            Assert.AreEqual(2, rows.Count);
            var msgmem = TypeData.GetTypeData(rows[0]).GetMember("Parameters \\ A");
            Assert.AreEqual(10, msgmem.GetValue(rows[0]));

            // this feature was disabled.
            //var annotated = AnnotationCollection.Annotate(sweep2);
            //var messageMember = annotated.GetMember(nameof(ScopeTestStep.A));
            //Assert.IsFalse(messageMember.Get<IEnabledAnnotation>().IsEnabled);

            var run = plan2.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);

            Assert.IsTrue(((ScopeTestStep)sweep2.ChildTestSteps[0]).Collection.SequenceEqual(new[] {10, 20}));

            var name = sweep.GetFormattedName();
            Assert.AreEqual("Sweep A, EnabledTest", name);
        }

        [Test]
        public void ScopedInputAnnotationTest()
        {
            var seqStep = new SequenceStep();
            var verdictStep = new VerdictStep();
            seqStep.ChildTestSteps.Add(verdictStep);
            var ifStep = new IfStep();
            
            seqStep.ChildTestSteps.Add(ifStep);
            var member = TypeData.GetTypeData(ifStep).GetMember(nameof(IfStep.InputVerdict));
            var parameterizedMember = member.Parameterize(seqStep, ifStep, member.Name);

            var annotation = AnnotationCollection.Annotate(seqStep);
            var memberAnnotation = annotation.GetMember(parameterizedMember.Name);
            var avail = memberAnnotation.Get<IAvailableValuesAnnotation>();
            Assert.IsNotNull(avail);
            
            // available values: None, verdict from itself, verdict from SetVerdict. 
            
            Assert.AreEqual(3, avail.AvailableValues.Cast<object>().Count());
            var strings = avail.AvailableValues.Cast<object>().Select(x => x.ToString()).ToArray();
            Assert.IsTrue(strings.Contains($"Verdict from {ifStep.GetFormattedName()}"));
            Assert.IsTrue(strings.Contains("None"));
            Assert.IsTrue(strings.Contains($"Verdict from {verdictStep.GetFormattedName()}"));
        }
        
        [Test]
        public void ScopedInputAnnotationWithSweepTest()
        {
            var sweepStep = new SweepParameterStep();
            var verdictStep = new VerdictStep();
            sweepStep.ChildTestSteps.Add(verdictStep);
            var ifStep = new IfStep();
            
            sweepStep.ChildTestSteps.Add(ifStep);
            var member = TypeData.GetTypeData(ifStep).GetMember(nameof(IfStep.InputVerdict));
            var parameterizedMember = member.Parameterize(sweepStep, ifStep, member.Name);

            var annotation = AnnotationCollection.Annotate(sweepStep);
            var memberAnnotation = annotation.GetMember(nameof(sweepStep.SweepValues));
            var col = memberAnnotation.Get<ICollectionAnnotation>();
            col.AnnotatedElements = new[] {col.NewElement()};
            annotation.Write();
            annotation.Read();
            var member2Annotation = col.AnnotatedElements.FirstOrDefault().GetMember(parameterizedMember.Name);
            var avail = member2Annotation.Get<IAvailableValuesAnnotation>();
            Assert.IsNotNull(avail);
            
            // available values: None, verdict from itself, verdict from SetVerdict. 
            
            Assert.AreEqual(3, avail.AvailableValues.Cast<object>().Count());
            var strings = avail.AvailableValues.Cast<object>().Select(x => x.ToString()).ToArray();
            Assert.IsTrue(strings.Contains($"Verdict from {ifStep.GetFormattedName()}"));
            Assert.IsTrue(strings.Contains("None"));
            Assert.IsTrue(strings.Contains($"Verdict from {verdictStep.GetFormattedName()}"));
        }
        
        [Test]
        public void ScopedInputAnnotationWithSweepTestSerialized()
        {
            var plan = new TestPlan();
            var sweepStep = new SweepParameterStep();
            plan.Steps.Add(sweepStep);
            var verdictStep = new VerdictStep();
            sweepStep.ChildTestSteps.Add(verdictStep);
            var ifStep = new IfStep();
            
            sweepStep.ChildTestSteps.Add(ifStep);
            var member = TypeData.GetTypeData(ifStep).GetMember(nameof(IfStep.InputVerdict));
            var parameterizedMember = member.Parameterize(sweepStep, ifStep, member.Name);

            var annotation = AnnotationCollection.Annotate(sweepStep);
            var memberAnnotation = annotation.GetMember(nameof(sweepStep.SweepValues));
            var col = memberAnnotation.Get<ICollectionAnnotation>();
            col.AnnotatedElements = new[] {col.NewElement()};
            annotation.Write();
            annotation.Read();
            var sweepValuesMember = TypeData.GetTypeData(sweepStep).GetMember(nameof(sweepStep.SweepValues));
            sweepValuesMember.Parameterize(plan, sweepStep, "SweepValues");
            var planstr = plan.SerializeToString();
            var plan2 = Utils.DeserializeFromString<TestPlan>(planstr);
            var ext = plan2.ExternalParameters.Get("SweepValues");
            Assert.IsNotNull(ext);

        }

        [Test]
        public void SweepLoopNoValuesSelected()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var step = new DelayStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);

            Assert.Throws(typeof(InvalidOperationException), sweep.PrePlanRun);

            // Select parameter to sweep
            TypeData.GetTypeData(step).GetMember(nameof(DelayStep.DelaySecs)).Parameterize(sweep, step, nameof(DelayStep.DelaySecs));
            try
            {
                sweep.PrePlanRun();
                Assert.Fail("An exception should have been thrown.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(ex.Message, "No values selected to sweep");
            }

        }
    }
}
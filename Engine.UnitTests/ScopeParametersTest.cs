using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ScopeParametersTest
    {
        [Test]
        public void TestIsParameterized()
        {
            var plan = new TestPlan();
            var diag = new DialogStep() {UseTimeout = true};
            var diag2 = new DialogStep();
            var scope = new SequenceStep();
            plan.ChildTestSteps.Add(scope);
            string parameterName = "Scope\"" + DisplayAttribute.GroupSeparator + "Title"; // name intentionally weird to mess with the serializer.
            scope.ChildTestSteps.Add(diag);
            scope.ChildTestSteps.Add(diag2);
            var member = TypeData.GetTypeData(diag).GetMember("Title");
            Assert.IsFalse(DynamicMember.IsParameterized(member, diag));
            Assert.IsFalse(DynamicMember.IsParameterized(member, diag2));
            
            var param = member.Parameterize(scope, diag, parameterName);
            member.Parameterize(scope, diag2, parameterName);
            Assert.IsTrue(DynamicMember.IsParameterized(member, diag));
            Assert.IsTrue(DynamicMember.IsParameterized(member, diag2));
            plan.ChildTestSteps.Remove(scope);
            TypeData.GetTypeData(scope).GetMembers();
            DynamicMember.UnparameterizeMember(param, member, diag);
        }

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
        public void ParallelAddParametersTest()
        {
            // verify that we can add parameters while somebody else is iterating them.
            // this issue only previously occured if the number of parameters in the same collection was 
            // greater than 1.
            var diag = new DialogStep() {UseTimeout = true};
            var diag2 = new DialogStep();
            var diag3 = new DialogStep();
            var scope = new SequenceStep();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(scope);
            string parameterName = "param1";
            scope.ChildTestSteps.Add(diag);
            scope.ChildTestSteps.Add(diag2);
            scope.ChildTestSteps.Add(diag3);
            
            var member = TypeData.GetTypeData(diag).GetMember("Title");
            var p1 = member.Parameterize(scope, diag, parameterName);
            member.Parameterize(scope, diag2, parameterName);
            int count = 0;
            foreach (var _ in p1.ParameterizedMembers)
            {
                count++;
                if (count == 2)
                    member.Parameterize(scope, diag3, parameterName);
            }
            Assert.AreEqual(2, count);
            
            count = 0;
            foreach (var _ in p1.ParameterizedMembers)
                count++;
            Assert.AreEqual(3, count);
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

            var member = TypeData.GetTypeData(delay).GetMember("DelaySecs");

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
        public void SweepLoopRange3Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterRangeStep();
            plan.ChildTestSteps.Add(sweep);
            
            var a = AnnotationCollection.Annotate(sweep).GetMember(nameof(sweep.SweepStep));
            var menu = a.Get<MenuAnnotation>();
            Assert.IsNotNull(menu);
            var icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
            var parameterize = icons[IconNames.ParameterizeOnTestPlan].First();

            Assert.IsTrue(parameterize.Get<EnabledIfAnnotation>().IsEnabled);
            Assert.AreEqual(0, plan.ExternalParameters.Entries.Count);
            parameterize.Get<IMethodAnnotation>().Invoke();
            Assert.AreEqual(1, plan.ExternalParameters.Entries.Count);

            sweep.SweepBehavior = SweepBehavior.Linear;
            sweep.SweepStart = 1;
            sweep.SweepEnd = 101;

            var param = plan.ExternalParameters.Entries.First();
            { // Parameterized value updates SweepPoints
                param.Value = 1;
                Assert.AreEqual(101, sweep.SweepPoints);
                param.Value = 2;
                Assert.AreEqual(51, sweep.SweepPoints);
                param.Value = 4;
                Assert.AreEqual(26, sweep.SweepPoints);
                param.Value = 8;
                Assert.AreEqual(13, sweep.SweepPoints);
            }

            { // SweepPoints updates parameterized value
                sweep.SweepPoints = 2;
                Assert.AreEqual(100, param.Value);
                sweep.SweepPoints = 3;
                Assert.AreEqual(50, param.Value);
                sweep.SweepPoints = 5;
                Assert.AreEqual(25, param.Value);
                sweep.SweepPoints = 6;
                Assert.AreEqual(20, param.Value);
            }
        }
        
        [Test]
        public void SweepLoopRange4Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterRangeStep();
            var seq = new SequenceStep();
            var delay = new DelayStep();
            plan.ChildTestSteps.Add(seq);
            seq.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(delay);
            
            var a = AnnotationCollection.Annotate(delay).GetMember(nameof(delay.DelaySecs));
            var menu = a.Get<MenuAnnotation>();
            var icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
            var parameterize = icons[IconNames.ParameterizeOnParent].First();
            parameterize.Get<IMethodAnnotation>().Invoke();
            
            a = AnnotationCollection.Annotate(sweep).GetMember("Parameters \\ Time Delay");
            menu = a.Get<MenuAnnotation>();
            icons = menu.MenuItems.ToLookup(x => x.Get<IIconAnnotation>()?.IconName ?? "");
            
            // It should not be possible to parameterize (parameterized) properties which are selected for sweeping.
            var names = new [] {IconNames.ParameterizeOnTestPlan, IconNames.Parameterize, IconNames.ParameterizeOnParent };
            foreach (var name in names)
            {
                parameterize = icons[name].First();
                Assert.IsTrue(parameterize.GetAll<IEnabledAnnotation>().Any(x => x.IsEnabled == false));
            }

            sweep.SelectedParameters.Clear();
            a.Read();
            
            // It should be possible to parameterize (parameterized) properties which are not selected for sweeping.
            foreach (var name in names)
            {
                parameterize = icons[name].First();
                Assert.IsFalse(parameterize.GetAll<IEnabledAnnotation>().Any(x => x.IsEnabled == false));
            }

            // Now parameterize it (sweep -> sequence step).
            icons[IconNames.ParameterizeOnParent].First().Get<IMethodAnnotation>().Invoke();
            
            // It should not be possible to select a parameterized property to be swept.
            Assert.IsTrue(!sweep.AvailableParameters.Any());
        }

        [Test]
        public void SweepLoop2Test()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var step = new ScopeTestStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);
           
            
            sweep.SweepValues.Add(new SweepRow(sweep));
            sweep.SweepValues.Add(new SweepRow(sweep));

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
                // The order is not deterministic because the implementation is using a dictionary and looping over the content.
                // The string hashing function used by dotnet can vary across processes, which affects the order.
                // It may be confusing to users that restarting an application causes the displayed parameters to change,
                // but this is a cosmetic issue. We can consider changing the underlying implementation to ensure the list
                // remains sorted on insertion. I have opted not to change the implementation at this time because the
                // list implementation is directly exposed to the user with methods such as Insert(index, parameter)
                // which would be impossible to honor if we sort the list anyway. The way the comma separated string
                // is actually generated is through a MemberDataSequenceStringAnnotation which is widely used, so ordering
                // the members alphabetically in that implementation is also a no-go since ordering could be important in other contexts.
                // The least destructive way would be to use a separate annotator for this particular implementation, which seems excessive for a cosmetic issue.
                Assert.That(col, Is.EqualTo("A, EnabledTest").Or.EqualTo("EnabledTest, A"));
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
            Assert.That(name, Is.EqualTo("Sweep A, EnabledTest").Or.EqualTo("Sweep EnabledTest, A"));

            { // Testing that sweep parameters are automatically removed after unparameterization.
                var p = (ParameterMemberData) TypeData.GetTypeData(sweep2).GetMember("Parameters \\ A");
                p.ParameterizedMembers.First().Member.Unparameterize(p, p.ParameterizedMembers.First().Source);
                Assert.AreEqual(2, sweep2.SweepValues[0].Values.Count);
                sweep2.Error.ToString(); // getting the error causes validation to be done.
                Assert.AreEqual(1, sweep2.SweepValues[0].Values.Count);
            }
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

        public class NullPropertyStep : TestStep
        {
            public string NormallyNull { get; set; }
            public override void Run() => UpgradeVerdict(Verdict.Pass);
        }

        [Test] 
        public void SweepLoopNullValueProperty()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var step = new NullPropertyStep();
            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);
            
            TypeData.GetTypeData(step).GetMember(nameof(step.NormallyNull)).Parameterize(sweep, step, nameof(step.NormallyNull));
            
            var row = new SweepRow(sweep);
            row.Values[nameof(step.NormallyNull)] = null;
            sweep.SweepValues.Add(new SweepRow(sweep));

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }


        public class Unclonable
        {
            public int Value { get; set; }
            public Unclonable(int value) => Value = value;
        }
        
        public class UnmergableValueTest : TestStep
        {
            public Unclonable A { get; set; } = new Unclonable(5);
            public override void Run()
            {
                
            }
        }
        
        [Test]
        public void MergeableScopeTest()
        {
            var plan = new TestPlan();
            var step1 = new UnmergableValueTest();
            var step2 = new UnmergableValueTest();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);

            foreach(var s in new []{step1, step2})                
            {
                var a = AnnotationCollection.Annotate(s)
                    .GetMember(nameof(step1.A))
                    .Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                        x.Get<IconAnnotationAttribute>()?.IconName == IconNames.ParameterizeOnTestPlan);
                var enabled = a.Get<IEnabledAnnotation>().IsEnabled;
                if (s == step1)
                {
                    Assert.IsTrue(enabled);
                    a.Get<IMethodAnnotation>()?.Invoke();
                }
                else
                    Assert.IsFalse(enabled);
            }
        }
        
        [Test]
        public void MergeableScopeTest2()
        {
            var plan = new TestPlan();
            var step1 = new SweepParameterStep();
            var step2 = new SweepParameterStep();
            plan.ChildTestSteps.Add(step1);
            plan.ChildTestSteps.Add(step2);

            var delay1 = new DelayStep();
            var delay2 = new DelayStep();
            step1.ChildTestSteps.Add(delay1);
            step2.ChildTestSteps.Add(delay2);
            TypeData.GetTypeData(delay1).GetMember(nameof(delay1.DelaySecs)).Parameterize(step1, delay1, "A");
            TypeData.GetTypeData(delay2).GetMember(nameof(delay2.DelaySecs)).Parameterize(step2, delay2, "A");
            
            foreach(var s in new []{step1, step2})                
            {
                var a = AnnotationCollection.Annotate(s)
                    .GetMember(nameof(s.SweepValues))
                    .Get<MenuAnnotation>().MenuItems.FirstOrDefault(x =>
                        x.Get<IconAnnotationAttribute>()?.IconName == IconNames.ParameterizeOnTestPlan);
                var enabled = a.Get<IEnabledAnnotation>().IsEnabled;
                if (s == step1)
                {
                    Assert.IsTrue(enabled);
                    a.Get<IMethodAnnotation>()?.Invoke();
                }
                else
                    Assert.IsFalse(enabled);
            }
        }
        
        [Test]
        public void NestedPropertyUnparameterize()
        {
            // testing unparameterize on multiple levels of parameters.
            
            var plan = new TestPlan();
            var seq = new SequenceStep();
            var logOutput = new LogStep();
            seq.ChildTestSteps.Add(logOutput);
            plan.Steps.Add(seq);
            var parameterName = "A";
            var p1 = TypeData.GetTypeData(logOutput).GetMember(nameof(logOutput.Severity)).Parameterize(seq, logOutput, parameterName);
            TypeData.GetTypeData(seq).GetMember(parameterName).Parameterize(plan, seq, parameterName);
            plan.SerializeToString(true);
            
            TypeData.GetTypeData(plan).GetMembers(); // check sanity is called, everything seems good. 
            var (x, y) = p1.ParameterizedMembers.First();
            y.Unparameterize(p1, x);
            
            // originally it failed already here since the sanity check was not done properly.
            var param2 = TypeData.GetTypeData(plan).GetMember(parameterName);
            Assert.IsNull(param2);
            
            // this has always passed.
            var param1 = TypeData.GetTypeData(seq).GetMember(parameterName);
            Assert.IsNull(param1);
            
            // this was what failed in the original bug report.
            plan.SerializeToString(true);
        }

        public class FailParameterStep : TestStep
        {
            double value;

            public double Value {
                get => value;
                set
                {
                    if (value < 0.0)
                    {
                        throw new Exception("!");
                    }

                    this.value = value;
                }
            }
            
            public override void Run()
            {
                
            }
        }


        [Test]
        public void SweepParameterStepTest()
        {
            var plan = new TestPlan();
            var step1 = new SweepParameterStep();
            plan.ChildTestSteps.Add(step1);
            
            var failStep = new FailParameterStep();
            step1.ChildTestSteps.Add(failStep);
            TypeData.GetTypeData(failStep).GetMember(nameof(failStep.Value)).Parameterize(step1, failStep, "A");
            step1.SweepValues.Add(new SweepRow(step1));
            // this should make failStep fail.
            step1.SweepValues[0].Values["A"] = -1.0;

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Error, run.Verdict);
        }

        [Test]
        public void SweepParameterErrorTest()
        {
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            plan.ChildTestSteps.Add(sweep);
            var testStep = new DelayStep();
            sweep.ChildTestSteps.Add(testStep);
            TypeData.GetTypeData(testStep).GetMember(nameof(testStep.DelaySecs)).Parameterize(sweep, testStep, "A");
            sweep.SweepValues.Add(new SweepRow(sweep));
            sweep.SweepValues[0].Values["A"] = -1;

            var result = plan.Execute();
            result.Start();

            Assert.AreEqual(Verdict.Error, result.Verdict);
        }

        [Test]
        public void MultiStepParameterize()
        {
            var plan = new TestPlan();
            for (int i = 0; i < 4; i++)
            {
                var diag = new DialogStep();
                plan.ChildTestSteps.Add(diag);
                var titleMem = TypeData.GetTypeData(diag).GetMember(nameof(diag.Title));
                titleMem.Parameterize(plan, diag, nameof(DialogStep.Title));
            }

            for (int i = 0; i < 4; i++)
            {
                Assert.IsNotNull(TypeData.GetTypeData(plan).GetMember(nameof(DialogStep.Title)));
                plan.ChildTestSteps.RemoveAt(0);
            }
            Assert.IsNull(TypeData.GetTypeData(plan).GetMember(nameof(DialogStep.Title)));
        }

        public class VerifyInstrumentTestStep : TestStep
        {
            public IInstrument Instrument { get; set; }
            public readonly HashSet<IInstrument> Instruments = new HashSet<IInstrument>();
            public override void PrePlanRun()
            {
                base.PrePlanRun();
                Instruments.Clear();
            }

            public override void Run()
            {
                if (Instrument.IsConnected == false)
                    throw new Exception("Instrument is not connected");
                UpgradeVerdict(Verdict.Pass);
                Instruments.Add(Instrument);
            }
        }
        
        [Test]
        public void SweepResourcesTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                InstrumentSettings.Current.Clear();
                
                var plan = new TestPlan();
                var step1 = new SweepParameterStep();
                var step2 = new VerifyInstrumentTestStep();
                int iterations = 10;
                var instruments = new DummyInstrument[iterations];
                for (int i = 0; i < iterations; i++)
                    instruments[i] = new DummyInstrument();
                step2.Instrument = instruments[0];
                foreach(var instr in instruments)
                    InstrumentSettings.Current.Add(instr);
                plan.ChildTestSteps.Add(step1);
                step1.ChildTestSteps.Add(step2);
                var mem = TypeData.GetTypeData(step2).GetMember(nameof(step2.Instrument));
                mem.Parameterize(step1, step2, "Instrument");
                for (int i = 0; i < iterations; i++)
                {
                    var row1 = new SweepRow(step1);
                    row1.Values["Instrument"] = instruments[i];
                    step1.SweepValues.Add(row1);
                }
                var run = plan.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.AreEqual(iterations, step2.Instruments.Count());
            }
        }

        [Test]
        public void DynamicMemberNullTest()
        {
            var plan = new TestPlan();
            
            var seq = new SequenceStep();
            plan.ChildTestSteps.Add(seq);
            var log = new LogStep();
            seq.ChildTestSteps.Add(log);
            var seqType1 = TypeData.GetTypeData(seq);
            var mems1 = seqType1.GetMembers().ToArray();
            var logType = TypeData.GetTypeData(log);
            var param = logType.GetMember(nameof(log.LogMessage)).Parameterize(seq, log, "log1");
            var seqType2 = TypeData.GetTypeData(seq);
            var mems2 = seqType2.GetMembers().ToArray();
            logType.GetMember(nameof(log.LogMessage)).Unparameterize(param, log);
            
            // previously, this getter threw an exception, since seqType2 had changed compared to seqType1
            // a coding error inside getDynamicStep caused that.
            var mems3 = seqType2.GetMembers().ToArray();
            Assert.IsTrue(mems1.Length == mems2.Length - 1); // mems1 + parameter.
            Assert.IsTrue(mems1.Length == mems3.Length); // parameter was removed.
        }

        [Test]
        public void DynamicMemberNullTest2()
        {
            var plan = new TestPlan();
            var seq = new SequenceStep();
            plan.ChildTestSteps.Add(seq);
            var log = new LogStep();
            seq.ChildTestSteps.Add(log);
            var logType = TypeData.GetTypeData(log);
            var param = logType.GetMember(nameof(log.LogMessage)).Parameterize(seq, log, "log1");
            var param2 = param.Parameterize(plan, seq, "log2");
            seq.ChildTestSteps.Clear();
            using (ParameterManager.WithSanityCheckDelayed(quickCheck: false))
            {
                var m2 = TypeData.GetTypeData(plan).GetMember(param2.Name);
                Assert.IsNotNull(m2);
            }
            using (ParameterManager.WithSanityCheckDelayed(quickCheck: true))
            {
                // since a quick-check is allowed, the check will anyway be done.
                var m2 = TypeData.GetTypeData(plan).GetMember(param2.Name);
                Assert.IsNull(m2);
            }
        }
        
        [Test]
        public void DynamicTypeDataEqualityTest()
        {
            // the ITypeData implementation if DynamicTestStepTypeData did not implement Equals / GetHashCode.
            // this gives rise to issues with caching caching, leading to possible leaks. 
            
            var plan = new TestPlan();
            var scope = new SequenceStep();
            var scope2 = new SequenceStep();
            plan.ChildTestSteps.Add(scope);
            scope.ChildTestSteps.Add(scope2);

            Dictionary<ITypeData, int> dict = new Dictionary<ITypeData, int>();
            
            var newMem = TypeData.GetTypeData(scope2).GetMember(nameof(scope2.Name)).Parameterize(scope, scope2, "param");
            scope.Name = "Test {param}";
            var tp0 = TypeData.GetTypeData(scope);

            Assert.IsNotNull(newMem);
            for (int i = 0; i < 5; i++)
            {
                var tp1 = TypeData.GetTypeData(scope);
                Assert.IsTrue(object.ReferenceEquals(tp1, tp0));
                dict[tp1] = i;
            }

            // all the values should be inserted at the same key location.
            Assert.AreEqual(1, dict.Count);
        }
        
    }
}

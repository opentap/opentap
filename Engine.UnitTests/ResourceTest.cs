using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenTap.Diagnostic;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class TestResource : IInstrument
    {
        public bool HasBeenOpened { get; set; } = false;

        public string Name { get; set; } = nameof(TestResource);

        public void Open()
        {
            IsConnected = true;
            HasBeenOpened = true;
        }

        public void Close()
        {
            IsConnected = false;
        }
        
        public bool IsConnected { get; private set; }
        // Disable 'unused' warnings
#pragma warning disable CS0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
    }
    public class ResourceTest
    {
        public class ResourceTestStep : TestStep
        {
            [EnabledIf(nameof(ResourceEnabled))]
            public IInstrument MyTestResource { get; set; }
            public bool ResourceEnabled { get; set; }
            public override void Run()
            {
                try
                {
                    if (ResourceEnabled)
                        Assert.IsTrue(MyTestResource.IsConnected);
                }
                catch
                {
                    
                }
            }
        }
        
        [AllowAnyChild]
        public class IgnoredResourceStep : TestStep
        {
            [ResourceOpen(ResourceOpenBehavior.Ignore)]
            public TestResource SomeInstrument { get; set; }

            public override void Run()
            {
                Assert.IsFalse(SomeInstrument.IsConnected);
                RunChildSteps();
                Assert.IsFalse(SomeInstrument.IsConnected);
            }
        }
        
        
        [Test]
        public void ResourceParameterOpenTest()
        {
            var oldResourceManager = EngineSettings.Current.ResourceManagerType;

            try
            {
                foreach (var resourceManager in new IResourceManager[]
                    {new ResourceTaskManager(), new LazyResourceManager()})
                {
                    EngineSettings.Current.ResourceManagerType = resourceManager;
                    var testPlan = new TestPlan();

                    var ignoredResourceStep = new IgnoredResourceStep();
                    var resourceStep = new ResourceTestStep();
                    ignoredResourceStep.ChildTestSteps.Add(resourceStep);
                    testPlan.ChildTestSteps.Add(ignoredResourceStep);

                    ignoredResourceStep = new IgnoredResourceStep();
                    resourceStep = new ResourceTestStep();
                    ignoredResourceStep.ChildTestSteps.Add(resourceStep);
                    testPlan.ChildTestSteps.Add(ignoredResourceStep);

                    testPlan.Execute();
                }
            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = oldResourceManager;
            }
        }
        
        class TestLogListener : ILogListener
        {
            public List<Event> EventList { get; } = new List<Event>();
            public void EventsLogged(IEnumerable<Event> Events)
            {
                EventList.AddRange(Events);
            }

            public void Flush()
            {
            }

            public void Clear()
            {
                EventList.Clear();
            }

            private bool Contains(string msg)
            {
                var list = new List<Event>(EventList);
                return list.Any(x => x.Message.ToLower().Contains(msg.ToLower()));
            }

            public void AssertContains(string msg)
            {
                Assert.IsTrue(Contains(msg));
            }

            public void AssertDoesNotContain(string msg)
            {
                Assert.IsFalse(Contains(msg));
            }

            private bool ContainsWarningsOrErrors()
            {
                var list = new List<Event>(EventList);
                return list.Any(x =>
                    x.EventType == (int) LogEventType.Warning || x.EventType == (int) LogEventType.Error);
            }

            public void AssertWarnings()
            {
                Assert.IsTrue(ContainsWarningsOrErrors());
            }

            public void AssertNoWarnings()
            {
                Assert.IsFalse(ContainsWarningsOrErrors());
            }

        }

        static class Messages
        {
            public const string ResourceOpened = @"Resource ""OpenTap.UnitTests.TestResource"" opened.";
            public const string MyTestResourceNotSet = @"Resource setting MyTestResource not set on step";
            public const string ResourceMissingFromBenchSettings = @"Test Plan changed due to resources missing from Bench settings.";
            public const string TestPlanCompleted = @"""Test Plan Reference \ ResourceTestStep"" completed.";
            public const string EnabledIfPropertyMissing = @"Could not find property 'ResourceEnabled'";
        }

        [Test]
        public void EnabledIfResourceWarningTest()
        {
            var listener = new TestLogListener();

            var resource = new TestResource();
            var enabledStep = new ResourceTestStep()
                {ResourceEnabled = true, MyTestResource = resource};
            var disabledStep = new ResourceTestStep()
                {ResourceEnabled = false, MyTestResource = resource};
            

            TestPlanRun ExecutePlan(ResourceTestStep step, bool resourceDisappears)
            {
                InstrumentSettings.Current.Clear();
                InstrumentSettings.Current.Add(resource);

                var serializer = new TapSerializer();
                
                var testPlan = new TestPlan();
                testPlan.ChildTestSteps.Add(step);
                
                testPlan.ExternalParameters.Add(step,
                    TypeData.GetTypeData(step).GetMember(nameof(step.MyTestResource)));
                
                Assert.AreEqual(1, TypeData.GetTypeData(testPlan).GetMembers().OfType<IParameterMemberData>().Count());
                
                testPlan.Save("testTestPlan.TapPlan");

                var realTestPlan = new TestPlan();
                var realTestStep = new TestPlanReference()
                    {Filepath = new MacroString() {Text = testPlan.Path}};
                realTestPlan.ChildTestSteps.Add(realTestStep);
                realTestStep.LoadTestPlan();
                realTestPlan.ChildTestSteps.Add(step);
                
                Assert.AreEqual(1, TypeData.GetTypeData(realTestStep).GetMembers().OfType<IParameterMemberData>().Count());

                var testPlanText = serializer.SerializeToString(realTestPlan);
                
                // Reload the test plan without the resource -- this should cause a warning
                if (resourceDisappears)
                    InstrumentSettings.Current.Clear();
                
                var deserializedTestPlan = (TestPlan)serializer.DeserializeFromString(testPlanText);
                (deserializedTestPlan.ChildTestSteps.First() as TestPlanReference).LoadTestPlan();
                return deserializedTestPlan.Execute();
            }
            
            
            var oldResourceManager = EngineSettings.Current.ResourceManagerType;
            
            try
            {
                Log.AddListener(listener);

                foreach (var resourceManager in new IResourceManager[]
                    {new ResourceTaskManager(), new LazyResourceManager()})
                {
                    EngineSettings.Current.ResourceManagerType = resourceManager;
                    
                    listener.Clear();
                    resource.HasBeenOpened = false;

                    {   /* Resource enabled
                         * Resource is present when reloading the plan */
                        var run = ExecutePlan(enabledStep, false);
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.NotSet, run.Verdict);

                        Assert.IsTrue(resource.HasBeenOpened);

                        listener.AssertContains(Messages.ResourceOpened);
                        listener.AssertDoesNotContain(Messages.MyTestResourceNotSet);
                        listener.AssertDoesNotContain(Messages.ResourceMissingFromBenchSettings);
                        listener.AssertContains(Messages.TestPlanCompleted);
                        listener.AssertDoesNotContain(Messages.EnabledIfPropertyMissing);
                        listener.AssertNoWarnings();
                    }

                    listener.Clear();
                    resource.HasBeenOpened = false;

                    {   /* Resource disabled
                         * Resource is present when reloading the plan */
                        var run = ExecutePlan(disabledStep, false);
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.NotSet, run.Verdict);
                        
                        Assert.IsFalse(resource.HasBeenOpened);

                        listener.AssertDoesNotContain(Messages.ResourceOpened);
                        listener.AssertDoesNotContain(Messages.MyTestResourceNotSet);
                        listener.AssertDoesNotContain(Messages.ResourceMissingFromBenchSettings);
                        listener.AssertContains(Messages.TestPlanCompleted);
                        listener.AssertDoesNotContain(Messages.EnabledIfPropertyMissing);
                        listener.AssertNoWarnings();
                    }

                    listener.Clear();
                    resource.HasBeenOpened = false;

                    {   /* Resource disabled
                         * Resource is gone when reloading the plan */
                        var run = ExecutePlan(disabledStep, true);
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.NotSet, run.Verdict);
                        
                        Assert.IsFalse(resource.HasBeenOpened);

                        listener.AssertDoesNotContain(Messages.ResourceOpened);
                        listener.AssertDoesNotContain(Messages.MyTestResourceNotSet);
                        listener.AssertContains(Messages.ResourceMissingFromBenchSettings);
                        listener.AssertContains(Messages.TestPlanCompleted);
                        listener.AssertDoesNotContain(Messages.EnabledIfPropertyMissing);
                        listener.AssertWarnings();
                    }

                    listener.Clear();
                    resource.HasBeenOpened = false;

                    {   /* Resource enabled
                         * Resource is gone when reloading the plan */
                        var run = ExecutePlan(enabledStep, true);
                        
                        if (resourceManager is LazyResourceManager)
                            Assert.IsFalse(run.FailedToStart);
                        if (resourceManager is ResourceTaskManager)
                            Assert.IsTrue(run.FailedToStart);
                        
                        Assert.AreEqual(Verdict.Error, run.Verdict);
                        
                        Assert.IsFalse(resource.HasBeenOpened);

                        listener.AssertDoesNotContain(Messages.ResourceOpened);
                        listener.AssertContains(Messages.MyTestResourceNotSet);
                        listener.AssertContains(Messages.ResourceMissingFromBenchSettings);
                        listener.AssertDoesNotContain(Messages.TestPlanCompleted);
                        listener.AssertDoesNotContain(Messages.EnabledIfPropertyMissing);
                        listener.AssertWarnings();
                    }
                }
            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = oldResourceManager;
                OpenTap.Log.RemoveListener(listener);
                InstrumentSettings.Current.Clear();
            }
        }
    }
}

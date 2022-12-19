using System;
using System.Collections.Generic;
using OpenTap.Diagnostic;

namespace OpenTap.Plugins.BasicSteps
{
    [Display("Nested Test Plan", "Run a nested test plan in an isolated fashion.", "Flow Control")]
    [AllowAnyChild]
    public class NestedTestPlan : TestPlanReference
    {
        /// <summary> This nested result listener forwards the results.</summary>
        class ResultForwarder : ResultListener
        {
            readonly ResultSource proxy;
            public ResultForwarder(ResultSource proxy) => this.proxy = proxy;

            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                base.OnResultPublished(stepRunId, result);
                proxy.PublishTable(result);
            }
        }

        /// <summary> This log forwarder forwards the results from the nested plan. </summary>
        class LogForwarder : ILogListener
        {
            readonly ILogContext2 forwardTo;
            readonly string prepend;
            public LogForwarder(ILogContext2 forwardTo, string prepend)
            {
                this.forwardTo = forwardTo;
                this.prepend = prepend;
            }

            public void EventsLogged(IEnumerable<Event> events)
            {
                foreach (var evtIt in events)
                {
                    // Store the event in a local variable to allow modifying it (struct).
                    var evt = evtIt;
                    
                    // Add the prepend if applicable
                    if(!string.IsNullOrEmpty(prepend))
                        evt.Message = prepend + evt.Message;
                    
                    // Forward the event to the real log context.
                    forwardTo.AddEvent(evt);
                }
            }

            public void Flush() { }
        }
        
        [Display("Forward Log Events")]
        public bool ForwardLogEvents { get; set; }

        [EnabledIf(nameof(ForwardLogEvents), true, HideIfDisabled = true)]
        [Display("Log Header", "Prepend this to the log messages forwarded from the inner test plan.")]
        public string LogHeader { get; set; } = "Inner plan: ";
        
        [Display("Forward Results")]
        public bool ForwardResults { get; set; }
        MacroString filepath = new MacroString();
        string currentlyLoaded;

        [Display("Referenced Plan", Order: 0, Description: "A file path pointing to a test plan which will be loaded as read-only test steps.")]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "TapPlan")]
        [DeserializeOrder(1.0)]
        public override MacroString Filepath 
        { 
            // this is copied from TestPlanReference.
            get => filepath;
            set
            {
                filepath = value;
                filepath.Context = this;
                try
                {
                    var rp = TapSerializer.GetObjectDeserializer(this)?.ReadPath;
                    if (rp != null) rp = System.IO.Path.GetDirectoryName(rp);
                    var fp = filepath.Expand(testPlanDir: rp);
                    
                    if (currentlyLoaded != fp)
                    {
                        LoadTestPlan();
                        currentlyLoaded = fp;
                    }
                }
                catch { }
            }
        }

        public override void Run()
        {
            // the inner plan needs to be reloaded when executed.
            // this is the simplest way to do that.
            // Note: the performance penalties of this approach should be considered.
            var xml = plan.SerializeToString();
            
            // for log forwarding, if applicable.
            LogForwarder forwarder = null;

            // forwarding is only supported in this case.
            if(ForwardLogEvents && OpenTap.Log.Context is ILogContext2 ctx2)
                forwarder = new LogForwarder(ctx2, LogHeader);
            
            // in the new session, redirect logging in all cases.
            // create new resources and everything else by overlaying component settings.
            using (Session.Create(SessionOptions.RedirectLogging | SessionOptions.OverlayComponentSettings))
            {
                // load the test plan from XML to ensure getting the new resource references. 
                var plan2 = Utils.DeserializeFromString<TestPlan>(xml);
                
                // don't print the summary of the inner test plan run
                plan2.PrintTestPlanRunSummary = false;
                
                // forward the log messages if enabled.
                if(forwarder != null)
                    OpenTap.Log.AddListener(forwarder);
                
                // override the plans break conditions with the break conditions of this step.
                // otherwise the nested plan will get the break conditions from engine settings, which would be
                // inconsistent with how it otherwise works.
                BreakConditionProperty.SetBreakCondition(plan2, BreakConditionProperty.GetBreakCondition(this));
                
                // this is what actually runs the test plan.
                // since it is running inside a session, it can be aborted.
                // it has very limited ways to affect the outer test plan.
                var subRun = plan2.Execute((ForwardResults ? new IResultListener[] {new ResultForwarder(Results)} as IEnumerable<IResultListener> : ResultSettings.Current));
                
                // upgrade the verdict with the current.
                UpgradeVerdict(subRun.Verdict);
            }
        }
    }
}
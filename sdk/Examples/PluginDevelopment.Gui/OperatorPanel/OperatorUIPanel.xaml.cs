using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Keysight.OpenTap.Gui;
using Keysight.OpenTap.Wpf;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public partial class OperatorUIPanel : UserControl
    {
        public ITapDockContext Context { get; }
        public OperatorUiViewModel ViewModel { get; } = new OperatorUiViewModel();
        
        readonly OperatorResultListener resultListener = new OperatorResultListener();
        
        public OperatorUIPanel(ITapDockContext context, OperatorUiSetting operatorUiSetting = null)
        {
            Context = context;
            Context.ResultListeners.Add(resultListener);
            ViewModel.ResultListener = resultListener;
            ViewModel.OperatorUiSetting = operatorUiSetting ?? new OperatorUiSetting();
            ViewModel.Context = context;
            
            InitializeComponent();
            
            IsVisibleChanged += OnIsVisibleChanged;
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(Equals(e.NewValue, true))
                RenderDispatch.RenderingSlow += RenderDispatchOnRenderingSlow;
            else 
                RenderDispatch.RenderingSlow -= RenderDispatchOnRenderingSlow;
        }

        void RenderDispatchOnRenderingSlow(object sender, EventArgs e) => ViewModel.UpdateTime();
        void StartButton_Clicked(object sender, RoutedEventArgs e) => ViewModel.ExecuteTestPlan();
        void StopButton_Clicked(object sender, RoutedEventArgs e) => ViewModel.StopTestPlan();

        void FixAspectRation_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var aspect = e.NewSize.Height / ( e.NewSize.Width + 1);
            grd.Height = grd.Width * aspect;
        }

        void DutIdEntered_OnClick(object sender, RoutedEventArgs e) => ViewModel.DutIdEntered("");

        void DutEnter_VisibileChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Equals(true, e.NewValue))
                (sender as FrameworkElement)?.Focus();
        }
    }

    public enum TestPlanStatus
    {
        None,
        Passed,
        Failed,
        Running,
        Aborted
    }

    public class OperatorUiViewModel : INotifyPropertyChanged
    {
        internal OperatorResultListener ResultListener { get; set; }
        readonly Stopwatch startedTimer = new Stopwatch();
        
        TestPlanStatus status;
        public string ElapsedTime => $"{startedTimer.Elapsed.Minutes} m {startedTimer.Elapsed.Seconds}.{startedTimer.Elapsed.Milliseconds/100} s";

        public TestPlanStatus Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        public ITapDockContext Context { get; set; }

        public string DutID { get; set; } = "N/A";

        public bool AskForDutID { get; set; } = false;

        public class PromotedResults : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public ITestStep StepSource { get; set; }
            public IMemberData Member { get; set; }
            public Verdict Verdict { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;

            public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        public List<PromotedResults> ResultsList { get; set; }= new List<PromotedResults>();

        void UpdatePromotedResults()
        {
            var newResults = new List<PromotedResults>();
            var allSteps = (currentPlan ?? Context.Plan).Steps.RecursivelyGetAllTestSteps(TestStepSearch.EnabledOnly);
            
            foreach (var step in allSteps)
            {
                var resultMembers = TypeData.GetTypeData(step)
                    .GetMembers()
                    .Where(x => x.HasAttribute<ResultAttribute>())
                    .ToArray();
                foreach (var r in resultMembers)
                {
                    newResults.Add(new
                    PromotedResults {
                        Name = r.GetDisplayAttribute().Name,
                        Value = "  ",
                        StepSource = step,
                        Member = r,
                        Verdict = Verdict.NotSet
                    });   
                }
            }

            ResultsList = newResults;
            OnPropertyChanged(nameof(ResultsList));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        TestPlan plan;
        public void UpdateTime()
        {
            OnPropertyChanged(nameof(ElapsedTime));
            if (plan != Context.Plan)
            {
                plan = Context.Plan;
                GuiHelpers.GuiInvoke(UpdatePromotedResults);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary> Used for stopping the test plan run. </summary>
        CancellationTokenSource cancellationToken;

        class UserInputOverride : IUserInputInterface, IUserInterface
        {
            readonly Action enterDutStarted;
            readonly Action enterDutEnded;
            IUserInputInterface prev;
            IUserInterface prev2;

            public UserInputOverride(object prev, Action enterDutStarted, Action enterDutEnded)
            {
                this.enterDutStarted = enterDutStarted;
                this.enterDutEnded = enterDutEnded;
                this.prev = prev as IUserInputInterface;
                this.prev2 = prev as IUserInterface;
            }

            bool enterComplete = false;
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                if (dataObject.GetType().Name == "MetadataPromptObject")
                {
                    enterDutStarted?.Invoke();
                    try
                    {
                        while (!enterComplete)
                        {
                            TapThread.Sleep(100);
                        }
                    }
                    finally
                    {
                        enterDutEnded?.Invoke();
                    }

                }else
                    prev?.RequestUserInput(dataObject, Timeout, modal);
            }

            public void EnterComplete()
            {
                enterComplete = true;
            }

            public void NotifyChanged(object obj, string property)
            {
                prev2?.NotifyChanged(obj, property);
            }
        }

        TestPlan currentPlan;
        Session executorSession;
        internal OperatorUiSetting OperatorUiSetting;

        public void ExecuteTestPlan()
        {
            var xml = Context.Plan.GetCachedXml();
            if (xml == null)
            {
                var str = new MemoryStream();
                Context.Plan.Save(str);
                xml = str.ToArray();
            }

            executorSession = Session.Create(SessionOptions.OverlayComponentSettings);
            {
                currentPlan = TestPlan.Load(new MemoryStream(xml), Context.Plan.Path);

                var a = AnnotationCollection.Annotate(currentPlan);
                foreach (var member in a.Get<IMembersAnnotation>().Members)
                {
                    var str = member.Get<IStringValueAnnotation>();
                    if (str == null) continue; // this parameter cannot be set.
                    var name = member.Get<IDisplayAnnotation>()?.Name;
                    var param = OperatorUiSetting.Parameters.FirstOrDefault(x => x.Name == name);
                    if (param == null) continue;
                    try
                    {
                        str.Value = param.Value;
                    }
                    catch
                    {
                        
                    }
                }

                a.Write();
                
                var prev = UserInput.Interface as IUserInputInterface;
                var ui = new UserInputOverride(prev,
                    () =>
                    {
                        GuiHelpers.GuiInvoke(() =>
                        {
                            DutID = "";
                            AskForDutID = true;
                            OnPropertyChanged("");
                        });
                    },
                    () =>
                    {
                        GuiHelpers.GuiInvoke(() =>
                        {
                            AskForDutID = false;
                            OnPropertyChanged("");
                        });
                    });
                UserInput.SetInterface(ui);
                UpdatePromotedResults();
                DutID = "N/A";
                OnPropertyChanged("");
                var resultListeners = ResultSettings.Current;
                var addedListener = new OperatorResultListener();
                addedListener.TestStepRunStart += AddedListenerOnTestStepRunStart;
                addedListener.TestPlanRunStarted += AddedListenerOnTestPlanRunStarted;
                startedTimer.Restart();
                Status = TestPlanStatus.Running;
                cancellationToken = new CancellationTokenSource();
                var runTask = currentPlan.ExecuteAsync(resultListeners.Concat(new IResultListener[] { addedListener }),
                    Array.Empty<ResultParameter>(), null, cancellationToken.Token);

                runTask.ContinueWith(t =>
                {
                    startedTimer.Stop();
                    GuiHelpers.GuiInvoke(() =>
                    {
                        if (t.IsCanceled)
                            Status = TestPlanStatus.Aborted;
                        else if (t.Result.Verdict == Verdict.Aborted)
                            Status = TestPlanStatus.Aborted;
                        else if (t.Result.Verdict > Verdict.Pass)
                            Status = TestPlanStatus.Failed;
                        else
                            Status = TestPlanStatus.Passed;
                        UserInput.SetInterface(prev);
                        executorSession.Dispose();
                    });
                });
                var prevSession = executorSession;
                executorSession = Session.Create(SessionOptions.None);
                prevSession.Dispose();
            }
        }

        void AddedListenerOnTestPlanRunStarted(object sender, TestPlanRun e)
        {
            DutID = e.Parameters.FirstOrDefault(x => x.Name == "ID")?.Value?.ToString() ?? "?";
            GuiHelpers.GuiInvokeAsync(() => OnPropertyChanged(nameof(DutID)));
        }

        void AddedListenerOnTestStepRunStart(object sender, TestStepRun run)
        {
            foreach (var mem in ResultsList)
            {
                if (mem.StepSource.Id == run.TestStepId)
                {
                    mem.Value = mem.Member.GetValue(mem.StepSource)?.ToString() ?? "<null>";
                    var unit = mem.Member.GetAttribute<UnitAttribute>()?.Unit;
                    if(unit != null)
                        mem.Value += " " + unit;
                    mem.Verdict = run.Verdict;
                    
                    GuiHelpers.GuiInvokeAsync(() => mem.OnPropertyChanged(string.Empty));
                }
            }
        }

        public void StopTestPlan()
        {
            cancellationToken?.Cancel();
        }

        public void DutIdEntered(string data)
        {
            executorSession.RunInSession(() => {
            (UserInput.Interface as UserInputOverride)?.EnterComplete();
            
            foreach(var dutmem in TypeData.GetTypeData(currentPlan).GetMembers().Where(x => x.TypeDescriptor.DescendsTo(typeof(Dut))))
            {
                if (dutmem.GetValue(currentPlan) is Dut d)
                    d.ID = this.DutID;
            }
            });
        }
    }

    class OperatorResultListener : ResultListener
    {
        public event EventHandler<TestPlanRun> TestPlanRunStarted;
        public event EventHandler<TestStepRun> TestStepRunStart;
        Dictionary<Guid, TestStepRun> activeRuns = new Dictionary<Guid, TestStepRun>();
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            TestPlanRunStarted?.Invoke(this, planRun);
            base.OnTestPlanRunStart(planRun);
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            activeRuns[stepRun.Id] = stepRun;
            base.OnTestStepRunStart(stepRun);
        }
        
        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            TestStepRunStart?.Invoke(this, stepRun);
            activeRuns.Remove(stepRun.Id);
            base.OnTestStepRunStart(stepRun);
        }

    }
}
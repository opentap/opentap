using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Keysight.Ccl.Wsl.UI;
using Keysight.OpenTap.Wpf;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public partial class OperatorUiPanel : UserControl
    {
        public ITapDockContext Context { get; }
        public OperatorUiViewModel ViewModel { get;  } 
        
        readonly OperatorResultListener resultListener = new OperatorResultListener();
        
        public OperatorUiPanel(ITapDockContext context, OperatorUiSetting operatorUiSetting, OperatorUiViewModel vm = null)
        {
            ViewModel = vm ?? new OperatorUiViewModel();
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
        void DutIdEntered_OnClick(object sender, RoutedEventArgs e) => ViewModel.DutIdEntered("");

        void DutEnter_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Equals(true, e.NewValue))
                (sender as FrameworkElement)?.Focus();
        }

        [Display("Edit the name")]
        public class ChangeName
        {
            [Layout(LayoutMode.FullRow)]
            public string NewName { get; set; }

            [Submit]
            [Layout(LayoutMode.FloatBottom|LayoutMode.FullRow)]
            public OkCancel Submit { get; set; } = OkCancel.Cancel;

            public enum OkCancel
            {
                Ok,
                Cancel
            }
        }

        void ChangeName_Clicked(object sender, RoutedEventArgs e)
        {
            var name = new ChangeName { NewName = ViewModel.Name };
            
            UserInput.Request(name);
            if (name.Submit == ChangeName.OkCancel.Ok)
            {
                ViewModel.Name = name.NewName;
                OperatorUiSettings.Current.Save();
            }
        }

        readonly TraceSource log = Log.CreateSource("OperatorUI");
        void EditParameters_Clicked(object sender, RoutedEventArgs e)
        {
            var xml = Context.Plan.GetCachedXml();
            if (xml == null)
            {
                var str = new MemoryStream();
                Context.Plan.Save(str);
                xml = str.ToArray();
            }
            
            var setting = ViewModel.OperatorUiSetting;
            var tmpPlan = TestPlan.Load(new MemoryStream(xml), Context.Plan.Path);
            var a = AnnotationCollection.Annotate(tmpPlan);
            foreach (var member in a.Get<IMembersAnnotation>().Members)
            {
                var str = member.Get<IStringValueAnnotation>();
                if (str == null) continue; // this parameter cannot be set.
                var name = member.Get<IDisplayAnnotation>()?.Name;
                var param = setting.Parameters.FirstOrDefault(x => x.Name == name);
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

            UserInput.Request(tmpPlan);
            
            foreach (var member in AnnotationCollection.Annotate(tmpPlan).Get<IMembersAnnotation>().Members)
            {
                if (member.Get<IMemberAnnotation>()?.Member is IParameterMemberData)
                {
                    var stringValue = member.Get<IStringValueAnnotation>()?.Value;
                    var name = member.Get<IDisplayAnnotation>()?.Name;
                    if (stringValue == null || name == null)
                        continue;
                    var param = setting.Parameters.FirstOrDefault(p => p.Name == name);
                    if (param == null)
                    {
                        param = new UiParameter
                        {
                            Name = name
                        };
                        setting.Parameters.Add(param);
                    }else if (param.Value == stringValue)
                        continue;

                    param.Value = stringValue;
                    log.Info("Setting {0} = {1}", name, stringValue);
                }
            }
            OperatorUiSettings.Current.Save();
        }

        void ViewLog_OnClick(object sender, RoutedEventArgs e)
        {
            var panel = new LogPanel();
            panel.AddLogMessages(ViewModel.LogEvents);
            var dialog = new WslDialog
            {
                Content = panel,
                Title = $"{ViewModel.Name}: log",
                Owner = Window.GetWindow(this),
                Width = 700,
                Height= 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Manual
            };
            dialog.Show();
        }
    }
}
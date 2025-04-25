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
    /// <summary>
    /// The operator UI panel itself, one panel represents one session, one play button, etc.
    /// but it shares the currently loaded test plan with other panels.
    /// </summary>
    public partial class OperatorUiPanel : UserControl
    {
        public ITapDockContext Context { get; }
        public OperatorPanelViewModel ViewModel { get;  } 
        
        readonly TraceSource log = Log.CreateSource("OperatorUI");

        public OperatorUiPanel(ITapDockContext context, OperatorPanelSetting operatorPanelSetting, OperatorPanelViewModel vm = null)
        {
            ViewModel = vm ?? new OperatorPanelViewModel();
            Context = context;
            ViewModel.operatorPanelSetting = operatorPanelSetting ?? new OperatorPanelSetting();
            ViewModel.Context = context;
            
            InitializeComponent();
            
            IsVisibleChanged += OnIsVisibleChanged;
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(Equals(e.NewValue, true))
                RenderDispatch.RenderingSlow += RenderDispatch_OnRenderingSlow;
            else 
                RenderDispatch.RenderingSlow -= RenderDispatch_OnRenderingSlow;
        }

        /// <summary> Update the time in the UI every 4th frame or so.</summary>
        void RenderDispatch_OnRenderingSlow(object sender, EventArgs e) => ViewModel.UpdateTime();
        
        void StartButton_Clicked(object sender, RoutedEventArgs e) => ViewModel.ExecuteTestPlan();
        void StopButton_Clicked(object sender, RoutedEventArgs e) => ViewModel.StopTestPlan();
        void DutIdEntered_OnClick(object sender, RoutedEventArgs e) => ViewModel.DutIdEntered();
        void DutEnter_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Equals(true, e.NewValue))
                (sender as FrameworkElement)?.Focus();
        }

        /// <summary>
        /// Model for for when the user wants to change the name of the panel.
        /// </summary>
        [Display("Edit the name of this panel.")]
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
            // use UserInput.Request to change the name. of the panel.
            var name = new ChangeName { NewName = ViewModel.Name };
            
            UserInput.Request(name);
            if (name.Submit == ChangeName.OkCancel.Ok)
            {
                ViewModel.Name = name.NewName;
                OperatorUiSettings.Current.Save();
            }
        }

        void EditParameters_Clicked(object sender, RoutedEventArgs e)
        {
            // Clone the test plan by serializing to an XML byte array
            // and then reload the XML.
            
            var xml = Context.Plan.GetCachedXml();
            if (xml == null)
            {
                var str = new MemoryStream();
                Context.Plan.Save(str);
                xml = str.ToArray();
            }
            
            var setting = ViewModel.operatorPanelSetting;
            
            // create a temporary plan.
            var tmpPlan = TestPlan.Load(new MemoryStream(xml), Context.Plan.Path);
            
            // load all the currently defined settings. AnnotationCollection is used to figure out which things
            // can easily be stored as a string.
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

            // write the data to the object.
            a.Write();

            // popup a UI allowing to edit the temporary plan settings.
            UserInput.Request(tmpPlan);
            
            // write back the changes to the component settings. Again use annotation collection.
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
                        param = new PanelParameter
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
            
            // save the changes to disk.
            OperatorUiSettings.Current.Save();
        }

        void ViewLog_OnClick(object sender, RoutedEventArgs e)
        {
            // open a log panel an show the current messages.
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
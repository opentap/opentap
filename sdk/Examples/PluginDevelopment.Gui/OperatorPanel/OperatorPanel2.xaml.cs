using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Keysight.OpenTap.Wpf;

namespace PluginDevelopment.Gui.OperatorPanel
{
    public partial class OperatorMainPanel : UserControl
    {
        readonly ITapDockContext tapDockContext;
        public OperatorMainPanelViewModel ViewModel { get; set; } = new OperatorMainPanelViewModel();
        public OperatorMainPanel(ITapDockContext tapDockContext)
        {
            this.tapDockContext = tapDockContext;
            InitializeComponent();
            baseGrid.DataContext = ViewModel;
        }

        readonly Dictionary<OperatorUiSetting, OperatorUiViewModel> viewModels =
            new Dictionary<OperatorUiSetting, OperatorUiViewModel>();
        void PanelContainer_Loaded(object sender, RoutedEventArgs e)
        {
            var decorator = (Decorator)sender;
            var viewModel = decorator.DataContext as OperatorUiSetting;
            if (viewModel == null) return;
            if (!viewModels.TryGetValue(viewModel, out var model2))
            {
                model2 = new OperatorUiViewModel();
                viewModels[viewModel] = model2;
            }

            var panel = new OperatorUiPanel(tapDockContext, viewModel, model2);
            decorator.Child = panel;
        }
    }
}
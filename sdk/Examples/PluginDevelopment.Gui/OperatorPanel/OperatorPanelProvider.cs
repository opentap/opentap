using System.Windows;
using Keysight.OpenTap.Wpf;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    [Display("Operator UI", Group:"Examples")]
    public class OperatorPanelProvider : ITapDockPanel
    {
        public FrameworkElement CreateElement(ITapDockContext context) =>  new OperatorMainPanel(context);

        public double? DesiredWidth => 200;
        public double? DesiredHeight => 200;
    }
}